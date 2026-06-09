using System.Text;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Diagnostics snapshot captured from a <see cref="ShimAssemblyLoadContext"/>.
/// </summary>
/// <remarks>
/// <b>Experimental.</b> This class is part of <c>MiniMockito.Shims.Experimental</c>.
/// API may change in future phases.
/// </remarks>
public sealed class ShimAlcDiagnostics
{
    internal ShimAlcDiagnostics()
    {
    }

    /// <summary>Gets the ALC name (e.g. <c>ShimIsolated-MyAssembly</c>).</summary>
    public string AlcName { get; init; } = string.Empty;

    /// <summary>Gets whether the ALC was created with <c>isCollectible: true</c>.</summary>
    public bool IsCollectible { get; init; }

    /// <summary>Gets the full path of the rewritten assembly loaded into the ALC.</summary>
    public string RewrittenAssemblyPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the original test output directory used as a fallback probing path,
    /// or <see langword="null"/> if probing was not configured.
    /// </summary>
    public string? OriginalAssemblyDirectory { get; init; }

    /// <summary>
    /// Gets the short assembly names of all assemblies currently loaded into the isolated ALC
    /// (excludes assemblies resolved via the parent/default ALC).
    /// </summary>
    public IReadOnlyList<string> LoadedAssemblyNames { get; init; } = [];

    /// <summary>
    /// Gets descriptions of dependencies resolved by <see cref="System.Runtime.Loader.AssemblyDependencyResolver"/>
    /// or by probing the original output directory.
    /// </summary>
    public IReadOnlyList<string> ResolvedPaths { get; init; } = [];

    /// <summary>
    /// Gets assembly names that fell back to the parent (default) ALC.
    /// Falling back is expected for <c>MiniMockito.Shims.Experimental</c> and BCL assemblies.
    /// </summary>
    public IReadOnlyList<string> ParentFallbacks { get; init; } = [];

    /// <summary>Formats the diagnostics as a multi-line human-readable string.</summary>
    public string Format()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ALC name         : {AlcName}");
        sb.AppendLine($"Collectible      : {IsCollectible}");
        sb.AppendLine($"Rewritten path   : {RewrittenAssemblyPath}");
        if (OriginalAssemblyDirectory is not null)
            sb.AppendLine($"Original dir     : {OriginalAssemblyDirectory}");

        sb.AppendLine($"Loaded in ALC    : {LoadedAssemblyNames.Count}");
        foreach (var n in LoadedAssemblyNames)
            sb.AppendLine($"  {n}");

        if (ResolvedPaths.Count > 0)
        {
            sb.AppendLine("Resolved paths:");
            foreach (var p in ResolvedPaths)
                sb.AppendLine($"  {p}");
        }

        if (ParentFallbacks.Count > 0)
        {
            sb.AppendLine("Parent ALC fallbacks:");
            foreach (var f in ParentFallbacks)
                sb.AppendLine($"  {f}");
        }

        return sb.ToString().TrimEnd();
    }
}
