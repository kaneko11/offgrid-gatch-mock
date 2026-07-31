using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// High-level facade (Phase 17) for the experimental shim infrastructure.
///
/// <para>It bundles the lower-level <see cref="NewInterceptionHarness"/>, the assembly rewrite,
/// the isolated load context, and the active <see cref="ShimContext"/> behind a single
/// <see cref="IDisposable"/> session so that callers do not have to orchestrate them by hand.</para>
///
/// <para><b>Typical usage — new interception:</b></para>
/// <code>
/// using static MiniMockito.Shims.Experimental.ShimArg;
///
/// using (var shims = Shims.For&lt;UserService&gt;()
///                         .WithNew&lt;UserRepository&gt;())
/// {
///     var fakeRepo = shims.CreateFake&lt;UserRepository&gt;("fake");
///
///     shims.New&lt;UserRepository&gt;()
///          .WithArguments(Eq("prod"))
///          .Returns(fakeRepo);
///
///     var service = shims.CreateObject(typeof(UserService).FullName);
///     var result = shims.Invoke&lt;string&gt;(service, "GetDisplayName", 1);
/// }
/// </code>
///
/// <para><b>Type identity:</b> rewritten types live in an isolated load context, so a rewritten
/// concrete type cannot be cast to the same-named type in the default context.  Prefer
/// <see cref="CreateObject"/> + <see cref="Invoke{TResult}"/>.  <see cref="Create{T}"/> only
/// succeeds for a shared contract such as <see cref="IShimCreatable"/>.</para>
///
/// <para><b>Parallelism:</b> the shim dispatcher uses process-wide state.  Annotate test
/// assemblies with <c>[assembly: DoNotParallelize]</c>.</para>
///
/// <para><b>Limitations:</b> BCL static methods (e.g. <c>DateTime.Now</c>,
/// <c>File.ReadAllText</c>), generic static methods, by-ref/out parameters, and production
/// in-place rewrite are not supported.</para>
/// </summary>
public sealed class Shims : IDisposable
{
    private readonly string _targetAssemblyPath;
    private readonly NewInterceptionHarness _harness;
    private readonly List<Type> _newTargets = new List<Type>();
    private readonly List<Type> _staticTargets = new List<Type>();
    private readonly List<Action<Shims>> _pendingReplacements = new List<Action<Shims>>();

    private ShimContext? _context;
    private bool _finalized;
    private bool _disposed;

    private Shims(string targetAssemblyPath)
    {
        _targetAssemblyPath = targetAssemblyPath;
        _harness = NewInterceptionHarness.Create();
    }

    /// <summary>
    /// Starts a new shim session whose interception applies to the assembly that defines
    /// <typeparamref name="TAnchor"/>.  <typeparamref name="TAnchor"/> is typically the service
    /// type whose method bodies contain the <c>new</c> / static call sites to intercept.
    /// </summary>
    /// <typeparam name="TAnchor">A concrete, public type in the assembly to rewrite.</typeparam>
    public static Shims For<TAnchor>() where TAnchor : class
        => new Shims(typeof(TAnchor).Assembly.Location);

