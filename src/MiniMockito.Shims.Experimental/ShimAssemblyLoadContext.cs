using System.Reflection;

#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif

namespace MiniMockito.Shims.Experimental;

#if !NETFRAMEWORK
/// <summary>
/// Isolated, collectible <see cref="AssemblyLoadContext"/> for loading rewritten assemblies
/// during shim interception experiments.
/// </summary>
/// <remarks>
/// <b>Experimental.</b> This class is part of <c>MiniMockito.Shims.Experimental</c>.
/// API may change in future phases.
/// <para>
/// The context is created with <c>isCollectible: true</c>, meaning the loaded assemblies can be
/// unloaded by calling <see cref="AssemblyLoadContext.Unload()"/> and then letting GC collect the
/// context (use a <see cref="WeakReference"/> to verify).
/// </para>
/// <para>
/// <c>MiniMockito.Shims.Experimental</c> itself is always resolved via the parent (default) ALC so
/// that <see cref="ShimDispatcher"/>, <see cref="ShimContext"/>, and <see cref="ShimRuleRegistry"/>
/// remain process-wide singletons.  Loading them into an isolated ALC would create a second
/// registry, breaking shim rule lookup.
/// </para>
/// <para><b>Type identity constraint:</b> types resolved from this ALC are distinct
/// <see cref="Type"/> objects from identically named types in the default ALC, even though their
/// <see cref="Type.FullName"/> values are the same.  Use reflection-based APIs (e.g.
/// <see cref="NewInterceptionHarness.Invoke{TResult}"/>) rather than direct casts.</para>
/// </remarks>
public sealed class ShimAssemblyLoadContext : AssemblyLoadContext
{
    private static readonly AssemblyName s_shimExperimentalName =
        typeof(ShimDispatcher).Assembly.GetName();

    private readonly AssemblyDependencyResolver _resolver;
    private readonly string? _originalDirectory;
    private readonly HashSet<string> _sharedAssemblyNames;

    private readonly object _diagLock = new();
    private readonly List<string> _resolvedPaths = [];
    private readonly List<string> _parentFallbacks = [];

    /// <summary>
    /// Initializes a new instance of <see cref="ShimAssemblyLoadContext"/>.
    /// </summary>
    /// <param name="rewrittenAssemblyPath">
    /// Path to the rewritten assembly that will be loaded into this context.
    /// </param>
    /// <param name="originalAssemblyDirectory">
    /// Optional directory of the original (non-rewritten) assembly.
    /// Used as a fallback probing path when <see cref="AssemblyDependencyResolver"/>
    /// cannot resolve a dependency (e.g. no <c>.deps.json</c> in the temp directory).
    /// </param>
    /// <param name="sharedAssemblyNames">
    /// Optional simple names of assemblies that must be shared from the parent (default) ALC instead
    /// of being loaded into this isolated context.  Used for cross-assembly external <c>newobj</c>
    /// targets so that the external type has the same runtime identity in both the rewritten code and
    /// the registering test, allowing a fake to be substituted across the call boundary.
    /// </param>
    public ShimAssemblyLoadContext(
        string rewrittenAssemblyPath,
        string? originalAssemblyDirectory = null,
        IEnumerable<string>? sharedAssemblyNames = null)
        : base(
            name: $"ShimIsolated-{Path.GetFileNameWithoutExtension(rewrittenAssemblyPath)}",
            isCollectible: true)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(rewrittenAssemblyPath);
        RewrittenAssemblyPath = Path.GetFullPath(rewrittenAssemblyPath);
        _originalDirectory = originalAssemblyDirectory;
        _resolver = new AssemblyDependencyResolver(RewrittenAssemblyPath);
        _sharedAssemblyNames = sharedAssemblyNames is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(sharedAssemblyNames, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the full path of the rewritten assembly this context is intended to load.</summary>
    public string RewrittenAssemblyPath { get; }

    /// <summary>
    /// Returns a snapshot of diagnostics collected during assembly loading.
    /// </summary>
    public ShimAlcDiagnostics GetDiagnostics()
    {
        List<string> resolvedSnapshot;
        List<string> fallbackSnapshot;

        lock (_diagLock)
        {
            resolvedSnapshot = [.. _resolvedPaths];
            fallbackSnapshot = [.. _parentFallbacks];
        }

        var loadedNames = Assemblies
            .Select(a => a.GetName().Name ?? "?")
            .ToList();

        return new ShimAlcDiagnostics
        {
            AlcName = Name ?? "unnamed",
            IsCollectible = IsCollectible,
            RewrittenAssemblyPath = RewrittenAssemblyPath,
            OriginalAssemblyDirectory = _originalDirectory,
            LoadedAssemblyNames = loadedNames.AsReadOnly(),
            ResolvedPaths = resolvedSnapshot.AsReadOnly(),
            ParentFallbacks = fallbackSnapshot.AsReadOnly(),
        };
    }

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // MiniMockito.Shims.Experimental must be shared from the default ALC.
        // ShimDispatcher / ShimContext / ShimRuleRegistry must be process-wide singletons;
        // loading them here would create a second registry, breaking shim rule lookup.
        if (AssemblyName.ReferenceMatchesDefinition(s_shimExperimentalName, assemblyName))
        {
            lock (_diagLock)
                _parentFallbacks.Add($"{assemblyName.Name} (shim-experimental → parent ALC)");
            return null;
        }

        // Cross-assembly external targets must be shared from the parent (default) ALC so the
        // external type has a single runtime identity across the rewritten code and the test.
        if (assemblyName.Name is not null && _sharedAssemblyNames.Contains(assemblyName.Name))
        {
            lock (_diagLock)
                _parentFallbacks.Add($"{assemblyName.Name} (external target → parent ALC)");
            return null;
        }

        // Step 1: try AssemblyDependencyResolver (requires .deps.json in the rewritten output dir).
        var resolverPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolverPath is not null)
        {
            lock (_diagLock)
                _resolvedPaths.Add($"{assemblyName.Name} → {resolverPath} (resolver)");
            return LoadFromAssemblyPath(resolverPath);
        }

        // Step 2: probe the original test output directory as a fallback.
        if (_originalDirectory is not null)
        {
            var candidate = Path.Combine(_originalDirectory, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
            {
                lock (_diagLock)
                    _resolvedPaths.Add($"{assemblyName.Name} → {candidate} (probing)");
                return LoadFromAssemblyPath(candidate);
            }
        }

        // Step 3: fall back to parent (default) ALC for BCL, System.Runtime, etc.
        lock (_diagLock)
            _parentFallbacks.Add($"{assemblyName.Name} (→ parent ALC)");
        return null;
    }
}
#endif
