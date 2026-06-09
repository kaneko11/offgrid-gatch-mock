using System.Reflection;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Loads a rewritten assembly into an isolated <see cref="ShimAssemblyLoadContext"/>
/// (collectible) for experimental shim tests.
/// </summary>
public sealed class RewrittenAssemblyLoader : IDisposable
{
    // Not readonly — must be nulled in Dispose() so the ALC becomes eligible for GC.
    private ShimAssemblyLoadContext? _context;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RewrittenAssemblyLoader"/> class.
    /// </summary>
    /// <param name="assemblyPath">The rewritten assembly path.</param>
    /// <param name="originalAssemblyDirectory">
    /// Optional directory of the original (non-rewritten) assembly.
    /// Used as a fallback probing path for dependencies not resolvable from
    /// the temp directory.
    /// </param>
    public RewrittenAssemblyLoader(string assemblyPath, string? originalAssemblyDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        AssemblyPath = Path.GetFullPath(assemblyPath);
        _context = new ShimAssemblyLoadContext(AssemblyPath, originalAssemblyDirectory);
    }

    /// <summary>Gets the rewritten assembly path.</summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// Loads the rewritten assembly into the isolated ALC and returns the loaded
    /// <see cref="Assembly"/>.
    /// </summary>
    public Assembly Load()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _context!.LoadFromAssemblyPath(AssemblyPath);
    }

    /// <summary>
    /// Returns a <see cref="WeakReference"/> to the isolated ALC, suitable for unload detection.
    /// After <see cref="Dispose"/> is called and the GC runs, the reference should become dead.
    /// </summary>
    /// <remarks>
    /// The ALC becomes eligible for GC only after:
    /// <list type="number">
    ///   <item><see cref="Dispose"/> is called (which calls <c>Unload()</c> and nulls the internal reference).</item>
    ///   <item>All other strong references to the ALC, its assemblies, and any <see cref="Type"/>
    ///   or <see cref="System.Reflection.MethodInfo"/> objects from it are released.</item>
    ///   <item>The GC runs (use <c>GC.Collect</c> + <c>GC.WaitForPendingFinalizers</c> in tests).</item>
    /// </list>
    /// </remarks>
    public WeakReference GetUnloadReference()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new WeakReference(_context, trackResurrection: true);
    }

    /// <summary>
    /// Returns a diagnostics snapshot of the current ALC loading state.
    /// </summary>
    public ShimAlcDiagnostics GetDiagnostics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _context!.GetDiagnostics();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _context?.Unload();
        _context = null; // Release strong reference so the ALC becomes eligible for GC.
    }
}
