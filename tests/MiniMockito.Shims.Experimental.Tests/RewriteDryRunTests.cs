using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

[TestClass]
[DoNotParallelize]
public sealed class RewriteDryRunTests
{
    [TestMethod]
    public void Scan_DetectsAllowlistedParameterlessNewObj()
    {
        var report = ScanFor(typeof(DryRunUserRepository));

        var callSite = SingleCallSiteForMethod(report, nameof(DryRunSampleService.CreateUserRepository));

        Assert.IsTrue(callSite.IsSupported);
        Assert.AreEqual(".ctor()", callSite.TargetConstructor);
        Assert.IsNull(callSite.UnsupportedReason);
    }

    [TestMethod]
    public void Scan_ExcludesTypesThatAreNotAllowlisted()
    {
        var report = ScanFor(typeof(DryRunUserRepository));

        Assert.IsFalse(report.CallSites.Any(callSite =>
            callSite.TargetTypeName.Contains(nameof(DryRunIgnoredRepository), StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Scan_ReportsSimpleConstructorArgumentsAsSupported()
    {
        // Phase 7: simple constructor arguments (string, int, bool) are now supported.
        var report = ScanFor(typeof(DryRunRepositoryWithArguments));

        var callSite = SingleCallSiteForMethod(report, nameof(DryRunSampleService.CreateRepositoryWithArguments));

        Assert.IsTrue(callSite.IsSupported);
        Assert.IsNull(callSite.UnsupportedReason);
        StringAssert.Contains(callSite.TargetConstructor, typeof(string).FullName!);
    }

    [TestMethod]
    public void Scan_ReportsGenericTypeAsUnsupported()
    {
        var report = ScanFor(typeof(DryRunGenericRepository<string>));

        var callSite = SingleCallSiteForMethod(report, nameof(DryRunSampleService.CreateGenericRepository));

        Assert.IsFalse(callSite.IsSupported);
        Assert.AreEqual("GenericTypeNotSupported", callSite.UnsupportedReason);
    }

    [TestMethod]
    public void Scan_ReportContainsCallingTypeCallingMethodAndIlOffset()
    {
        var report = ScanFor(typeof(DryRunUserRepository));

        var callSite = SingleCallSiteForMethod(report, nameof(DryRunSampleService.CreateUserRepository));

        StringAssert.Contains(callSite.CallingTypeName, nameof(DryRunSampleService));
        Assert.AreEqual(nameof(DryRunSampleService.CreateUserRepository), callSite.CallingMethodName);
        Assert.IsTrue(callSite.ILOffset >= 0);
    }

    [TestMethod]
    public void Scan_ReportContainsAssemblyAndTargetMetadata()
    {
        var assemblyPath = typeof(DryRunSampleService).Assembly.Location;
        // Phase 7: use DryRunByRefTarget (ref param — still unsupported) for the unsupported side.
        var report = AssemblyRewriteScanner.Scan(
            assemblyPath,
            new NewObjScanOptions
            {
                TargetTypes = [typeof(DryRunUserRepository), typeof(DryRunByRefTarget)]
            });

        Assert.AreEqual(Path.GetFullPath(assemblyPath), report.AssemblyPath);
        Assert.AreEqual(2, report.Targets.Count);
        Assert.IsTrue(report.SupportedCallSites.Any());
        Assert.IsTrue(report.UnsupportedCallSites.Any());
    }

    [TestMethod]
    public void Scan_UnsupportedReasonIsReadable()
    {
        // Phase 7: by-ref constructor arguments remain unsupported.
        var report = ScanFor(typeof(DryRunByRefTarget));

        var callSite = SingleCallSiteForMethod(report, nameof(DryRunSampleService.CreateByRefTarget));

        Assert.IsFalse(string.IsNullOrWhiteSpace(callSite.UnsupportedReason));
        StringAssert.Contains(callSite.UnsupportedReason, "ByRef");
    }

    private static RewriteReport ScanFor(params Type[] targetTypes)
    {
        return AssemblyRewriteScanner.Scan(
            typeof(DryRunSampleService).Assembly.Location,
            new NewObjScanOptions
            {
                TargetTypes = targetTypes
            });
    }

    private static NewObjCallSite SingleCallSiteForMethod(RewriteReport report, string methodName)
    {
        return report.CallSites.Single(callSite =>
            string.Equals(callSite.CallingMethodName, methodName, StringComparison.Ordinal));
    }

    public sealed class DryRunSampleService
    {
        public DryRunUserRepository CreateUserRepository()
        {
            return new DryRunUserRepository();
        }

        public DryRunIgnoredRepository CreateIgnoredRepository()
        {
            return new DryRunIgnoredRepository();
        }

        public DryRunRepositoryWithArguments CreateRepositoryWithArguments()
        {
            return new DryRunRepositoryWithArguments("configured");
        }

        public DryRunGenericRepository<string> CreateGenericRepository()
        {
            return new DryRunGenericRepository<string>();
        }

        public DryRunByRefTarget CreateByRefTarget(int value)
        {
            return new DryRunByRefTarget(ref value);
        }
    }

    public sealed class DryRunUserRepository
    {
    }

    public sealed class DryRunIgnoredRepository
    {
    }

    public sealed class DryRunRepositoryWithArguments
    {
        public DryRunRepositoryWithArguments(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public sealed class DryRunGenericRepository<T>
    {
    }

    public sealed class DryRunByRefTarget
    {
        public DryRunByRefTarget(ref int value)
        {
        }
    }
}