    /// <summary>
    /// Starts a new shim session that rewrites the assembly at <paramref name="targetAssemblyPath"/>
    /// (Phase 23).  Use this with <see cref="ReplaceNew{T}(T)"/> / <see cref="ReplaceNew(Type, object)"/> /
    /// <see cref="ReplaceNew(string, string, object)"/> to intercept <c>new</c> calls without needing a
    /// compile-time anchor type.
    /// </summary>
    /// <param name="targetAssemblyPath">Path to the assembly whose <c>newobj</c> call sites are rewritten.</param>
    public static Shims ForAssembly(string targetAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(targetAssemblyPath))
            throw new ArgumentException("Target assembly path must be provided.", nameof(targetAssemblyPath));
        return new Shims(targetAssemblyPath);
    }

    /// <summary>
    /// Registers <typeparamref name="TTarget"/> as a <c>newobj</c> interception target.
    /// Must be called before the rewrite is finalized (before the first
    /// <see cref="New{TTarget}"/>, <see cref="CreateFake{TTarget}"/>, <see cref="Create{T}"/>,
    /// <see cref="CreateObject"/>, or <see cref="Invoke{TResult}"/> call).
    /// </summary>
    /// <typeparam name="TTarget">The concrete, non-generic class whose <c>new</c> calls are intercepted.</typeparam>
    /// <exception cref="InvalidOperationException">The rewrite has already been finalized.</exception>
    public Shims WithNew<TTarget>() where TTarget : class
    {
        ThrowIfDisposed();
        ThrowIfFinalized("WithNew");
        _harness.WithTarget<TTarget>();
        _newTargets.Add(typeof(TTarget));
        return this;
    }

    /// <summary>
    /// Registers <paramref name="declaringType"/> as a user-defined static method interception
    /// target.  Must be called before the rewrite is finalized.
    /// </summary>
    /// <param name="declaringType">The type declaring the static methods to intercept.</param>
    /// <exception cref="InvalidOperationException">The rewrite has already been finalized.</exception>
    public Shims WithStatic(Type declaringType)
    {
        ThrowIfDisposed();
        if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
        ThrowIfFinalized("WithStatic");
        _harness.WithStaticTarget(declaringType);
        _staticTargets.Add(declaringType);
        return this;
    }

    /// <summary>
    /// Easy API (Phase 23): replaces <c>new T()</c> with <paramref name="fake"/>.  Internal targets
    /// (defined in the rewrite-target assembly) are registered with <c>WithTarget</c>; external
    /// (cross-assembly) targets with <c>WithExternalTarget</c>.  The fake is registered when the
    /// rewrite is finalized (first <see cref="CreateObject"/> / <see cref="Create{T}"/> /
    /// <see cref="Invoke{TResult}(object, string, object[])"/>).
    ///
    /// <para><b>Internal targets:</b> the fake must share the rewritten load-context identity, which a
    /// hand-made instance does not have.  For internal targets use the factory overload
    /// <see cref="ReplaceNew{T}(Func{Shims, object})"/> with <see cref="CreateFake{TTarget}"/>.</para>
    /// </summary>
    public Shims ReplaceNew<T>(T fake) where T : class
    {
        ThrowIfDisposed();
        ThrowIfFinalizedForReplaceNew();
        if (fake == null) throw new ArgumentNullException(nameof(fake));

        var type = typeof(T);
        DeclareNewTarget(type);
        _pendingReplacements.Add(s => s._harness.RegisterShim(type, fake));
        return this;
    }

    /// <summary>
    /// Easy API (Phase 23): replaces <c>new T()</c> with a fake produced by <paramref name="fakeFactory"/>
    /// at rewrite-finalization time.  Use this for <b>internal</b> targets where the fake must be created
    /// from the rewritten load context, e.g.
    /// <c>ReplaceNew&lt;UserRepository&gt;(s =&gt; s.CreateFake&lt;UserRepository&gt;("fake"))</c>.
    /// </summary>
    public Shims ReplaceNew<T>(Func<Shims, object> fakeFactory) where T : class
    {
        ThrowIfDisposed();
        ThrowIfFinalizedForReplaceNew();
        if (fakeFactory == null) throw new ArgumentNullException(nameof(fakeFactory));

        var type = typeof(T);
        DeclareNewTarget(type);
        _pendingReplacements.Add(s =>
        {
            var fake = fakeFactory(s);
            if (fake == null)
                throw new InvalidOperationException(
                    "ReplaceNew<" + (type.FullName ?? type.Name) + ">(factory): the factory returned null.");
            s._harness.RegisterShim(type, fake);
        });
        return this;
    }

    /// <summary>
    /// Easy API (Phase 23): replaces <c>new</c> of <paramref name="targetType"/> with
    /// <paramref name="fake"/>.  Internal vs external is detected from the type's assembly.
    /// </summary>
    public Shims ReplaceNew(Type targetType, object fake)
    {
        ThrowIfDisposed();
        ThrowIfFinalizedForReplaceNew();
        if (targetType == null) throw new ArgumentNullException(nameof(targetType));
        if (fake == null) throw new ArgumentNullException(nameof(fake));

        DeclareNewTarget(targetType);
        _pendingReplacements.Add(s => s._harness.RegisterShim(targetType, fake));
        return this;
    }

    /// <summary>
    /// Easy API (Phase 23): replaces <c>new</c> of the external type identified by
    /// <paramref name="externalAssemblyPath"/> + <paramref name="typeFullName"/> with
    /// <paramref name="fake"/>, without requiring a compile-time reference to the external type.
    /// </summary>
    /// <exception cref="ShimExternalTargetException">The external type could not be resolved.</exception>
    public Shims ReplaceNew(string externalAssemblyPath, string typeFullName, object fake)
    {
        ThrowIfDisposed();
        ThrowIfFinalizedForReplaceNew();
        if (string.IsNullOrWhiteSpace(externalAssemblyPath))
            throw new ArgumentException("External assembly path must be provided.", nameof(externalAssemblyPath));
        if (string.IsNullOrWhiteSpace(typeFullName))
            throw new ArgumentException("Type full name must be provided.", nameof(typeFullName));
        if (fake == null) throw new ArgumentNullException(nameof(fake));

        _harness.WithExternalTarget(externalAssemblyPath, typeFullName);
        _pendingReplacements.Add(s => s._harness.RegisterShim(typeFullName, fake));
        return this;
    }

    /// <summary>
    /// Advanced, untyped API (Phase 25): replaces calls to <paramref name="declaringType"/>.<paramref name="methodName"/>
    /// (an instance method) with <paramref name="shim"/>. For new code prefer
    /// <see cref="ReplaceMethod{TResult}(MethodInfo)"/>. For generic methods supply
    /// <paramref name="returnSubstituteInterface"/> (open generic interface, e.g. <c>typeof(IEnumerable&lt;&gt;)</c>).
    /// </summary>
    public Shims ReplaceMethod(Type declaringType, string methodName, Func<object?, object?[], object?> shim, Type? returnSubstituteInterface = null)
    {
        ThrowIfDisposed();
        ThrowIfFinalizedForReplaceNew();
        if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
        if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentException("Method name must be provided.", nameof(methodName));
        if (shim == null) throw new ArgumentNullException(nameof(shim));

        _harness.WithMethodTarget(declaringType, methodName, returnSubstituteInterface);
        _pendingReplacements.Add(s => s._harness.RegisterMethodShim(declaringType, methodName, shim));
        return this;
    }

    /// <summary>Easy API (Phase 25): instance-method shim by generic declaring type.</summary>
    public Shims ReplaceMethod<TDeclaring>(string methodName, Func<object?, object?[], object?> shim, Type? returnSubstituteInterface = null)
        => ReplaceMethod(typeof(TDeclaring), methodName, shim, returnSubstituteInterface);

    /// <summary>
    /// Easy API (Phase 25): instance-method shim for an external declaring type identified by assembly
    /// path + type full name (no compile-time reference needed).
    /// </summary>
    public Shims ReplaceMethod(string externalAssemblyPath, string typeFullName, string methodName, Func<object?, object?[], object?> shim, Type? returnSubstituteInterface = null)
    {
        ThrowIfDisposed();
        ThrowIfFinalizedForReplaceNew();
        if (string.IsNullOrWhiteSpace(externalAssemblyPath)) throw new ArgumentException("External assembly path must be provided.", nameof(externalAssemblyPath));
        if (string.IsNullOrWhiteSpace(typeFullName)) throw new ArgumentException("Type full name must be provided.", nameof(typeFullName));
        if (string.IsNullOrWhiteSpace(methodName)) throw new ArgumentException("Method name must be provided.", nameof(methodName));
        if (shim == null) throw new ArgumentNullException(nameof(shim));

        _harness.WithMethodTarget(externalAssemblyPath, typeFullName, methodName, returnSubstituteInterface);
        _pendingReplacements.Add(s => s._harness.RegisterMethodShim(typeFullName, methodName, shim));
        return this;
    }

    /// <summary>
    /// Begins a type-safe replacement for one exact, non-void instance method.
    /// The reflected return type must exactly equal <typeparamref name="TResult"/>.
    /// </summary>
    public TypedMethodReplacementBuilder<TResult> ReplaceMethod<TResult>(MethodInfo method)
    {
        ThrowIfDisposed();
        ThrowIfFinalizedForMethodReplacement();
        var descriptor = MethodReplacementValidator.ValidateTyped<TResult>(method);
        _harness.WithMethodTarget(method);
        return new TypedMethodReplacementBuilder<TResult>(this, descriptor);
    }

    /// <summary>
    /// Resolves an exact overload from <paramref name="declaringType"/> and the explicitly supplied
    /// <paramref name="parameterTypes"/>, then begins a type-safe replacement.
    /// Use <see cref="Type.EmptyTypes"/> for a zero-argument method.
    /// </summary>
    public TypedMethodReplacementBuilder<TResult> ReplaceMethod<TResult>(
        Type declaringType,
        string methodName,
        Type[] parameterTypes)
        => ReplaceMethod<TResult>(
            MethodReplacementValidator.ResolveMethod(declaringType, methodName, parameterTypes));

    /// <summary>Begins a type-safe replacement with one or more explicit parameter types.</summary>
    public TypedMethodReplacementBuilder<TResult> ReplaceMethod<TResult>(
        Type declaringType,
        string methodName,
        Type firstParameterType,
        params Type[] additionalParameterTypes)
        => ReplaceMethod<TResult>(
            declaringType,
            methodName,
            MethodReplacementValidator.CombineParameterTypes(
                firstParameterType,
                additionalParameterTypes));

    /// <summary>
    /// Resolves an exact overload on <typeparamref name="TTarget"/> and begins a type-safe replacement.
    /// Use <see cref="Type.EmptyTypes"/> for a zero-argument method.
    /// </summary>
    public TypedMethodReplacementBuilder<TResult> ReplaceMethod<TTarget, TResult>(
        string methodName,
        Type[] parameterTypes)
        => ReplaceMethod<TResult>(typeof(TTarget), methodName, parameterTypes);

    /// <summary>Begins a type-safe replacement on <typeparamref name="TTarget"/> with explicit parameter types.</summary>
    public TypedMethodReplacementBuilder<TResult> ReplaceMethod<TTarget, TResult>(
        string methodName,
        Type firstParameterType,
        params Type[] additionalParameterTypes)
        => ReplaceMethod<TResult>(
            typeof(TTarget),
            methodName,
            firstParameterType,
            additionalParameterTypes);

    /// <summary>Begins a type-safe replacement for one exact void instance method.</summary>
    public VoidMethodReplacementBuilder ReplaceVoidMethod(MethodInfo method)
    {
        ThrowIfDisposed();
        ThrowIfFinalizedForMethodReplacement();
        var descriptor = MethodReplacementValidator.ValidateVoid(method);
        _harness.WithMethodTarget(method);
        return new VoidMethodReplacementBuilder(this, descriptor);
    }

    /// <summary>
    /// Resolves an exact void overload. Use <see cref="Type.EmptyTypes"/> for a zero-argument method.
    /// </summary>
    public VoidMethodReplacementBuilder ReplaceVoidMethod(
        Type declaringType,
        string methodName,
        Type[] parameterTypes)
        => ReplaceVoidMethod(
            MethodReplacementValidator.ResolveMethod(declaringType, methodName, parameterTypes));

    /// <summary>Begins a void replacement with one or more explicit parameter types.</summary>
    public VoidMethodReplacementBuilder ReplaceVoidMethod(
        Type declaringType,
        string methodName,
        Type firstParameterType,
        params Type[] additionalParameterTypes)
        => ReplaceVoidMethod(
            declaringType,
            methodName,
            MethodReplacementValidator.CombineParameterTypes(
                firstParameterType,
                additionalParameterTypes));

    /// <summary>
    /// Resolves an exact void overload on <typeparamref name="TTarget"/>.
    /// Use <see cref="Type.EmptyTypes"/> for a zero-argument method.
    /// </summary>
    public VoidMethodReplacementBuilder ReplaceVoidMethod<TTarget>(
        string methodName,
        Type[] parameterTypes)
        => ReplaceVoidMethod(typeof(TTarget), methodName, parameterTypes);

    /// <summary>Begins a void replacement on <typeparamref name="TTarget"/> with explicit parameter types.</summary>
    public VoidMethodReplacementBuilder ReplaceVoidMethod<TTarget>(
        string methodName,
        Type firstParameterType,
        params Type[] additionalParameterTypes)
        => ReplaceVoidMethod(
            typeof(TTarget),
            methodName,
            firstParameterType,
            additionalParameterTypes);

    internal void RegisterTypedMethodReplacement(
        MethodReplacementDescriptor descriptor,
        IReadOnlyList<IShimArgumentMatcher>? matchers,
        Func<object?, object?[], object?> shim)
    {
        ThrowIfDisposed();
        ThrowIfFinalizedForMethodReplacement();
        ThrowHelper.ThrowIfNull(descriptor);
        ThrowHelper.ThrowIfNull(shim);

        if (matchers is not null)
            MethodReplacementValidator.ValidateMatchers(descriptor.Method, matchers);

        var capturedMatchers = matchers?.ToArray();
        _pendingReplacements.Add(s => s._harness.RegisterMethodShim(
            descriptor.Method,
            shim,
            capturedMatchers,
            descriptor.RegistrationSource));
    }

    private void DeclareNewTarget(Type type)
    {
        if (IsInternalTarget(type))
            _harness.WithTarget(type);
        else
            _harness.WithExternalTarget(type);
    }

    private bool IsInternalTarget(Type type)
    {
        var location = type.Assembly.Location;
        if (string.IsNullOrEmpty(location) || string.IsNullOrEmpty(_targetAssemblyPath))
            return false;

        try
        {
            return string.Equals(
                Path.GetFullPath(location),
                Path.GetFullPath(_targetAssemblyPath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Begins configuring a <c>new T()</c> shim for a target previously registered with
    /// <see cref="WithNew{TTarget}"/>.  Finalizes the rewrite on first use.
    /// </summary>
    /// <typeparam name="TTarget">The interception target type.</typeparam>
    public ShimsNewBuilder<TTarget> New<TTarget>() where TTarget : class
    {
        ThrowIfDisposed();
        EnsureFinalized();
        if (!_newTargets.Contains(typeof(TTarget)))
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                "Cannot configure a new-shim for an unregistered target.",
                "Target type: " + (typeof(TTarget).FullName ?? typeof(TTarget).Name),
                "Reason: WithNew<T>() was not called for this type before the rewrite was finalized.",
                "Hint: call Shims.For<TAnchor>().WithNew<" + typeof(TTarget).Name + ">() first."));
        }

        return new ShimsNewBuilder<TTarget>(this);
    }

    /// <summary>
    /// Begins configuring a non-void user-defined static method shim, identified by declaring
    /// <see cref="Type"/>.  Finalizes the rewrite on first use.
    /// </summary>
    public StaticShimBuilder<TResult> Static<TResult>(Type declaringType, string methodName, params Type[] parameterTypes)
    {
        ThrowIfDisposed();
        if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
        EnsureFinalized();
        return Shim.Static<TResult>(declaringType, methodName, parameterTypes);
    }

    /// <summary>
    /// Begins configuring a non-void user-defined static method shim, identified by full type name.
    /// Finalizes the rewrite on first use.
    /// </summary>
    public StaticShimBuilder<TResult> Static<TResult>(string declaringTypeFullName, string methodName, params Type[] parameterTypes)
    {
        ThrowIfDisposed();
        EnsureFinalized();
        return Shim.Static<TResult>(declaringTypeFullName, methodName, parameterTypes);
    }

    /// <summary>
    /// Begins configuring a void user-defined static method shim, identified by declaring
    /// <see cref="Type"/>.  Finalizes the rewrite on first use.
    /// </summary>
    public StaticShimBuilder Static(Type declaringType, string methodName, params Type[] parameterTypes)
    {
        ThrowIfDisposed();
        if (declaringType == null) throw new ArgumentNullException(nameof(declaringType));
        EnsureFinalized();
        return Shim.Static(declaringType, methodName, parameterTypes);
    }

    /// <summary>
    /// Begins configuring a void user-defined static method shim, identified by full type name.
    /// Finalizes the rewrite on first use.
    /// </summary>
    public StaticShimBuilder Static(string declaringTypeFullName, string methodName, params Type[] parameterTypes)
    {
        ThrowIfDisposed();
        EnsureFinalized();
        return Shim.Static(declaringTypeFullName, methodName, parameterTypes);
    }

    /// <summary>
    /// Creates a rewritten-identity instance of <typeparamref name="TTarget"/> suitable for passing
    /// to <see cref="ShimsNewBuilder{TTarget}.Returns(object)"/>.  Pass no arguments to use the
    /// parameterless constructor.
    /// </summary>
    public object CreateFake<TTarget>(params object[] constructorArgs) where TTarget : class
    {
        ThrowIfDisposed();
        EnsureFinalized();
        return _harness.CreateFake<TTarget>(constructorArgs);
    }

    /// <summary>
    /// Attempts to create a strongly-typed instance from the rewritten assembly.
    ///
    /// <para>This succeeds only when <typeparamref name="T"/> is a contract whose identity is
    /// shared across the load-context boundary — in practice an interface declared in this
    /// assembly such as <see cref="IShimCreatable"/>.  In that case the single rewritten class
    /// implementing the contract is instantiated and returned.</para>
    ///
    /// <para>For a concrete service type the rewritten instance cannot be cast to the same-named
    /// type in the default load context, so this throws <see cref="InvalidOperationException"/>
    /// and directs you to <see cref="CreateObject"/> + <see cref="Invoke{TResult}"/>.</para>
    /// </summary>
    /// <typeparam name="T">A shared contract type (interface declared in this assembly).</typeparam>
    public T Create<T>() where T : class
    {
        ThrowIfDisposed();
        EnsureFinalized();

        var requested = typeof(T);

        if (requested.IsInterface || requested.IsAbstract)
        {
            var impl = ResolveSingleImplementation(requested);
            var instance = Activator.CreateInstance(impl);
            if (instance is T typedFromContract)
            {
                return typedFromContract;
            }

            throw CreateTypeIdentityException(requested, instance?.GetType());
        }

        var rewrittenType = _harness.GetRewrittenType(requested);
        var concreteInstance = Activator.CreateInstance(rewrittenType);
        if (concreteInstance is T typed)
        {
            return typed;
        }

        throw CreateTypeIdentityException(requested, concreteInstance?.GetType());
    }

    /// <summary>
    /// Creates an instance of the rewritten type with the given full name and returns it as
    /// <see cref="object"/>.  Combine with <see cref="Invoke{TResult}"/> to drive the instance
    /// without crossing the load-context type-identity boundary.
    /// </summary>
    /// <param name="typeFullName">The full name of the type to create (e.g. <c>"My.Namespace.UserService"</c>).</param>
    public object CreateObject(string typeFullName)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(typeFullName))
            throw new ArgumentException("Type full name must be provided.", nameof(typeFullName));
        EnsureFinalized();

        return _harness.CreateObject(typeFullName);
    }

    /// <summary>
    /// Easy API (Phase 26): resolves a type from the rewritten assembly by its full name.
    ///
    /// <para>Useful inside a <see cref="ReplaceMethod(Type, string, Func{object, object[], object}, Type)"/>
    /// shim to build canned return values whose identity matches the rewritten load context (a hand-made
    /// instance of the same-named original type would not be assignable).</para>
    /// </summary>
    /// <param name="typeFullName">The full name of the rewritten type (e.g. <c>"My.Namespace.QueryData"</c>).</param>
    /// <exception cref="InvalidOperationException">The type was not found in the rewritten assembly.</exception>
    public Type GetRewrittenType(string typeFullName)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(typeFullName))
            throw new ArgumentException("Type full name must be provided.", nameof(typeFullName));

        var assembly = GetRewrittenAssembly();
        var type = assembly.GetType(typeFullName, throwOnError: false);
        if (type == null)
            throw new InvalidOperationException(
                "GetRewrittenType: type '" + typeFullName + "' was not found in the rewritten assembly '" +
                assembly.GetName().Name + "'.");
        return type;
    }

    /// <summary>
    /// Easy API (Phase 26): creates an instance of the rewritten type named <paramref name="typeFullName"/>
    /// and assigns members from <paramref name="properties"/> by name.
    ///
    /// <para><paramref name="properties"/> is typically an anonymous object, e.g.
    /// <c>new { Name = "x", Age = 30 }</c>; each of its properties is matched to a public writable property
    /// or field of the same name on the target type, converting the value where necessary.  Pass
    /// <see langword="null"/> to leave all members at their defaults.</para>
    /// </summary>
    public object NewObject(string typeFullName, object? properties = null)
    {
        var type = GetRewrittenType(typeFullName);
        var instance = Activator.CreateInstance(type)!;
        if (properties != null)
            ApplyMembers(type, instance, properties);
        return instance;
    }

    /// <summary>
    /// Easy API (Phase 26): creates a <c>List&lt;T&gt;</c> of the rewritten type named
    /// <paramref name="typeFullName"/>, one element per entry in <paramref name="rows"/>.
    ///
    /// <para>Each row is a property bag (typically an anonymous object) whose members are assigned by name,
    /// e.g. <c>shims.NewList("My.QueryData", new { 車名 = "A" }, new { 車名 = "B" })</c>.  The returned list
    /// is typed as <c>List&lt;rewritten T&gt;</c>, so it satisfies an <c>IEnumerable&lt;T&gt;</c> return
    /// substitution in a <see cref="ReplaceMethod(Type, string, Func{object, object[], object}, Type)"/>
    /// shim (e.g. for <c>context.Database.SqlQuery&lt;T&gt;(sql).ToList()</c>).</para>
    /// </summary>
    public object NewList(string typeFullName, params object[] rows)
    {
        var type = GetRewrittenType(typeFullName);
        var listType = typeof(List<>).MakeGenericType(type);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        if (rows != null)
        {
            foreach (var row in rows)
            {
                var element = Activator.CreateInstance(type)!;
                if (row != null)
                    ApplyMembers(type, element, row);
                list.Add(element);
            }
        }
        return list;
    }

    private static void ApplyMembers(Type targetType, object target, object propertyBag)
    {
        foreach (var src in propertyBag.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = src.GetValue(propertyBag);
            var name = src.Name;

            var destProp = targetType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (destProp != null && destProp.CanWrite)
            {
                destProp.SetValue(target, ConvertMemberValue(value, destProp.PropertyType, targetType, name));
                continue;
            }

            var destField = targetType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (destField != null)
            {
                destField.SetValue(target, ConvertMemberValue(value, destField.FieldType, targetType, name));
                continue;
            }

            throw new InvalidOperationException(
                "NewObject/NewList: the rewritten type '" + (targetType.FullName ?? targetType.Name) +
                "' has no writable public property or field named '" + name + "'.");
        }
    }

    private static object? ConvertMemberValue(object? value, Type targetMemberType, Type targetType, string memberName)
    {
        if (value == null)
            return null;

        var valueType = value.GetType();
        if (targetMemberType.IsAssignableFrom(valueType))
            return value;

        var underlying = Nullable.GetUnderlyingType(targetMemberType) ?? targetMemberType;
        try
        {
            if (underlying.IsEnum)
                return Enum.ToObject(underlying, value);
            return Convert.ChangeType(value, underlying);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            throw new InvalidOperationException(
                "NewObject/NewList: cannot assign a value of type '" + (valueType.FullName ?? valueType.Name) +
                "' to member '" + memberName + "' of type '" + (targetMemberType.FullName ?? targetMemberType.Name) +
                "' on '" + (targetType.FullName ?? targetType.Name) + "'.", ex);
        }
    }

    /// <summary>
    /// Invokes a public instance method on <paramref name="instance"/> via reflection and returns
    /// the result cast to <typeparamref name="TResult"/>.
    /// </summary>
    public TResult Invoke<TResult>(object instance, string methodName, params object[] args)
    {
        ThrowIfDisposed();
        return _harness.Invoke<TResult>(instance, methodName, args);
    }

    /// <summary>
    /// Invokes a public instance method on <paramref name="instance"/> via reflection, ignoring
    /// any return value.
    /// </summary>
    public void Invoke(object instance, string methodName, params object[] args)
    {
        ThrowIfDisposed();
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        if (string.IsNullOrWhiteSpace(methodName))
            throw new ArgumentException("Method name must be provided.", nameof(methodName));

        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        if (method == null)
        {
            throw new InvalidOperationException(
                "Public instance method '" + methodName + "' was not found on " +
                (instance.GetType().FullName ?? instance.GetType().Name) + ".");
        }

        try
        {
            method.Invoke(instance, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is ShimException)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }

    /// <summary>
    /// Phase 24 inspection: wraps <paramref name="instance"/> in a <see cref="ShimsObject"/> so its
    /// (possibly rewritten) object graph can be observed by property path without casting it to the
    /// test's original type.
    /// </summary>
    public ShimsObject Inspect(object instance)
    {
        ThrowIfDisposed();
        return new ShimsObject(instance);
    }

    /// <summary>
    /// Phase 24 inspection: evaluates <paramref name="path"/> on <paramref name="instance"/> and
    /// returns the raw value (may be null at the leaf).  Supports property/field access and integer
    /// indexers, e.g. <c>"Items.Count"</c>, <c>"Items[0].Name"</c>, <c>"SelectedUser.Name"</c>.
    /// </summary>
    public object GetValue(object instance, string path) => Inspect(instance).GetValue(path);

    /// <summary>
    /// Phase 24 inspection: evaluates <paramref name="path"/> and converts the value to
    /// <typeparamref name="T"/>.  Use this for primitive / string / enum / value-type leaves;
    /// rewritten reference types are never force-cast to a same-named original type.
    /// </summary>
    public T GetValue<T>(object instance, string path) => Inspect(instance).GetValue<T>(path);

    /// <summary>Phase 24 inspection: reads a single public property or field by name.</summary>
    public object GetProperty(object instance, string propertyName) => Inspect(instance).GetProperty(propertyName);

    /// <summary>Phase 24 inspection: reads a single public property or field and converts it to <typeparamref name="T"/>.</summary>
    public T GetProperty<T>(object instance, string propertyName) => Inspect(instance).GetProperty<T>(propertyName);

    /// <summary>
    /// Phase 24 inspection: evaluates <paramref name="path"/> and returns the collection as a
    /// <see cref="ShimsCollection"/> whose elements are exposed as <see cref="ShimsObject"/> wrappers.
    /// Works even when the element type is a rewritten type (e.g. <c>ObservableCollection&lt;T&gt;</c>).
    /// </summary>
    public ShimsCollection GetCollection(object instance, string path) => Inspect(instance).GetCollection(path);

    /// <summary>
    /// Returns a diagnostics snapshot of the isolated load context.
    /// </summary>
    public ShimAlcDiagnostics GetAlcDiagnostics()
    {
        ThrowIfDisposed();
        EnsureFinalized();
        return _harness.GetAlcDiagnostics();
    }

    /// <summary>
    /// Gets the harness-level diagnostics log (Phase 21/23): external target resolution, registration,
    /// the target assembly being rewritten, registry keys, duplicate FullName risk, etc.  Forwarded
    /// from the underlying <see cref="NewInterceptionHarness.Diagnostics"/>.
    /// </summary>
    public IReadOnlyList<string> Diagnostics => _harness.Diagnostics;

    /// <summary>
    /// Gets the diagnostics captured by the most recent <c>newobj</c> dispatch in this session,
    /// or <see langword="null"/> if no dispatch has occurred.
    /// </summary>
    public ShimDispatchDiagnostics? LastNewDispatchDiagnostics => _context?.LastDispatchDiagnostics;

    /// <summary>
    /// Alias of <see cref="LastNewDispatchDiagnostics"/> (Phase 23 diagnostics forwarding).
    /// </summary>
    public ShimDispatchDiagnostics? LastDispatchDiagnostics => _context?.LastDispatchDiagnostics;

    /// <summary>
    /// Gets the diagnostics captured by the most recent static-method dispatch in this session,
    /// or <see langword="null"/> if no dispatch has occurred.
    /// </summary>
    public StaticDispatchDiagnostics? LastStaticDispatchDiagnostics => _context?.LastStaticDispatchDiagnostics;

    /// <summary>
    /// Gets exact signature, rule selection, return validation, and fallback diagnostics for the
    /// most recent rewritten instance-method dispatch in this session.
    /// </summary>
    public MethodDispatchDiagnostics? LastMethodDispatchDiagnostics =>
        _context?.LastMethodDispatchDiagnostics;

    /// <summary>
    /// Gets the underlying low-level harness.  Exposed for advanced scenarios; most callers should
    /// use the facade methods instead.
    /// </summary>
    internal NewInterceptionHarness Harness => _harness;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _context?.Dispose();
        }
        finally
        {
            _context = null;
            _harness.Dispose();
        }
    }

    private void EnsureFinalized()
    {
        if (_finalized)
            return;

        _harness.RewriteAssembly(_targetAssemblyPath);
        _context = ShimContext.Create();
        _finalized = true;

        // Apply all deferred ReplaceNew(...) registrations now that the rewrite + context exist.
        // _finalized is already true so any CreateFake(...) inside a factory will not re-enter here.
        foreach (var registration in _pendingReplacements)
            registration(this);
        _pendingReplacements.Clear();
    }

    private Assembly GetRewrittenAssembly()
    {
        EnsureFinalized();
        return _harness.LoadedAssembly
            ?? throw new InvalidOperationException("The rewritten assembly has not been loaded.");
    }

    private Type ResolveSingleImplementation(Type contract)
    {
        var assembly = GetRewrittenAssembly();
        var candidates = SafeGetTypes(assembly)
            .Where(t => t != null
                        && t.IsClass
                        && !t.IsAbstract
                        && contract.IsAssignableFrom(t))
            .ToList();

        if (candidates.Count == 1)
        {
            return candidates[0]!;
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                "Create<T>() could not find a rewritten class implementing the contract.",
                "Contract: " + (contract.FullName ?? contract.Name),
                "Rewritten assembly: " + assembly.GetName().Name,
                "Reason: no public, concrete class in the rewritten assembly implements the contract.",
                "Hint: implement the contract on a class in the rewritten assembly, or use",
                "      CreateObject(typeFullName) + Invoke(...)."));
        }

        throw new InvalidOperationException(string.Join(
            Environment.NewLine,
            "Create<T>() found more than one rewritten class implementing the contract.",
            "Contract: " + (contract.FullName ?? contract.Name),
            "Candidates: " + string.Join(", ", candidates.Select(t => t!.FullName)),
            "Reason: the contract maps to multiple concrete types, so the target is ambiguous.",
            "Hint: use CreateObject(typeFullName) + Invoke(...) to pick the exact type."));
    }

    private static IEnumerable<Type?> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null);
        }
    }

    private InvalidOperationException CreateTypeIdentityException(Type requested, Type? actualType)
    {
        return new InvalidOperationException(string.Join(
            Environment.NewLine,
            "Create<T>() cannot safely return a strongly-typed instance for this type.",
            "Requested type : " + (requested.FullName ?? requested.Name),
            "Rewritten type : " + (actualType?.FullName ?? "<unknown>") +
                " (loaded in an isolated load context)",
            "Reason         : the rewritten type has a different assembly / load-context identity",
            "                 than the requested type, so the cast would throw InvalidCastException.",
            "Use instead    :",
            "  var obj = shims.CreateObject(typeof(" + (requested.Name) + ").FullName);",
            "  var result = shims.Invoke<TResult>(obj, \"MethodName\", args);",
            "Hint           : Create<T>() only returns strongly-typed for a shared contract",
            "                 (an interface declared in MiniMockito.Shims.Experimental, e.g. IShimCreatable)."));
    }

    private void ThrowIfFinalized(string operation)
    {
        if (_finalized)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                operation + "() cannot be called after the rewrite has been finalized.",
                "Reason: the target assembly is rewritten and loaded on the first New/Static/Create/",
                "        CreateObject/Invoke/CreateFake call; targets are locked in at that point.",
                "Hint: declare all WithNew<T>() / WithStatic(...) targets before using the session."));
        }
    }

    private void ThrowIfFinalizedForReplaceNew()
    {
        if (_finalized)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                "ReplaceNew(...) failed: rewrite already completed.",
                "target cannot be added after rewrite.",
                "Reason: the target assembly is rewritten and loaded on the first",
                "        CreateObject/Create/Invoke call; new targets are locked in at that point.",
                "Hint: create a new Shims session (Shims.ForAssembly(...) or Shims.For<T>()) and declare",
                "      every ReplaceNew(...) before the first CreateObject/Create/Invoke."));
        }
    }

    private void ThrowIfFinalizedForMethodReplacement()
    {
        if (_finalized)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                "ReplaceMethod/ReplaceVoidMethod failed: rewrite already completed.",
                "Reason: exact method targets are locked when the target assembly is rewritten.",
                "Hint: register every method replacement before the first CreateObject/Create/Invoke call."));
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Shims));
    }
}
