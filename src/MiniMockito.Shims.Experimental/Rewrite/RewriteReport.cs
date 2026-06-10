namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Reports the result of a rewrite dry-run scan.
/// </summary>
public sealed class RewriteReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RewriteReport"/> class.
    /// </summary>
    /// <param name="assemblyPath">The scanned assembly path.</param>
    /// <param name="targets">The allowlisted rewrite targets.</param>
    /// <param name="scanResult">The newobj scan result.</param>
    public RewriteReport(string assemblyPath, IReadOnlyList<RewriteTarget> targets, NewObjScanResult scanResult)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(assemblyPath);
        ThrowHelper.ThrowIfNull(targets);
        ThrowHelper.ThrowIfNull(scanResult);
        AssemblyPath = Path.GetFullPath(assemblyPath);
        Targets = targets;
        ScanResult = scanResult;
    }

    /// <summary>
    /// Gets the scanned assembly path.
    /// </summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// Gets the allowlisted rewrite targets.
    /// </summary>
    public IReadOnlyList<RewriteTarget> Targets { get; }

    /// <summary>
    /// Gets the newobj scan result.
    /// </summary>
    public NewObjScanResult ScanResult { get; }

    /// <summary>
    /// Gets all detected allowlisted newobj call sites.
    /// </summary>
    public IReadOnlyList<NewObjCallSite> CallSites => ScanResult.CallSites;

    /// <summary>
    /// Gets detected call sites that satisfy the Phase 3 supported pattern.
    /// </summary>
    public IReadOnlyList<NewObjCallSite> SupportedCallSites => ScanResult.SupportedCallSites;

    /// <summary>
    /// Gets detected call sites that are allowlisted but unsupported.
    /// </summary>
    public IReadOnlyList<NewObjCallSite> UnsupportedCallSites => ScanResult.UnsupportedCallSites;
}
