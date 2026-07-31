using System.Reflection;

using System.Runtime.ExceptionServices;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Test helper that combines assembly rewrite, loading into an isolated
/// <see cref="ShimAssemblyLoadContext"/>, and reflection-based instance creation
/// for new-interception experiments.
///
/// <para>Typical usage:</para>
/// <code>
/// using var harness = NewInterceptionHarness.Create()
///     .WithTarget&lt;UserRepository&gt;()
///     .RewriteTargetTypeAssembly();
///
/// using (ShimContext.Create())
/// {
///     var fake = harness.CreateFake&lt;UserRepository&gt;("fake-prefix");
///     harness.RegisterShim&lt;UserRepository&gt;(fake);
///
///     var service = harness.Create&lt;UserService&gt;();
///     var result = harness.Invoke&lt;string&gt;(service, "GetDisplayName", 42);
/// }
/// </code>
///
/// <para><b>Type identity constraint:</b> instances returned by <see cref="Create{TService}"/> and
/// <see cref="CreateFake{TTarget}"/> are from the isolated ALC.
/// Their runtime types differ from the original types even though they share the same full name.
/// Use <see cref="Invoke{TResult}"/> or reflection to call methods on them.</para>
///
/// <para><b>Parallelism:</b> do not run harness tests in parallel.  The shim dispatcher
/// uses process-wide state; concurrent test runs will interfere with each other.</para>
///
/// <para><b>Unload:</b> call <see cref="Dispose"/> to start ALC unload.  Use
/// <see cref="GetUnloadReference"/> before dispose to obtain a <see cref="WeakReference"/>
/// for unload verification after GC.</para>
/// </summary>
public sealed class NewInterceptionHarness : IDisposable
{
    private readonly List<Type> _targetTypes = [];
    private readonly List<ExternalNewTarget> _externalTargets = [];
    private readonly List<Type> _staticTargetTypes = [];
    private readonly List<MethodTargetEntry> _methodTargets = [];
    private readonly List<string> _diagnostics = [];
    private readonly Dictionary<string, HashSet<string>> _externalRegistryKeys = new(StringComparer.Ordinal);
    private RewrittenAssemblyLoader? _loader;
    private Assembly? _assembly;
    private bool _disposed;

    private NewInterceptionHarness()
    {
    }

    /// <summary>
    /// Gets the result of the last <see cref="RewriteTargetTypeAssembly"/> or
    /// <see cref="RewriteAssembly"/> call, or <see langword="null"/> if no rewrite has run.
    /// </summary>
    public RewriteResult? LastRewriteResult { get; private set; }

    /// <summary>
    /// Gets the path of the rewritten output assembly, or <see langword="null"/> before a rewrite.
    /// </summary>
    public string? OutputAssemblyPath { get; private set; }

    /// <summary>
    /// Gets the harness-level diagnostics log (Phase 21).  Records external target resolution,
    /// registration, the target assembly being rewritten, registry keys used, duplicate FullName
    /// risk, and external fake creation outcomes.  Rewrite-time IL diagnostics (newobj detected /
    /// rewritten / skipped) are available on <see cref="LastRewriteResult"/>.
    /// </summary>
    public IReadOnlyList<string> Diagnostics => _diagnostics;

    /// <summary>Creates a new harness builder.</summary>
    public static NewInterceptionHarness Create() => new();

    /// <summary>
    /// Gets the rewritten assembly loaded into the isolated load context, or <see langword="null"/>
    /// before a rewrite has run.  Used by the high-level <see cref="Shims"/> facade.
    /// </summary>
    internal Assembly? LoadedAssembly => _assembly;

