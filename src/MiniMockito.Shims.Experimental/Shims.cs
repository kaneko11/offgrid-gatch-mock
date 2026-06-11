using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
    private readonly Type _anchorType;
    private readonly NewInterceptionHarness _harness;
    private readonly List<Type> _newTargets = new List<Type>();
    private readonly List<Type> _staticTargets = new List<Type>();

    private ShimContext? _context;
    private bool _finalized;
    private bool _disposed;

    private Shims(Type anchorType)
    {
        _anchorType = anchorType;
        _harness = NewInterceptionHarness.Create();
    }

    /// <summary>
    /// Starts a new shim session whose interception applies to the assembly that defines
    /// <typeparamref name="TAnchor"/>.  <typeparamref name="TAnchor"/> is typically the service
    /// type whose method bodies contain the <c>new</c> / static call sites to intercept.
    /// </summary>
    /// <typeparam name="TAnchor">A concrete, public type in the assembly to rewrite.</typeparam>
    public static Shims For<TAnchor>() where TAnchor : class
        => new Shims(typeof(TAnchor));

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

        var assembly = GetRewrittenAssembly();
        var type = assembly.GetType(typeFullName, throwOnError: true)!;
        var instance = Activator.CreateInstance(type);
        if (instance == null)
        {
            throw new InvalidOperationException(
                "Activator.CreateInstance returned null for " + typeFullName + ".");
        }

        return instance;
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

        method.Invoke(instance, args);
    }

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
    /// Gets the diagnostics captured by the most recent <c>newobj</c> dispatch in this session,
    /// or <see langword="null"/> if no dispatch has occurred.
    /// </summary>
    public ShimDispatchDiagnostics? LastNewDispatchDiagnostics => _context?.LastDispatchDiagnostics;

    /// <summary>
    /// Gets the diagnostics captured by the most recent static-method dispatch in this session,
    /// or <see langword="null"/> if no dispatch has occurred.
    /// </summary>
    public StaticDispatchDiagnostics? LastStaticDispatchDiagnostics => _context?.LastStaticDispatchDiagnostics;

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

        _harness.RewriteAssembly(_anchorType.Assembly.Location);
        _context = ShimContext.Create();
        _finalized = true;
    }

    private Assembly GetRewrittenAssembly()
        => _harness.GetRewrittenType(_anchorType).Assembly;

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

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Shims));
    }
}
