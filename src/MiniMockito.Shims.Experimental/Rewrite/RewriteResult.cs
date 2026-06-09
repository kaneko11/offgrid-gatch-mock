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

    /// <summary>
    /// Gets the descriptions of call sites that were successfully rewritten.
    /// Each entry is a line from <see cref="Diagnostics"/> that starts with "Rewrote ".
    /// </summary>
    public IReadOnlyList<string> RewrittenCallSiteDescriptions =>
        Diagnostics
            .Where(d => d.StartsWith("Rewrote ", StringComparison.Ordinal))
            .ToArray();

    /// <summary>
    /// Gets the descriptions of call sites that were found in the allowlist but skipped.
    /// Each entry is a line from <see cref="Diagnostics"/> that starts with "Skipped ".
    /// </summary>
    public IReadOnlyList<string> SkippedCallSiteDescriptions =>
        Diagnostics
            .Where(d => d.StartsWith("Skipped ", StringComparison.Ordinal))
            .ToArray();

    /// <summary>
    /// Returns a human-readable summary of the rewrite result.
    /// </summary>
    public string ToSummary()
    {
        var lines = new List<string>
        {
            "=== Rewrite Result ===",
            $"Original assembly : {OriginalAssemblyPath}",
            $"Rewritten assembly: {RewrittenAssemblyPath}",
            $"Rewritten call sites  : {RewrittenCallSiteCount}",
            $"Unsupported call sites: {UnsupportedCallSiteCount}",
        };

        if (RewrittenCallSiteDescriptions.Count > 0)
        {
            lines.Add("Rewritten:");
            foreach (var desc in RewrittenCallSiteDescriptions)
            {
                lines.Add($"  + {desc}");
            }
        }

        if (SkippedCallSiteDescriptions.Count > 0)
        {
            lines.Add("Skipped:");
            foreach (var desc in SkippedCallSiteDescriptions)
            {
                lines.Add($"  - {desc}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
