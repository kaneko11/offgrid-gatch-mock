using System.Reflection;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Test helper that combines assembly rewrite, loading, and instance creation
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
/// <para><b>Important:</b> instances returned by <see cref="Create{TService}"/> and
/// <see cref="CreateFake{TTarget}"/> are from the rewritten assembly load context.
/// Their runtime types differ from the original types even though they share the same
/// full name.  Use <see cref="Invoke{TResult}"/> or reflection to call methods on them.</para>
///
/// <para><b>Parallelism:</b> do not run harness tests in parallel.  The shim dispatcher
/// uses process-wide state; concurrent test runs will interfere with each other.</para>
/// </summary>
public sealed class NewInterceptionHarness : IDisposable
{
    private readonly List<Type> _targetTypes = [];
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
    /// Creates a new harness builder.
    /// </summary>
    public static NewInterceptionHarness Create() => new();

    /// <summary>
    /// Adds <typeparamref name="T"/> to the allowlist of target types to rewrite.
    /// </summary>
    public NewInterceptionHarness WithTarget<T>() where T : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _targetTypes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Rewrites the assembly that contains the first registered target type and loads the output.
    /// </summary>
    /// <exception cref="InvalidOperationException">No target types have been registered.</exception>
    public NewInterceptionHarness RewriteTargetTypeAssembly()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_targetTypes.Count == 0)
        {
            throw new InvalidOperationException(
                "No target types registered. Call WithTarget<T>() before RewriteTargetTypeAssembly().");
        }

        return RewriteAssembly(_targetTypes[0].Assembly.Location);
    }

    /// <summary>
    /// Rewrites the specified assembly using the registered target types and loads the output.
    /// </summary>
    /// <param name="inputAssemblyPath">The path to the assembly to rewrite.</param>
    public NewInterceptionHarness RewriteAssembly(string inputAssemblyPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputAssemblyPath);

        var outputPath = CreateOutputPath(inputAssemblyPath);
        OutputAssemblyPath = outputPath;

        LastRewriteResult = AssemblyRewriter.RewriteNewObj(
            inputAssemblyPath,
            outputPath,
            new RewriteOptions
            {
                TargetTypes = _targetTypes.ToArray(),
            });

        _loader?.Dispose();
        _loader = new RewrittenAssemblyLoader(outputPath);
        _assembly = _loader.Load();
        return this;
    }

    /// <summary>
    /// Creates an instance of <typeparamref name="TService"/> from the rewritten assembly
    /// using a public parameterless constructor.
    /// </summary>
    /// <typeparam name="TService">The service type defined in the original assembly.</typeparam>
    /// <returns>An instance whose runtime type is the rewritten assembly's version of <typeparamref name="TService"/>.</returns>
    public object Create<TService>() where TService : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureRewritten();
        var type = GetRewrittenType(typeof(TService));
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                $"Activator.CreateInstance returned null for {typeof(TService).FullName}.");
    }

    /// <summary>
    /// Creates an instance of <typeparamref name="TTarget"/> from the rewritten assembly
    /// using the specified constructor arguments.  Pass no arguments to use the parameterless constructor.
    /// </summary>
    /// <typeparam name="TTarget">The target type defined in the original assembly.</typeparam>
    /// <param name="constructorArgs">Arguments forwarded to the constructor.</param>
    /// <returns>An instance whose runtime type is the rewritten assembly's version of <typeparamref name="TTarget"/>.</returns>
    public object CreateFake<TTarget>(params object[] constructorArgs) where TTarget : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureRewritten();
        var type = GetRewrittenType(typeof(TTarget));
        return (constructorArgs.Length == 0
                ? Activator.CreateInstance(type)
                : Activator.CreateInstance(type, constructorArgs))
            ?? throw new InvalidOperationException(
                $"Activator.CreateInstance returned null for {typeof(TTarget).FullName}.");
    }

    /// <summary>
    /// Registers a shim rule for <typeparamref name="TTarget"/> in the active <see cref="ShimContext"/>.
    /// The registered instance will be returned by <see cref="ShimDispatcher.New{T}"/> when called
    /// from the rewritten assembly.
    /// </summary>
    /// <typeparam name="TTarget">The target type defined in the original assembly.</typeparam>
    /// <param name="fakeInstance">The replacement instance.  Must be from the rewritten assembly
    /// (e.g. created via <see cref="CreateFake{TTarget}"/>).</param>
    /// <exception cref="ShimException">No active <see cref="ShimContext"/> exists.</exception>
    public void RegisterShim<TTarget>(object fakeInstance) where TTarget : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(fakeInstance);
        EnsureRewritten();

        var rewrittenType = GetRewrittenType(typeof(TTarget));
        var context = ShimContext.RequireCurrent();
        context.EnsureActive();
        context.Registry.RegisterNewRule(rewrittenType, () => fakeInstance, context.ContextId);
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
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

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
        ArgumentNullException.ThrowIfNull(originalType);
        EnsureRewritten();

        var typeName = originalType.FullName
            ?? throw new InvalidOperationException($"Type {originalType.Name} has no FullName.");

        return _assembly!.GetType(typeName, throwOnError: true)!;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

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
