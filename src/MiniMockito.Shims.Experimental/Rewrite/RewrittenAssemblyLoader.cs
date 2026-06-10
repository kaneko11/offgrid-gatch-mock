using System.Reflection;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Loads a rewritten assembly for experimental shim tests.
/// On .NET 5+, uses an isolated collectible <see cref="ShimAssemblyLoadContext"/>.
/// On .NET Framework 4.8, uses <see cref="Assembly.Load(byte[])"/> (load-from-bytes) with an
/// <see cref="AppDomain.AssemblyResolve"/> handler so that already-loaded assemblies
/// (especially <c>MiniMockito.Shims.Experimental</c>) are shared as singletons.
/// LoadFrom is intentionally avoided: it caches assemblies by identity, so a second
/// harness in the same test run would receive the first rewritten copy, defeating
/// the per-harness rewrite.
/// </summary>
public sealed class RewrittenAssemblyLoader : IDisposable
{
#if !NETFRAMEWORK
    // Not readonly — must be nulled in Dispose() so the ALC becomes eligible for GC.
    private ShimAssemblyLoadContext? _context;
#else
    private Assembly? _netFxAssembly;
    private readonly string? _originalDirectory;
    private ResolveEventHandler? _resolveHandler;
#endif

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RewrittenAssemblyLoader"/> class.
    /// </summary>
    /// <param name="assemblyPath">The rewritten assembly path.</param>
    /// <param name="originalAssemblyDirectory">
    /// Optional directory of the original (non-rewritten) assembly.
    /// Used as a fallback probing path for dependencies not resolvable from the temp directory.
    /// </param>
    public RewrittenAssemblyLoader(string assemblyPath, string? originalAssemblyDirectory = null)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(assemblyPath);
        AssemblyPath = Path.GetFullPath(assemblyPath);
#if !NETFRAMEWORK
        _context = new ShimAssemblyLoadContext(AssemblyPath, originalAssemblyDirectory);
#else
        _originalDirectory = originalAssemblyDirectory;
#endif
    }

    /// <summary>Gets the rewritten assembly path.</summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// Loads the rewritten assembly and returns the loaded <see cref="Assembly"/>.
    /// </summary>
    public Assembly Load()
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
#if !NETFRAMEWORK
        return _context!.LoadFromAssemblyPath(AssemblyPath);
#else
        if (_netFxAssembly != null)
            return _netFxAssembly;

        // Register a resolve handler so that assemblies already loaded in the AppDomain
        // (especially MiniMockito.Shims.Experimental) are reused as singletons.
        // This mirrors the ALC parent-fallback pattern for .NET Framework.
        _resolveHandler = OnAssemblyResolve;
        AppDomain.CurrentDomain.AssemblyResolve += _resolveHandler;

        // Use Assembly.Load(byte[]) instead of Assembly.LoadFrom(path).
        // LoadFrom caches assemblies by identity: a second harness in the same test run
        // would receive the first rewritten copy, defeating per-harness IL rewrites.
        // Load(byte[]) bypasses the LoadFrom identity cache, giving each harness
        // its own freshly-rewritten assembly instance.
        byte[] bytes = File.ReadAllBytes(AssemblyPath);
        _netFxAssembly = Assembly.Load(bytes);
        return _netFxAssembly;
#endif
    }

#if NETFRAMEWORK
    private Assembly? OnAssemblyResolve(object sender, ResolveEventArgs args)
    {
        var requestedName = new AssemblyName(args.Name);

        // Return already-loaded assemblies to ensure type identity singletons.
        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(loaded.GetName().Name, requestedName.Name, StringComparison.OrdinalIgnoreCase))
                return loaded;
        }

        // Probe the original assembly directory as a fallback.
        if (_originalDirectory != null)
        {
            var candidate = Path.Combine(_originalDirectory, requestedName.Name + ".dll");
            if (File.Exists(candidate))
                return Assembly.LoadFrom(candidate);
        }

        return null;
    }
#endif

    /// <summary>
    /// Returns a <see cref="WeakReference"/> to the isolated ALC, suitable for unload detection.
    /// On .NET Framework, assembly unload is not supported without AppDomain recycling;
    /// a dead reference is returned instead.
    /// </summary>
    public WeakReference GetUnloadReference()
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
#if !NETFRAMEWORK
        return new WeakReference(_context, trackResurrection: true);
#else
        return new WeakReference(null, trackResurrection: true);
#endif
    }

    /// <summary>
    /// Returns a diagnostics snapshot of the current loading state.
    /// </summary>
    public ShimAlcDiagnostics GetDiagnostics()
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
#if !NETFRAMEWORK
        return _context!.GetDiagnostics();
#else
        var names = _netFxAssembly != null
            ? new List<string> { _netFxAssembly.GetName().Name ?? "?" }
            : new List<string>();
        return new ShimAlcDiagnostics
        {
            AlcName = "NetFx-LoadBytes",
            IsCollectible = false,
            RewrittenAssemblyPath = AssemblyPath,
            OriginalAssemblyDirectory = _originalDirectory,
            LoadedAssemblyNames = names.AsReadOnly(),
            ResolvedPaths = new List<string>().AsReadOnly(),
            ParentFallbacks = new List<string>().AsReadOnly(),
        };
#endif
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
#if !NETFRAMEWORK
        _context?.Unload();
        _context = null;
#else
        if (_resolveHandler != null)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= _resolveHandler;
            _resolveHandler = null;
        }
        _netFxAssembly = null;
#endif
    }
}