    /// <summary>Adds <typeparamref name="T"/> to the allowlist of <c>newobj</c> target types to rewrite.</summary>
    public NewInterceptionHarness WithTarget<T>() where T : class
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        _targetTypes.Add(typeof(T));
        return this;
    }

    /// <summary>Adds <paramref name="targetType"/> to the allowlist of <c>newobj</c> target types to rewrite.</summary>
    public NewInterceptionHarness WithTarget(Type targetType)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNull(targetType);
        _targetTypes.Add(targetType);
        return this;
    }

    /// <summary>
    /// Adds <typeparamref name="TExternal"/> — a type defined in an assembly <b>other</b> than the one
    /// being rewritten — to the allowlist of cross-assembly <c>newobj</c> target types.
    /// Register a replacement with <see cref="RegisterShim{TTarget}"/> or
    /// <see cref="RegisterShim(Type, object)"/>; the recommended approach for external types is to
    /// supply a manually constructed fake instance (e.g. a hand-written subclass or a class mock).
    /// </summary>
    public NewInterceptionHarness WithExternalTarget<TExternal>() where TExternal : class
        => WithExternalTarget(typeof(TExternal));

    /// <summary>
    /// Adds <paramref name="externalType"/> — a type defined in an assembly <b>other</b> than the one
    /// being rewritten — to the allowlist of cross-assembly <c>newobj</c> target types.
    /// </summary>
    public NewInterceptionHarness WithExternalTarget(Type externalType)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNull(externalType);
        AddExternalTarget(new ExternalNewTarget(externalType));
        return this;
    }

    /// <summary>
    /// Adds an external (cross-assembly) <c>newobj</c> target identified by an assembly file path and
    /// a type full name, <b>without</b> requiring a compile-time reference to the external type
    /// (Phase 21).  The type is resolved via <see cref="ResolveExternalType(string, string)"/>.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly file that defines the external type.</param>
    /// <param name="typeFullName">The full name of the external type (e.g. <c>"ExternalLib.ExternalDbContext"</c>).</param>
    /// <exception cref="ShimExternalTargetException">
    /// The assembly file does not exist, or the type full name was not found in the loaded assembly.
    /// </exception>
    public NewInterceptionHarness WithExternalTarget(string assemblyPath, string typeFullName)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNullOrWhiteSpace(assemblyPath);
        ThrowHelper.ThrowIfNullOrWhiteSpace(typeFullName);

        var resolved = ResolveExternalType(assemblyPath, typeFullName);
        AddExternalTarget(new ExternalNewTarget(resolved));
        return this;
    }

    /// <summary>
    /// Resolves an external type from an assembly file path and a type full name (Phase 21).
    /// The assembly is loaded into the default load context so the resolved type shares its runtime
    /// identity across the rewrite boundary.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly file that defines the external type.</param>
    /// <param name="typeFullName">The full name of the external type.</param>
    /// <returns>The resolved <see cref="Type"/>.</returns>
    /// <exception cref="ShimExternalTargetException">
    /// The assembly file does not exist, could not be loaded, or does not contain the requested type.
    /// </exception>
    public Type ResolveExternalType(string assemblyPath, string typeFullName)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(assemblyPath);
        ThrowHelper.ThrowIfNullOrWhiteSpace(typeFullName);

        var fullPath = Path.GetFullPath(assemblyPath);
        Diag($"External assembly path: {fullPath}");
        Diag($"External type full name: {typeFullName}");

        if (!File.Exists(fullPath))
        {
            Diag($"Type resolution: failure — external assembly file not found: {fullPath}");
            throw new ShimExternalTargetException(string.Join(
                Environment.NewLine,
                "External target assembly file was not found.",
                $"External assembly path: {fullPath}",
                $"External type full name: {typeFullName}",
                $"Searched path: {fullPath}",
                "Reason: ExternalAssemblyFileNotFound",
                "Hint: pass the path to the compiled external assembly (e.g. \"...\\ExternalLib.dll\")."));
        }

        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(fullPath);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or System.Security.SecurityException)
        {
            Diag($"Type resolution: failure — external assembly could not be loaded: {fullPath}");
            throw new ShimExternalTargetException(string.Join(
                Environment.NewLine,
                "External target assembly could not be loaded.",
                $"External assembly path: {fullPath}",
                $"External type full name: {typeFullName}",
                "Reason: ExternalAssemblyLoadFailed",
                $"Hint: ensure the file is a valid managed assembly. ({ex.GetType().Name}: {ex.Message})"),
                ex);
        }

        var simpleName = assembly.GetName().Name ?? "<unknown>";
        Diag($"Candidate assembly loaded: {simpleName} from {fullPath}");

        var type = assembly.GetType(typeFullName, throwOnError: false, ignoreCase: false);
        if (type is null)
        {
            Diag($"Type resolution: failure — type '{typeFullName}' not found in assembly {simpleName}");
            throw new ShimExternalTargetException(string.Join(
                Environment.NewLine,
                "External target type was not found in the loaded assembly.",
                $"External assembly path: {fullPath}",
                $"Candidate assembly: {simpleName}",
                $"External type full name: {typeFullName}",
                "Reason: ExternalTypeNotFound",
                "Hint: check the namespace-qualified full name (case-sensitive) and the assembly path."));
        }

        Diag($"Type resolution: success — {type.FullName} from {simpleName}");
        return type;
    }

    /// <summary>
    /// Adds <paramref name="staticTargetType"/> to the allowlist of static call target types to rewrite.
    /// All non-BCL, non-generic static methods on this type whose call sites appear in the target assembly
    /// will be replaced with wrapper methods that delegate to <see cref="StaticShimDispatcher"/>.
    /// </summary>
    public NewInterceptionHarness WithStaticTarget(Type staticTargetType)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNull(staticTargetType);
        _staticTargetTypes.Add(staticTargetType);
        return this;
    }

    /// <summary>
    /// Adds an instance-method call-site target (Phase 25): call sites to
    /// <typeparamref name="TDeclaring"/>.<paramref name="methodName"/> inside the rewritten assembly
    /// are redirected to a method shim.  For generic methods, supply <paramref name="returnSubstituteInterface"/>
    /// (an open generic interface such as <c>typeof(IEnumerable&lt;&gt;)</c>) used as the wrapper return type.
    /// </summary>
    public NewInterceptionHarness WithMethodTarget<TDeclaring>(string methodName, Type? returnSubstituteInterface = null)
        => WithMethodTarget(typeof(TDeclaring), methodName, returnSubstituteInterface);

    /// <summary>Adds an instance-method call-site target by <see cref="Type"/> (Phase 25).</summary>
    public NewInterceptionHarness WithMethodTarget(Type declaringType, string methodName, Type? returnSubstituteInterface = null)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNull(declaringType);
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodName);

        var fullName = declaringType.FullName
            ?? throw new InvalidOperationException($"Type {declaringType.Name} has no FullName.");
        _methodTargets.Add(new MethodTargetEntry(
            fullName, methodName, returnSubstituteInterface, declaringType.Assembly.GetName().Name ?? string.Empty));
        Diag($"Method target registered: {fullName}::{methodName}.");
        return this;
    }

    /// <summary>
    /// Adds one exact, overload-safe instance-method call-site target resolved from
    /// <paramref name="method"/>. This is the preferred method-target declaration.
    /// </summary>
    public NewInterceptionHarness WithMethodTarget(MethodInfo method)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        var descriptor = MethodReplacementValidator.ValidateInstanceMethod(method, "typed API");
        var declaringType = descriptor.Method.DeclaringType!;
        var fullName = declaringType.FullName!.Replace('+', '/');
        var parameterTypeNames = descriptor.Method
            .GetParameters()
            .Select(parameter => MethodSignatureFormatter.FormatType(parameter.ParameterType))
            .ToArray();

        _methodTargets.Add(new MethodTargetEntry(
            fullName,
            descriptor.Method.Name,
            returnSubstituteInterface: null,
            declaringType.Assembly.GetName().Name ?? string.Empty,
            parameterTypeNames,
            descriptor.RegistryKey,
            descriptor.Signature,
            MethodSignatureFormatter.FormatType(descriptor.Method.ReturnType),
            descriptor.Method.IsVirtual,
            descriptor.RegistrationSource));

        Diag("Method target registered:");
        Diag("  Target type: " + fullName);
        Diag("  Exact MethodInfo signature: " + descriptor.Signature);
        Diag("  Return type: " + MethodSignatureFormatter.FormatType(descriptor.Method.ReturnType));
        Diag("  Parameter types: " + MethodSignatureFormatter.FormatRequestedParameterTypes(
            descriptor.Method.GetParameters().Select(parameter => parameter.ParameterType)));
        Diag("  Instance / static: instance");
        Diag("  Virtual / non-virtual: " + (descriptor.Method.IsVirtual ? "virtual" : "non-virtual"));
        Diag(
            "  Final: " + descriptor.Method.IsFinal +
            (descriptor.Method.IsVirtual && descriptor.Method.IsFinal
                ? " (override is unavailable; call-site rewrite remains selected)"
                : string.Empty));
        Diag("  Selected backend: " + descriptor.Backend);
        Diag("  Registration source: " + descriptor.RegistrationSource);
        return this;
    }

    /// <summary>
    /// Adds an instance-method call-site target by external assembly path + type full name (Phase 25),
    /// without a compile-time reference to the declaring type.
    /// </summary>
    public NewInterceptionHarness WithMethodTarget(string assemblyPath, string typeFullName, string methodName, Type? returnSubstituteInterface = null)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        var resolved = ResolveExternalType(assemblyPath, typeFullName);
        return WithMethodTarget(resolved, methodName, returnSubstituteInterface);
    }

    /// <summary>
    /// Rewrites the assembly that contains the first registered target type (or the first static
    /// target type if no newobj targets are registered) and loads the output into an isolated
    /// <see cref="ShimAssemblyLoadContext"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No target types have been registered.</exception>
    public NewInterceptionHarness RewriteTargetTypeAssembly()
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);

        if (_targetTypes.Count == 0 && _staticTargetTypes.Count == 0)
        {
            throw new InvalidOperationException(
                "No target types registered. " +
                "Call WithTarget<T>() or WithStaticTarget(Type) before RewriteTargetTypeAssembly().");
        }

        var assemblyType = _targetTypes.Count > 0 ? _targetTypes[0] : _staticTargetTypes[0];
        return RewriteAssembly(assemblyType.Assembly.Location);
    }

    /// <summary>
    /// Rewrites the specified assembly using the registered target types and loads the output
    /// into an isolated <see cref="ShimAssemblyLoadContext"/>.
    /// </summary>
    /// <param name="inputAssemblyPath">The path to the assembly to rewrite.</param>
    public NewInterceptionHarness RewriteAssembly(string inputAssemblyPath)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNullOrWhiteSpace(inputAssemblyPath);

        var outputPath = CreateOutputPath(inputAssemblyPath);
        OutputAssemblyPath = outputPath;

        Diag($"Target assembly being rewritten: {Path.GetFullPath(inputAssemblyPath)}");

        LastRewriteResult = AssemblyRewriter.RewriteNewObj(
            inputAssemblyPath,
            outputPath,
            new RewriteOptions
            {
                TargetTypes = _targetTypes.ToArray(),
                ExternalTargetTypes = _externalTargets.Select(t => t.OriginalType).ToArray(),
                StaticTargetTypes = _staticTargetTypes.ToArray(),
                MethodTargets = _methodTargets
                    .Select(e => new MethodShimTarget(
                        e.DeclaringTypeFullName,
                        e.MethodName,
                        e.ReturnSubstituteInterface,
                        e.AssemblySimpleName,
                        e.ParameterTypeNames,
                        e.RegistryKey,
                        e.MethodSignature,
                        e.ReturnTypeName,
                        e.IsVirtual,
                        e.RegistrationSource))
                    .ToArray(),
            });

        // Pass the original assembly directory so the ALC can probe for dependencies
        // that are not in the temp output directory.
        var originalDir = Path.GetDirectoryName(inputAssemblyPath);

        // External target assemblies (and method-shim declaring assemblies that are NOT the rewritten
        // assembly) must be shared from the parent ALC so those types keep a single runtime identity
        // across the rewrite boundary — required so test-supplied fakes / canned data are assignable.
        var targetSimpleName = Path.GetFileNameWithoutExtension(inputAssemblyPath);
        var sharedAssemblyNames = _externalTargets
            .Select(t => t.AssemblySimpleName)
            .Concat(_methodTargets
                .Select(e => e.AssemblySimpleName)
                .Where(name => !string.Equals(name, targetSimpleName, StringComparison.OrdinalIgnoreCase)))
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _loader?.Dispose();
        _loader = new RewrittenAssemblyLoader(outputPath, originalDir, sharedAssemblyNames);
        _assembly = _loader.Load();
        return this;
    }

    /// <summary>
    /// Creates an instance of <typeparamref name="TService"/> from the rewritten assembly
    /// using a public parameterless constructor.
    /// </summary>
    public object Create<TService>() where TService : class
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        EnsureRewritten();
        var type = GetRewrittenType(typeof(TService));
        return CreateInstanceUnwrapped(type)
            ?? throw new InvalidOperationException(
                $"Activator.CreateInstance returned null for {typeof(TService).FullName}.");
    }

    /// <summary>
    /// Creates an instance of the rewritten type named <paramref name="typeName"/> using its public
    /// parameterless constructor.  Use this for service types whose compile-time reference is not
    /// available to the test (e.g. the cross-assembly sample's caller type).
    /// </summary>
    /// <param name="typeName">The full type name as defined in the rewritten assembly.</param>
    public object CreateObject(string typeName)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNullOrWhiteSpace(typeName);
        EnsureRewritten();

        var type = _assembly!.GetType(typeName, throwOnError: true)!;
        return CreateInstanceUnwrapped(type)
            ?? throw new InvalidOperationException(
                $"Activator.CreateInstance returned null for {typeName}.");
    }

    /// <summary>
    /// Creates an instance of <typeparamref name="TTarget"/> from the rewritten assembly
    /// using the specified constructor arguments.  Pass no arguments to use the parameterless constructor.
    /// </summary>
    /// <remarks>
    /// <b>External targets:</b> <see cref="CreateFake{TTarget}"/> does not support cross-assembly
    /// external targets registered with <see cref="WithExternalTarget{TExternal}"/>.  A rewritten
    /// counterpart of an external type does not exist inside the rewritten assembly, and synthesizing
    /// behaviour-overriding fakes is out of scope for this phase.  Construct the fake yourself
    /// (a hand-written subclass or a class mock) and pass it to <see cref="RegisterShim{TTarget}"/>.
    /// </remarks>
    public object CreateFake<TTarget>(params object[] constructorArgs) where TTarget : class
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        EnsureRewritten();

        if (TryGetExternalTarget(typeof(TTarget), out var external))
        {
            throw new NotSupportedException(string.Join(
                Environment.NewLine,
                "CreateFake<T>() does not support external (cross-assembly) targets.",
                $"Target type: {external.TypeFullName}",
                $"Calling assembly: {external.AssemblySimpleName}",
                "Rewrite mode: CrossAssemblyNewObj",
                "Reason: ExternalTargetFakeNotSupported",
                "Supported patterns:",
                "  manually constructed fake registered via RegisterShim<T>(fake)",
                "  manually constructed fake registered via RegisterShim(Type, fake)",
                "Unsupported patterns:",
                "  CreateFake<T>() for a type defined in another assembly",
                "Hint: Create the fake yourself (a hand-written subclass or Mock.Class<T>()) " +
                "and call RegisterShim<T>(fake)."));
        }

        var type = GetRewrittenType(typeof(TTarget));
        return (constructorArgs.Length == 0
                ? Activator.CreateInstance(type)
                : Activator.CreateInstance(type, constructorArgs))
            ?? throw new InvalidOperationException(
                $"Activator.CreateInstance returned null for {typeof(TTarget).FullName}.");
    }

    /// <summary>
    /// Creates a plain instance of an external (cross-assembly) type for use as a shim fake (Phase 21).
    /// Only <b>public, non-sealed, non-abstract</b> classes are supported; with no
    /// <paramref name="args"/> a public parameterless constructor is required.  No proxy / behaviour
    /// override is generated — for behaviour-overriding fakes construct a subclass or a class mock
    /// yourself and register it via <see cref="RegisterShim(string, object)"/>.
    /// </summary>
    /// <param name="targetType">The external type to instantiate.</param>
    /// <param name="args">Optional constructor arguments.</param>
    /// <exception cref="NotSupportedException">The type is not supported (sealed, abstract, non-public, or no matching public constructor).</exception>
    public object CreateFakeExternal(Type targetType, params object[] args)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNull(targetType);

        var unsupportedReason = GetExternalFakeUnsupportedReason(targetType, args);
        if (unsupportedReason is not null)
        {
            Diag($"External type fake creation unsupported: {targetType.FullName} — {unsupportedReason}");
            throw new NotSupportedException(string.Join(
                Environment.NewLine,
                "CreateFakeExternal cannot create an instance of this external type.",
                $"Target type: {targetType.FullName}",
                $"Calling assembly: {targetType.Assembly.GetName().Name}",
                "Rewrite mode: CrossAssemblyNewObj",
                $"Reason: {unsupportedReason}",
                "Supported patterns:",
                "  public, non-sealed, non-abstract class",
                "  public parameterless constructor (when no args are supplied)",
                "Unsupported patterns:",
                "  sealed / abstract / non-public types",
                "  no matching public constructor",
                "Hint: construct the fake yourself (a hand-written subclass or Mock.Class<T>()) " +
                "and call RegisterShim(typeFullName, fake)."));
        }

        var instance = (args.Length == 0
                ? Activator.CreateInstance(targetType)
                : Activator.CreateInstance(targetType, args))
            ?? throw new InvalidOperationException(
                $"Activator.CreateInstance returned null for {targetType.FullName}.");

        Diag($"External type fake creation supported: {targetType.FullName}");
        return instance;
    }

    /// <summary>
    /// Creates a plain instance of an external type identified by <paramref name="typeFullName"/>
    /// (Phase 21).  The type must have been registered with
    /// <see cref="WithExternalTarget(string, string)"/> or another <c>WithExternalTarget</c> overload.
    /// </summary>
    /// <param name="typeFullName">The external type's full name.</param>
    /// <param name="args">Optional constructor arguments.</param>
    /// <exception cref="InvalidOperationException">The type full name is not a registered external target.</exception>
    /// <exception cref="NotSupportedException">The type is not supported for fake creation.</exception>
    public object CreateFakeExternal(string typeFullName, params object[] args)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNullOrWhiteSpace(typeFullName);

        if (!TryGetExternalTargetByFullName(typeFullName, out var target))
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                "CreateFakeExternal could not resolve the external type by full name.",
                $"External type full name: {typeFullName}",
                "Reason: ExternalTargetNotRegistered",
                "Hint: register it first with WithExternalTarget<T>(), WithExternalTarget(Type), " +
                "or WithExternalTarget(assemblyPath, typeFullName)."));
        }

        return CreateFakeExternal(target.OriginalType, args);
    }

    private static string? GetExternalFakeUnsupportedReason(Type targetType, object[] args)
    {
        if (!targetType.IsClass)
            return "TargetTypeIsNotAClass";
        if (!targetType.IsPublic && !targetType.IsNestedPublic)
            return "TargetTypeIsNotPublic";
        if (targetType.IsAbstract)
            return "AbstractTypeNotSupported";
        if (targetType.IsSealed)
            return "SealedTypeNotSupported";
        if (targetType.ContainsGenericParameters)
            return "OpenGenericTypeNotSupported";

        if (args.Length == 0
            && targetType.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance, binder: null, Type.EmptyTypes, modifiers: null) is null)
        {
            return "PublicParameterlessConstructorNotFound";
        }

        return null;
    }

    /// <summary>
    /// Registers a catch-all shim rule for <typeparamref name="TTarget"/> in the active
    /// <see cref="ShimContext"/>.  The registered instance will be returned by
    /// <see cref="ShimDispatcher.New{T}"/> and <see cref="ShimDispatcher.NewWithArgs{T}"/>
    /// when called from the rewritten assembly.
    /// </summary>
    /// <typeparam name="TTarget">The target type defined in the original assembly.</typeparam>
    /// <param name="fakeInstance">The replacement instance.</param>
    /// <exception cref="ShimException">No active <see cref="ShimContext"/> exists.</exception>
    public void RegisterShim<TTarget>(object fakeInstance) where TTarget : class
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNull(fakeInstance);
        RegisterShimCore(typeof(TTarget), fakeInstance, matchers: null);
    }

    /// <summary>
    /// Registers a catch-all shim rule for the external (cross-assembly) type
    /// <paramref name="externalType"/> in the active <see cref="ShimContext"/>.  The type must have
    /// been registered with <see cref="WithExternalTarget(Type)"/>.  External rules are matched by
    /// <see cref="Type.FullName"/>, so the registered fake is returned even though the
    /// <c>newobj</c> call site may resolve the type through a different load context.
    /// </summary>
    /// <param name="externalType">The external target type, as seen by the test.</param>
    /// <param name="fakeInstance">The replacement instance.</param>
    public void RegisterShim(Type externalType, object fakeInstance)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNull(externalType);
        ThrowHelper.ThrowIfNull(fakeInstance);
        RegisterShimCore(externalType, fakeInstance, matchers: null);
    }

    /// <summary>
    /// Registers a catch-all shim for an external type identified by <paramref name="typeFullName"/>
    /// (Phase 21).  The rule is matched by <see cref="Type.FullName"/>.  When the type was registered
    /// with <see cref="WithExternalTarget(string, string)"/> its assembly simple name is used to
    /// detect duplicate-FullName risk.
    /// </summary>
    /// <param name="typeFullName">The external type's full name.</param>
    /// <param name="fake">The replacement instance.</param>
    public void RegisterShim(string typeFullName, object fake)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNullOrWhiteSpace(typeFullName);
        ThrowHelper.ThrowIfNull(fake);

        var assemblySimpleName = TryGetExternalTargetByFullName(typeFullName, out var target)
            ? target.AssemblySimpleName
            : string.Empty;
        RegisterExternalShimByName(typeFullName, assemblySimpleName, fake);
    }

    /// <summary>
    /// Registers a catch-all shim for an external type identified by <paramref name="typeFullName"/>
    /// and <paramref name="assemblySimpleName"/> (Phase 21).  The FullName is the registry key; the
    /// assembly simple name is recorded for duplicate-FullName risk diagnostics.
    /// </summary>
    /// <param name="typeFullName">The external type's full name.</param>
    /// <param name="assemblySimpleName">The simple name of the assembly that defines the type.</param>
    /// <param name="fake">The replacement instance.</param>
    public void RegisterShim(string typeFullName, string assemblySimpleName, object fake)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNullOrWhiteSpace(typeFullName);
        ThrowHelper.ThrowIfNullOrWhiteSpace(assemblySimpleName);
        ThrowHelper.ThrowIfNull(fake);

        RegisterExternalShimByName(typeFullName, assemblySimpleName, fake);
    }

    /// <summary>
    /// Registers an instance-method shim (Phase 25) for the previously declared
    /// <see cref="WithMethodTarget(Type, string, Type)"/> target.  The shim receives the call receiver
    /// and boxed arguments and returns the replacement result.  Last registration wins.
    /// </summary>
    public void RegisterMethodShim(Type declaringType, string methodName, Func<object?, object?[], object?> shim)
    {
        ThrowHelper.ThrowIfNull(declaringType);
        RegisterMethodShim(declaringType.FullName!, methodName, shim);
    }

    /// <summary>Registers an instance-method shim by declaring type full name (Phase 25).</summary>
    public void RegisterMethodShim(string declaringTypeFullName, string methodName, Func<object?, object?[], object?> shim)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNullOrWhiteSpace(declaringTypeFullName);
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodName);
        ThrowHelper.ThrowIfNull(shim);
        EnsureRewritten();

        var context = ShimContext.RequireCurrent();
        context.EnsureActive();
        context.MethodRegistry.Register(declaringTypeFullName, methodName, shim);
        Diag($"Method shim registered: {declaringTypeFullName}::{methodName}.");
    }

    internal void RegisterMethodShim(
        MethodInfo method,
        Func<object?, object?[], object?> shim,
        IReadOnlyList<IShimArgumentMatcher>? matchers,
        string registrationSource)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNull(method);
        ThrowHelper.ThrowIfNull(shim);
        ThrowHelper.ThrowIfNullOrWhiteSpace(registrationSource);
        EnsureRewritten();

        var context = ShimContext.RequireCurrent();
        context.EnsureActive();
        context.MethodRegistry.Register(method, shim, matchers, registrationSource);
        Diag("Method shim registered: " + MethodSignatureFormatter.Format(method) + ".");
        Diag("  Registration source: " + registrationSource);
    }

    /// <summary>
    /// Registers a shim rule with optional argument matchers for <typeparamref name="TTarget"/>
    /// in the active <see cref="ShimContext"/>.
    /// When <paramref name="matchers"/> is empty the rule is a catch-all (matches any args).
    /// </summary>
    /// <typeparam name="TTarget">The target type defined in the original assembly.</typeparam>
    /// <param name="fakeInstance">The replacement instance.</param>
    /// <param name="matchers">Argument matchers; empty means catch-all.</param>
    /// <exception cref="ShimException">No active <see cref="ShimContext"/> exists.</exception>
    public void RegisterShimWithMatchers<TTarget>(
        object fakeInstance,
        params IShimArgumentMatcher[] matchers) where TTarget : class
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        ThrowHelper.ThrowIfNull(fakeInstance);

        IReadOnlyList<IShimArgumentMatcher>? matcherList =
            matchers.Length == 0 ? null : matchers;
        RegisterShimCore(typeof(TTarget), fakeInstance, matcherList);
    }

    private void RegisterShimCore(Type targetType, object fakeInstance, IReadOnlyList<IShimArgumentMatcher>? matchers)
    {
        EnsureRewritten();

        var context = ShimContext.RequireCurrent();
        context.EnsureActive();

        if (TryGetExternalTarget(targetType, out var external))
        {
            context.Registry.RegisterNewRuleByName(
                external.TypeFullName,
                external.AssemblySimpleName,
                external.OriginalType,
                () => fakeInstance,
                context.ContextId,
                matchers);
            Diag($"Registry key used: {external.TypeFullName} | {NormalizeAssemblyKey(external.AssemblySimpleName)}");
            TrackExternalRegistryKey(external.TypeFullName, external.AssemblySimpleName);
            return;
        }

        var rewrittenType = GetRewrittenType(targetType);
        context.Registry.RegisterNewRule(rewrittenType, () => fakeInstance, context.ContextId, matchers);
    }

    private void RegisterExternalShimByName(string typeFullName, string assemblySimpleName, object fake)
    {
        EnsureRewritten();

        var context = ShimContext.RequireCurrent();
        context.EnsureActive();

        var originalType = TryGetExternalTargetByFullName(typeFullName, out var target)
            ? target.OriginalType
            : typeof(object);

        context.Registry.RegisterNewRuleByName(
            typeFullName, assemblySimpleName, originalType, () => fake, context.ContextId, matchers: null);

        Diag($"Registry key used: {typeFullName} | {NormalizeAssemblyKey(assemblySimpleName)}");
        TrackExternalRegistryKey(typeFullName, assemblySimpleName);
    }

    private void TrackExternalRegistryKey(string typeFullName, string assemblySimpleName)
    {
        if (string.IsNullOrEmpty(assemblySimpleName))
            return;

        if (!_externalRegistryKeys.TryGetValue(typeFullName, out var assemblies))
        {
            assemblies = new HashSet<string>(StringComparer.Ordinal);
            _externalRegistryKeys[typeFullName] = assemblies;
        }

        assemblies.Add(assemblySimpleName);
        if (assemblies.Count > 1)
        {
            Diag($"Duplicate FullName risk: {typeFullName} registered for assemblies [{string.Join(", ", assemblies)}].");
        }
    }

    private static string NormalizeAssemblyKey(string assemblySimpleName)
        => string.IsNullOrEmpty(assemblySimpleName) ? "<fullname-only>" : assemblySimpleName;

    private bool TryGetExternalTarget(Type type, out ExternalNewTarget target)
        => TryGetExternalTargetByFullName(type.FullName, out target);

    private bool TryGetExternalTargetByFullName(string? fullName, out ExternalNewTarget target)
    {
        foreach (var candidate in _externalTargets)
        {
            if (string.Equals(candidate.TypeFullName, fullName, StringComparison.Ordinal))
            {
                target = candidate;
                return true;
            }
        }

        target = null!;
        return false;
    }

    private void AddExternalTarget(ExternalNewTarget target)
    {
        _externalTargets.Add(target);
        Diag($"External target registered: {target.TypeFullName} (assembly {target.AssemblySimpleName}).");
    }

    private void Diag(string message) => _diagnostics.Add(message);

    private sealed class MethodTargetEntry
    {
        public MethodTargetEntry(
            string declaringTypeFullName,
            string methodName,
            Type? returnSubstituteInterface,
            string assemblySimpleName,
            IReadOnlyList<string>? parameterTypeNames = null,
            string? registryKey = null,
            string? methodSignature = null,
            string? returnTypeName = null,
            bool? isVirtual = null,
            string registrationSource = "legacy untyped API")
        {
            DeclaringTypeFullName = declaringTypeFullName;
            MethodName = methodName;
            ReturnSubstituteInterface = returnSubstituteInterface;
            AssemblySimpleName = assemblySimpleName;
            ParameterTypeNames = parameterTypeNames;
            RegistryKey = registryKey ??
                MethodShimRegistry.MakeKey(declaringTypeFullName, methodName);
            MethodSignature = methodSignature ??
                declaringTypeFullName + "." + methodName + "(<legacy name-only>)";
            ReturnTypeName = returnTypeName;
            IsVirtual = isVirtual;
            RegistrationSource = registrationSource;
        }

        public string DeclaringTypeFullName { get; }
        public string MethodName { get; }
        public Type? ReturnSubstituteInterface { get; }
        public string AssemblySimpleName { get; }
        public IReadOnlyList<string>? ParameterTypeNames { get; }
        public string RegistryKey { get; }
        public string MethodSignature { get; }
        public string? ReturnTypeName { get; }
        public bool? IsVirtual { get; }
        public string RegistrationSource { get; }
    }

    /// <summary>
    /// Invokes a public instance method on the given object using reflection and returns the result.
    /// </summary>
    /// <typeparam name="TResult">The expected return type.</typeparam>
    /// <param name="instance">The instance to invoke the method on.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="args">Arguments forwarded to the method.</param>
    public TResult Invoke<TResult>(object instance, string methodName, params object[] args)
    {
        ThrowHelper.ThrowIfNull(instance);
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodName);

        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException(
                $"Public instance method '{methodName}' was not found on {instance.GetType().FullName}.");

        try
        {
            var result = method.Invoke(instance, args);
            return (TResult)result!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is ShimException)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static object? CreateInstanceUnwrapped(Type type)
    {
        try
        {
            return Activator.CreateInstance(type);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is ShimException)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    /// <summary>
    /// Gets the <see cref="Type"/> from the rewritten assembly that corresponds to
    /// <paramref name="originalType"/> from the original assembly.
    /// </summary>
    /// <param name="originalType">The type from the original (non-rewritten) assembly.</param>
    public Type GetRewrittenType(Type originalType)
    {
        ThrowHelper.ThrowIfNull(originalType);
        EnsureRewritten();

        var typeName = originalType.FullName
            ?? throw new InvalidOperationException($"Type {originalType.Name} has no FullName.");

        return _assembly!.GetType(typeName, throwOnError: true)!;
    }

    /// <summary>
    /// Returns a <see cref="WeakReference"/> to the isolated ALC, suitable for unload detection.
    /// Call this <b>before</b> <see cref="Dispose"/> to capture the reference.
    /// After <see cref="Dispose"/> and GC the reference should become dead.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No assembly has been loaded yet (call <see cref="RewriteTargetTypeAssembly"/> first).
    /// </exception>
    public WeakReference GetUnloadReference()
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        if (_loader is null)
        {
            throw new InvalidOperationException(
                "No assembly loaded. Call RewriteTargetTypeAssembly() or RewriteAssembly() first.");
        }

        return _loader.GetUnloadReference();
    }

    /// <summary>
    /// Returns a diagnostics snapshot of the isolated ALC loading state.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No assembly has been loaded yet (call <see cref="RewriteTargetTypeAssembly"/> first).
    /// </exception>
    public ShimAlcDiagnostics GetAlcDiagnostics()
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        if (_loader is null)
        {
            throw new InvalidOperationException(
                "No assembly loaded. Call RewriteTargetTypeAssembly() or RewriteAssembly() first.");
        }

        return _loader.GetDiagnostics();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _loader?.Dispose();
        _loader = null;
        _assembly = null;
    }

    private void EnsureRewritten()
    {
        if (_assembly is null)
        {
            throw new InvalidOperationException(
                "The harness has not run a rewrite yet. " +
                "Call RewriteTargetTypeAssembly() or RewriteAssembly() first.");
        }
    }

    private static string CreateOutputPath(string inputAssemblyPath)
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "MiniMockito.Shims.Experimental",
            "harness",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(outputDirectory);
        return Path.Combine(outputDirectory, Path.GetFileName(inputAssemblyPath));
    }
}
