using System.Reflection;

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

    /// <summary>Creates a new harness builder.</summary>
    public static NewInterceptionHarness Create() => new();

    /// <summary>Adds <typeparamref name="T"/> to the allowlist of <c>newobj</c> target types to rewrite.</summary>
    public NewInterceptionHarness WithTarget<T>() where T : class
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        _targetTypes.Add(typeof(T));
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
        _externalTargets.Add(new ExternalNewTarget(externalType));
        return this;
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

        LastRewriteResult = AssemblyRewriter.RewriteNewObj(
            inputAssemblyPath,
            outputPath,
            new RewriteOptions
            {
                TargetTypes = _targetTypes.ToArray(),
                ExternalTargetTypes = _externalTargets.Select(t => t.OriginalType).ToArray(),
                StaticTargetTypes = _staticTargetTypes.ToArray(),
            });

        // Pass the original assembly directory so the ALC can probe for dependencies
        // that are not in the temp output directory.
        var originalDir = Path.GetDirectoryName(inputAssemblyPath);

        // External target assemblies must be shared from the parent ALC so the external type keeps a
        // single runtime identity across the rewrite boundary (required for fake substitution).
        var sharedAssemblyNames = _externalTargets
            .Select(t => t.AssemblySimpleName)
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
        return Activator.CreateInstance(type)
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
        return Activator.CreateInstance(type)
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
            return;
        }

        var rewrittenType = GetRewrittenType(targetType);
        context.Registry.RegisterNewRule(rewrittenType, () => fakeInstance, context.ContextId, matchers);
    }

    private bool TryGetExternalTarget(Type type, out ExternalNewTarget target)
    {
        var fullName = type.FullName;
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

        var result = method.Invoke(instance, args);
        return (TResult)result!;
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
