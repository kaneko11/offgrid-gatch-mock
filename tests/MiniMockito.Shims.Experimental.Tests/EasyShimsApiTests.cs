using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental.Sample;

namespace MiniMockito.Shims.Experimental.Tests;

/// <summary>
/// Phase 23 — Easy Shims API (<see cref="Shims.ForAssembly(string)"/> + <c>ReplaceNew(...)</c>).
/// Exercises the high-level facade without the caller touching NewInterceptionHarness / ShimContext
/// / WithTarget / WithExternalTarget / RegisterShim directly.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class EasyShimsApiTests
{
    private const string ServiceTypeName = "CrossAssemblySample.CrossAssemblyUserService";
    private const string ExternalDbTypeName = "ExternalLib.ExternalDbContext";

    private static string TargetAssemblyPath =>
        typeof(CrossAssemblySample.CrossAssemblyUserService).Assembly.Location;

    private static string ExternalAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "ExternalLib.dll");

    private sealed class FakeExternalDbContext : ExternalDbContext
    {
        private readonly string _tag;
        public FakeExternalDbContext() : this("fake") { }
        public FakeExternalDbContext(string tag) => _tag = tag;
        public override string GetName(int id) => _tag + "-" + id;
    }

    private sealed class FakeExternalLogger : ExternalLogger
    {
        public override string Tag() => "fake-log";
    }

    [TestMethod]
    public void ReplaceNew_Generic_ExternalSubstitutes()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext()))
        {
            var service = shims.CreateObject(ServiceTypeName);
            Assert.AreEqual("fake-1", shims.Invoke<string>(service, "GetDisplayName", 1));
        }
    }

    [TestMethod]
    public void ReplaceNew_ByType_ExternalSubstitutes()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceNew(typeof(ExternalDbContext), new FakeExternalDbContext()))
        {
            var service = shims.CreateObject(ServiceTypeName);
            Assert.AreEqual("fake-1", shims.Invoke<string>(service, "GetDisplayName", 1));
        }
    }

    [TestMethod]
    public void ReplaceNew_StringBased_ExternalSubstitutes()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceNew(ExternalAssemblyPath, ExternalDbTypeName, new FakeExternalDbContext()))
        {
            var service = shims.CreateObject(ServiceTypeName);
            Assert.AreEqual("fake-7", shims.Invoke<string>(service, "GetDisplayName", 7));
        }
    }

    [TestMethod]
    public void ReplaceNew_TwoExternalTargets_InOneSession()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext())
            .ReplaceNew<ExternalLogger>(new FakeExternalLogger()))
        {
            var service = shims.CreateObject(ServiceTypeName);
            // greeter is not replaced -> real "real(...)"; db + logger are faked.
            Assert.AreEqual("real(fake-1|fake-log)", shims.Invoke<string>(service, "Run", 1));
        }
    }

    [TestMethod]
    public void ReplaceNew_MixedInternalAndExternal_InOneSession()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext())
            .ReplaceNew<CrossAssemblySample.InternalGreeter>(s => s.CreateFake<CrossAssemblySample.InternalGreeter>("gfake")))
        {
            var service = shims.CreateObject(ServiceTypeName);
            // db external fake + internal greeter fake; logger left real.
            Assert.AreEqual("gfake(fake-1|real-log)", shims.Invoke<string>(service, "Run", 1));
        }
    }

    [TestMethod]
    public void ReplaceNew_SameTargetTwice_LastStubWins()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext("first"))
            .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext("last")))
        {
            var service = shims.CreateObject(ServiceTypeName);
            Assert.AreEqual("last-1", shims.Invoke<string>(service, "GetDisplayName", 1));
        }
    }

    [TestMethod]
    public void ReplaceNew_AfterRewriteFinalized_Throws()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext()))
        {
            var service = shims.CreateObject(ServiceTypeName); // finalizes the rewrite
            Assert.IsNotNull(service);

            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => shims.ReplaceNew<ExternalLogger>(new FakeExternalLogger()));

            StringAssert.Contains(ex.Message, "rewrite already completed");
            StringAssert.Contains(ex.Message, "target cannot be added after rewrite");
            StringAssert.Contains(ex.Message, "create a new Shims session");
        }
    }

    [TestMethod]
    public void NoMatch_FallsBackToRealConstructor()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext()))
        {
            var service = shims.CreateObject(ServiceTypeName);
            // GetOtherTag uses ExternalOtherContext which is NOT replaced -> real behaviour.
            Assert.AreEqual("real-tag", shims.Invoke<string>(service, "GetOtherTag"));
        }
    }

    [TestMethod]
    public void ReplaceNew_InternalTarget_ViaFactory_Works()
    {
        using (var shims = Shims.For<UserService>()
            .ReplaceNew<UserRepository>(s => s.CreateFake<UserRepository>("fake")))
        {
            var service = shims.CreateObject(typeof(UserService).FullName!);
            Assert.AreEqual("fake-1", shims.Invoke<string>(service, nameof(UserService.GetDisplayName), 1));
        }
    }

    [TestMethod]
    public void Diagnostics_AreForwarded()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceNew(ExternalAssemblyPath, ExternalDbTypeName, new FakeExternalDbContext()))
        {
            var service = shims.CreateObject(ServiceTypeName);
            Assert.AreEqual("fake-1", shims.Invoke<string>(service, "GetDisplayName", 1));

            Assert.IsTrue(
                shims.Diagnostics.Any(d => d.StartsWith("External target registered:", StringComparison.Ordinal)),
                "Expected forwarded harness diagnostics.");
            Assert.IsNotNull(shims.LastDispatchDiagnostics);
            Assert.IsNotNull(shims.GetAlcDiagnostics());
        }
    }

    [TestMethod]
    public void NoManualShimContext_AndDisposeCleansUp()
    {
        var before = ShimContext.ActiveContextCount;

        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext()))
        {
            var service = shims.CreateObject(ServiceTypeName);
            Assert.AreEqual("fake-1", shims.Invoke<string>(service, "GetDisplayName", 1));
            Assert.IsTrue(ShimContext.ActiveContextCount > before,
                "A ShimContext should be active inside the session without the caller creating one.");
        }

        Assert.AreEqual(before, ShimContext.ActiveContextCount,
            "Dispose must clean up the internally-managed ShimContext.");
    }
}
