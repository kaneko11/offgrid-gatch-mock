namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes a dry-run rewrite scan request.
/// </summary>
public sealed class RewritePlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RewritePlan"/> class.
    /// </summary>
    /// <param name="assemblyPath">The assembly path to scan.</param>
    /// <param name="targets">The allowlisted target types.</param>
    public RewritePlan(string assemblyPath, IEnumerable<RewriteTarget> targets)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(assemblyPath);
        ThrowHelper.ThrowIfNull(targets);
        AssemblyPath = Path.GetFullPath(assemblyPath);
        Targets = targets.ToArray();
    }

    /// <summary>
    /// Gets the assembly path to scan.
    /// </summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// Gets the allowlisted rewrite targets.
    /// </summary>
    public IReadOnlyList<RewriteTarget> Targets { get; }

    /// <summary>
    /// Creates a rewrite plan from scan options.
    /// </summary>
    /// <param name="assemblyPath">The assembly path to scan.</param>
    /// <param name="options">The scan options.</param>
    /// <returns>A rewrite plan.</returns>
    public static RewritePlan FromOptions(string assemblyPath, NewObjScanOptions options)
    {
        ThrowHelper.ThrowIfNull(options);
        return new RewritePlan(assemblyPath, options.TargetTypes.Select(type => new RewriteTarget(type)));
    }
}
