namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Contains detected <c>newobj</c> call sites for a dry-run scan.
/// </summary>
public sealed class NewObjScanResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NewObjScanResult"/> class.
    /// </summary>
    /// <param name="callSites">The detected call sites.</param>
    public NewObjScanResult(IEnumerable<NewObjCallSite> callSites)
    {
        ArgumentNullException.ThrowIfNull(callSites);
        CallSites = callSites.ToArray();
    }

    /// <summary>
    /// Gets all detected allowlisted call sites.
    /// </summary>
    public IReadOnlyList<NewObjCallSite> CallSites { get; }

    /// <summary>
    /// Gets call sites that satisfy the Phase 3 supported pattern.
    /// </summary>
    public IReadOnlyList<NewObjCallSite> SupportedCallSites => CallSites.Where(callSite => callSite.IsSupported).ToArray();

    /// <summary>
    /// Gets allowlisted call sites that are unsupported.
    /// </summary>
    public IReadOnlyList<NewObjCallSite> UnsupportedCallSites => CallSites.Where(callSite => !callSite.IsSupported).ToArray();
}
