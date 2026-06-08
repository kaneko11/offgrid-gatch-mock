namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes the result of an experimental assembly rewrite operation.
/// </summary>
public sealed class RewriteResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RewriteResult"/> class.
    /// </summary>
    /// <param name="originalAssemblyPath">The original assembly path.</param>
    /// <param name="rewrittenAssemblyPath">The rewritten assembly path.</param>
    /// <param name="report">The dry-run report used by the rewrite.</param>
    /// <param name="rewrittenCallSiteCount">The number of call sites rewritten.</param>
    /// <param name="diagnostics">Human-readable rewrite diagnostics.</param>
    public RewriteResult(
        string originalAssemblyPath,
        string rewrittenAssemblyPath,
        RewriteReport report,
        int rewrittenCallSiteCount,
        IReadOnlyList<string> diagnostics)
    {
        OriginalAssemblyPath = originalAssemblyPath;
        RewrittenAssemblyPath = rewrittenAssemblyPath;
        Report = report;
        RewrittenCallSiteCount = rewrittenCallSiteCount;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Gets the path of the original assembly.
    /// </summary>
    public string OriginalAssemblyPath { get; }

    /// <summary>
    /// Gets the path of the rewritten assembly.
    /// </summary>
    public string RewrittenAssemblyPath { get; }

    /// <summary>
    /// Gets the dry-run report generated before rewriting.
    /// </summary>
    public RewriteReport Report { get; }

    /// <summary>
    /// Gets the number of call sites rewritten.
    /// </summary>
    public int RewrittenCallSiteCount { get; }

    /// <summary>
    /// Gets the number of unsupported call sites found by the dry-run report.
    /// </summary>
    public int UnsupportedCallSiteCount => Report.UnsupportedCallSites.Count;

    /// <summary>
    /// Gets human-readable rewrite diagnostics.
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; }
}
