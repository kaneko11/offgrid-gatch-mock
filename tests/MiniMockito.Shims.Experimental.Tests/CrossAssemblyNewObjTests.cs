using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;

namespace MiniMockito.Shims.Experimental.Tests;

/// <summary>
/// Phase 20 — cross-assembly <c>newobj</c> interception PoC.
/// The rewrite target is <c>CrossAssemblySample.dll</c>, but the intercepted <c>newobj</c>
/// declaring types live in <c>ExternalLib.dll</c>.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CrossAssemblyNewObjTests
{
    private const string ServiceTypeName = "CrossAssemblySample.CrossAssemblyUserService";

    /// <summary>Hand-written fake for the external type (the first-recommended substitution path).</summary>
    private sealed class FakeExternalDbContext : ExternalDbContext
    {
        public override string GetName(int id) => "fake-" + id;
    }

    private static string TargetAssemblyPath =>
        typeof(CrossAssemblySample.CrossAssemblyUserService).Assembly.Location;

    [TestMethod]
    public void WithExternalTarget_Generic_RewritesAndSubstitutesFake()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget<ExternalDbContext>()
            .RewriteAssembly(TargetAssemblyPath);

        using (ShimContext.Create())
        {
            harness.RegisterShim<ExternalDbContext>(new FakeExternalDbContext());

            var service = harness.CreateObject(ServiceTypeName);
            var result = harness.Invoke<string>(service, "GetDisplayName", 1);

            Assert.AreEqual("fake-1", result);
        }
    }

    [TestMethod]
    public void WithExternalTarget_ByType_RewritesAndSubstitutesFake()
    {
        var externalType = typeof(ExternalDbContext);

        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget(externalType)
            .RewriteAssembly(TargetAssemblyPath);

        using (ShimContext.Create())
        {
            harness.RegisterShim(externalType, new FakeExternalDbContext());

            var service = harness.CreateObject(ServiceTypeName);
            var result = harness.Invoke<string>(service, "GetDisplayName", 7);

            Assert.AreEqual("fake-7", result);
        }
    }

    [TestMethod]
    public void ExternalNewObj_IsDetectedAndRewritten()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget<ExternalDbContext>()
            .RewriteAssembly(TargetAssemblyPath);

        var result = harness.LastRewriteResult!;

        Assert.AreEqual(1, result.RewrittenCallSiteCount,
            "Only the single ExternalDbContext newobj should be rewritten.");
        Assert.IsTrue(
            result.Diagnostics.Any(d => d.StartsWith("External newobj detected", StringComparison.Ordinal)),
            "Expected an 'External newobj detected' diagnostic.");
        Assert.IsTrue(
            result.Diagnostics.Any(d => d.StartsWith("External newobj rewritten", StringComparison.Ordinal)),
            "Expected an 'External newobj rewritten' diagnostic.");
    }

    [TestMethod]
    public void NoShimRegistered_FallsBackToRealConstructor()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget<ExternalDbContext>()
            .RewriteAssembly(TargetAssemblyPath);

        using (ShimContext.Create())
        {
            // No shim registered -> real ExternalDbContext is constructed.
            var service = harness.CreateObject(ServiceTypeName);
            var result = harness.Invoke<string>(service, "GetDisplayName", 3);

            Assert.AreEqual("real-3", result);
        }
    }

    [TestMethod]
    public void UnregisteredExternalType_IsNotRewritten()
    {
        // Only ExternalDbContext is allowlisted; ExternalOtherContext must keep its real behaviour.
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget<ExternalDbContext>()
            .RewriteAssembly(TargetAssemblyPath);

        using (ShimContext.Create())
        {
            var service = harness.CreateObject(ServiceTypeName);
            var otherTag = harness.Invoke<string>(service, "GetOtherTag");

            Assert.AreEqual("real-tag", otherTag,
                "ExternalOtherContext is not an allowlisted target and must not be rewritten.");
        }
    }

    [TestMethod]
    public void Dispatch_ResolvedByFullNameFallback()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget<ExternalDbContext>()
            .RewriteAssembly(TargetAssemblyPath);

        using (var context = ShimContext.Create())
        {
            harness.RegisterShim<ExternalDbContext>(new FakeExternalDbContext());

            var service = harness.CreateObject(ServiceTypeName);
            harness.Invoke<string>(service, "GetDisplayName", 1);

            var diag = context.LastDispatchDiagnostics;
            Assert.IsNotNull(diag);
            Assert.IsTrue(diag!.MatchFound);
            Assert.IsTrue(diag.ResolvedByFullNameFallback,
                "External rules must be resolved by the FullName fallback lookup.");
            Assert.IsFalse(diag.DuplicateFullNameRisk);
        }
    }

    [TestMethod]
    public void RewrittenAssembly_PreservesExternalAssemblyReference()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget<ExternalDbContext>()
            .RewriteAssembly(TargetAssemblyPath);

        using var module = ModuleDefinition.ReadModule(harness.OutputAssemblyPath);

        Assert.IsTrue(
            module.AssemblyReferences.Any(r =>
                string.Equals(r.Name, "ExternalLib", StringComparison.Ordinal)),
            "The rewritten assembly must still reference ExternalLib.");
    }

    [TestMethod]
    public void CreateFake_ForExternalTarget_ThrowsNotSupported()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget<ExternalDbContext>()
            .RewriteAssembly(TargetAssemblyPath);

        var ex = Assert.ThrowsException<NotSupportedException>(
            () => harness.CreateFake<ExternalDbContext>());

        StringAssert.Contains(ex.Message, "RegisterShim<T>(fake)");
    }

    [TestMethod]
    public void InternalTargetRewrite_StillWorks_AlongsideExternalApi()
    {
        // Regression guard: the internal (same-assembly) newobj path is unaffected by the
        // external-target additions.
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<Sample.UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            var fake = harness.CreateFake<Sample.UserRepository>("internal-fake");
            harness.RegisterShim<Sample.UserRepository>(fake);

            var service = harness.Create<Sample.UserService>();
            var result = harness.Invoke<string>(service, nameof(Sample.UserService.GetDisplayName), 5);

            Assert.AreEqual("internal-fake-5", result);
        }
    }
}
