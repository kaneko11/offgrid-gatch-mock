namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Result of a <see cref="StaticCallRewriter.Rewrite"/> operation.
/// </summary>
public sealed class StaticRewriteResult
{
    internal StaticRewriteResult(
        int rewrittenCallSiteCount,
        IReadOnlyList<StaticCallSite> rewrittenCallSites,
        IReadOnlyList<StaticCallSite> skippedCallSites,
        IReadOnlyList<string> diagnostics)
    {
        RewrittenCallSiteCount = rewrittenCallSiteCount;
        RewrittenCallSites = rewrittenCallSites;
        SkippedCallSites = skippedCallSites;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the number of call sites that were rewritten.</summary>
    public int RewrittenCallSiteCount { get; }

    /// <summary>Gets the rewritten call sites.</summary>
    public IReadOnlyList<StaticCallSite> RewrittenCallSites { get; }

    /// <summary>Gets call sites that were skipped (BCL, generic, by-ref, etc.).</summary>
    public IReadOnlyList<StaticCallSite> SkippedCallSites { get; }

    /// <summary>Gets the diagnostic messages produced during the rewrite.</summary>
    public IReadOnlyList<string> Diagnostics { get; }
}
