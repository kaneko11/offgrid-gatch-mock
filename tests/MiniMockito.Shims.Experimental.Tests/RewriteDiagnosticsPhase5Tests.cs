using MiniMockito.Shims.Experimental.Sample;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

[TestClass]
[DoNotParallelize]
public sealed class RewriteDiagnosticsPhase5Tests
{
    // -------------------------------------------------------------------------
    // RewrittenCallSiteDescriptions
    // -------------------------------------------------------------------------

    [TestMethod]
    public void RewriteResult_RewrittenCallSiteDescriptions_ContainsRewrittenSites()
    {
        var result = RewriteSampleAssembly([typeof(UserRepository)]);

        Assert.IsTrue(result.RewrittenCallSiteDescriptions.Count >= 1,
            "At least one rewritten call site description should be present.");
    }

    [TestMethod]
    public void RewriteResult_RewrittenCallSiteDescriptions_ContainTargetTypeName()
    {
        var result = RewriteSampleAssembly([typeof(UserRepository)]);

        var desc = result.RewrittenCallSiteDescriptions.First();
        StringAssert.Contains(desc, typeof(UserRepository).FullName!,
            "Rewritten description should contain the target type name.");
    }

    [TestMethod]
    public void RewriteResult_RewrittenCallSiteDescriptions_ContainShimDispatcher()
    {
        var result = RewriteSampleAssembly([typeof(UserRepository)]);

        Assert.IsTrue(
            result.RewrittenCallSiteDescriptions.Any(d =>
                d.Contains(nameof(ShimDispatcher), StringComparison.Ordinal)),
            "Rewritten description should reference ShimDispatcher.");
    }

    // -------------------------------------------------------------------------
    // SkippedCallSiteDescriptions
    // -------------------------------------------------------------------------

    [TestMethod]
    public void RewriteResult_SkippedCallSiteDescriptions_ContainsSkippedSites()
    {
        // UserRepository has a constructor with arguments — that call site should be skipped.
        var result = RewriteSampleAssembly([typeof(UserRepository)]);

        Assert.IsTrue(result.SkippedCallSiteDescriptions.Count >= 1,
            "At least one skipped call site description should be present.");
    }

    [TestMethod]
    public void RewriteResult_SkippedCallSiteDescriptions_ContainUnsupportedReason()
    {
        var result = RewriteSampleAssembly([typeof(UserRepository)]);

        Assert.IsTrue(
            result.SkippedCallSiteDescriptions.Any(d =>
                d.Contains("constructor arguments", StringComparison.OrdinalIgnoreCase) ||
                d.Contains("generic", StringComparison.OrdinalIgnoreCase) ||
                d.Contains("not supported", StringComparison.OrdinalIgnoreCase)),
            "Skipped description should contain a human-readable reason.");
    }

    // -------------------------------------------------------------------------
    // ToSummary
    // -------------------------------------------------------------------------

    [TestMethod]
    public void RewriteResult_ToSummary_ContainsRewrittenAndSkippedSections()
    {
        var result = RewriteSampleAssembly([typeof(UserRepository)]);
        var summary = result.ToSummary();

        StringAssert.Contains(summary, "Rewritten assembly:", "Summary should include rewritten assembly path.");
        StringAssert.Contains(summary, "Rewritten call sites", "Summary should include rewritten count.");
    }

    [TestMethod]
    public void RewriteResult_ToSummary_ContainsRewrittenSection()
    {
        var result = RewriteSampleAssembly([typeof(UserRepository)]);
        var summary = result.ToSummary();

        StringAssert.Contains(summary, "Rewritten:", "Summary should include rewritten section.");
    }

    // -------------------------------------------------------------------------
    // Unsupported pattern diagnostics
    // -------------------------------------------------------------------------

    [TestMethod]
    public void UnsupportedPattern_ConstructorArguments_ReportedInScanResult()
    {
        var report = AssemblyRewriteScanner.Scan(
            typeof(UserService).Assembly.Location,
            new NewObjScanOptions
            {
                TargetTypes = [typeof(UserRepository)],
            });

        Assert.IsTrue(
            report.UnsupportedCallSites.Any(cs =>
                cs.UnsupportedReason == "ConstructorArgumentsNotSupported"),
            "Constructor arguments should be reported as unsupported.");
    }

    [TestMethod]
    public void UnsupportedPattern_GenericType_ReportedInScanResult()
    {
        var report = AssemblyRewriteScanner.Scan(
            typeof(UserService).Assembly.Location,
            new NewObjScanOptions
            {
                TargetTypes = [typeof(GenericRepository<string>)],
            });

        Assert.IsTrue(
            report.UnsupportedCallSites.Any(cs =>
                cs.UnsupportedReason == "GenericTypeNotSupported"),
            "Generic types should be reported as unsupported.");
    }

    [TestMethod]
    public void UnsupportedPattern_UnsupportedReasonIsNonEmpty()
    {
        var report = AssemblyRewriteScanner.Scan(
            typeof(UserService).Assembly.Location,
            new NewObjScanOptions
            {
                TargetTypes = [typeof(UserRepository), typeof(GenericRepository<string>)],
            });

        foreach (var callSite in report.UnsupportedCallSites)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(callSite.UnsupportedReason),
                $"UnsupportedReason should not be empty for {callSite.TargetTypeName}.");
        }
    }

    [TestMethod]
    public void ScanResult_SupportedCallSites_AreNotInUnsupported()
    {
        var report = AssemblyRewriteScanner.Scan(
            typeof(UserService).Assembly.Location,
            new NewObjScanOptions
            {
                TargetTypes = [typeof(UserRepository), typeof(GenericRepository<string>)],
            });

        foreach (var supported in report.SupportedCallSites)
        {
            Assert.IsNull(supported.UnsupportedReason,
                "Supported call sites should have null UnsupportedReason.");
            Assert.IsTrue(supported.IsSupported);
        }
    }

    [TestMethod]
    public void RewriteResult_Diagnostics_ContainDryRunSummaryLine()
    {
        var result = RewriteSampleAssembly([typeof(UserRepository)]);

        Assert.IsTrue(
            result.Diagnostics.Any(d => d.Contains("Dry-run scan", StringComparison.OrdinalIgnoreCase)),
            "Diagnostics should contain dry-run scan summary.");
    }

    [TestMethod]
    public void RewriteResult_Diagnostics_ContainOutputPath()
    {
        var result = RewriteSampleAssembly([typeof(UserRepository)]);

        Assert.IsTrue(
            result.Diagnostics.Any(d => d.Contains("Wrote rewritten assembly", StringComparison.Ordinal)),
            "Diagnostics should note where the rewritten assembly was written.");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static RewriteResult RewriteSampleAssembly(Type[] targetTypes)
    {
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            "MiniMockito.Shims.Experimental.Tests",
            "diagnostics",
            Guid.NewGuid().ToString("N"),
            Path.GetFileName(typeof(UserService).Assembly.Location));

        return AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputPath,
            new RewriteOptions
            {
                TargetTypes = targetTypes,
            });
    }
}
