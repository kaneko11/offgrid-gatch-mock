using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

/// <summary>
/// Phase 21 — string-based external target API and diagnostics.
/// Verifies that cross-assembly <c>newobj</c> interception works without a compile-time reference
/// to the external type (by assembly path + type full name), and that diagnostics are observable.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CrossAssemblyStringTargetTests
{
    private const string ServiceTypeName = "CrossAssemblySample.CrossAssemblyUserService";
    private const string ExternalTypeName = "ExternalLib.ExternalDbContext";
    private const string ExternalAssemblySimpleName = "ExternalLib";

    /// <summary>Hand-written fake for the external type.</summary>
    private sealed class FakeExternalDbContext : ExternalDbContext
    {
        public override string GetName(int id) => "fake-" + id;
    }

    private static string TargetAssemblyPath =>
        typeof(CrossAssemblySample.CrossAssemblyUserService).Assembly.Location;

    // Resolve the external assembly purely by path (as a caller without a compile-time reference would).
    private static string ExternalAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "ExternalLib.dll");

    [TestMethod]
    public void WithExternalTarget_StringBased_RegistersAndSubstitutes()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
            .RewriteAssembly(TargetAssemblyPath);

        using (ShimContext.Create())
        {
            harness.RegisterShim(ExternalTypeName, new FakeExternalDbContext());

            var service = harness.CreateObject(ServiceTypeName);
            var result = harness.Invoke<string>(service, "GetDisplayName", 1);

            Assert.AreEqual("fake-1", result);
        }
    }

    [TestMethod]
    public void WithExternalTarget_NonexistentAssemblyPath_ThrowsClearException()
    {
        var harness = NewInterceptionHarness.Create();
        var missingPath = Path.Combine(AppContext.BaseDirectory, "DoesNotExist.dll");

        var ex = Assert.ThrowsException<ShimExternalTargetException>(
            () => harness.WithExternalTarget(missingPath, ExternalTypeName));

        StringAssert.Contains(ex.Message, missingPath);
        StringAssert.Contains(ex.Message, ExternalTypeName);
        StringAssert.Contains(ex.Message, "ExternalAssemblyFileNotFound");

        harness.Dispose();
    }

    [TestMethod]
    public void WithExternalTarget_NonexistentTypeFullName_ThrowsClearException()
    {
        var harness = NewInterceptionHarness.Create();

        var ex = Assert.ThrowsException<ShimExternalTargetException>(
            () => harness.WithExternalTarget(ExternalAssemblyPath, "ExternalLib.NoSuchType"));

        StringAssert.Contains(ex.Message, "ExternalLib.NoSuchType");
        StringAssert.Contains(ex.Message, "ExternalTypeNotFound");

        harness.Dispose();
    }

    [TestMethod]
    public void RegisterShim_ByFullName_Substitutes()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
            .RewriteAssembly(TargetAssemblyPath);

        using (ShimContext.Create())
        {
            harness.RegisterShim(ExternalTypeName, new FakeExternalDbContext());

            var service = harness.CreateObject(ServiceTypeName);
            Assert.AreEqual("fake-5", harness.Invoke<string>(service, "GetDisplayName", 5));
        }
    }

    [TestMethod]
    public void RegisterShim_ByFullNameAndAssemblySimpleName_Substitutes()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
            .RewriteAssembly(TargetAssemblyPath);

        using (ShimContext.Create())
        {
            harness.RegisterShim(ExternalTypeName, ExternalAssemblySimpleName, new FakeExternalDbContext());

            var service = harness.CreateObject(ServiceTypeName);
            Assert.AreEqual("fake-8", harness.Invoke<string>(service, "GetDisplayName", 8));
        }
    }

    [TestMethod]
    public void CreateFakeExternal_ByType_Succeeds()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget<ExternalDbContext>()
            .RewriteAssembly(TargetAssemblyPath);

        var fake = harness.CreateFakeExternal(typeof(ExternalDbContext));

        Assert.IsNotNull(fake);
        Assert.IsInstanceOfType(fake, typeof(ExternalDbContext));
    }

    [TestMethod]
    public void CreateFakeExternal_ByFullName_Succeeds()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
            .RewriteAssembly(TargetAssemblyPath);

        var fake = harness.CreateFakeExternal(ExternalTypeName);

        Assert.IsNotNull(fake);
        Assert.IsInstanceOfType(fake, typeof(ExternalDbContext));
    }

    [TestMethod]
    public void CreateFakeExternal_SealedType_ThrowsNotSupported()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget<ExternalDbContext>()
            .RewriteAssembly(TargetAssemblyPath);

        var ex = Assert.ThrowsException<NotSupportedException>(
            () => harness.CreateFakeExternal(typeof(SealedExternalContext)));

        StringAssert.Contains(ex.Message, "SealedTypeNotSupported");
    }

    [TestMethod]
    public void CreateFakeExternal_NoParameterlessCtor_ThrowsNotSupported()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget<ExternalDbContext>()
            .RewriteAssembly(TargetAssemblyPath);

        var ex = Assert.ThrowsException<NotSupportedException>(
            () => harness.CreateFakeExternal(typeof(NoDefaultCtorContext)));

        StringAssert.Contains(ex.Message, "PublicParameterlessConstructorNotFound");
    }

    [TestMethod]
    public void Diagnostics_ExternalTargetRegistered_IsRecorded()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
            .RewriteAssembly(TargetAssemblyPath);

        Assert.IsTrue(
            harness.Diagnostics.Any(d =>
                d.StartsWith("External target registered:", StringComparison.Ordinal)
                && d.Contains(ExternalTypeName)),
            "Expected an 'External target registered' diagnostic.");
        Assert.IsTrue(
            harness.Diagnostics.Any(d => d.StartsWith("Type resolution: success", StringComparison.Ordinal)),
            "Expected a 'Type resolution: success' diagnostic.");
        Assert.IsTrue(
            harness.Diagnostics.Any(d => d.StartsWith("Target assembly being rewritten:", StringComparison.Ordinal)),
            "Expected a 'Target assembly being rewritten' diagnostic.");
    }

    [TestMethod]
    public void Diagnostics_ExternalNewObjRewritten_IsRecorded()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
            .RewriteAssembly(TargetAssemblyPath);

        Assert.IsTrue(
            harness.LastRewriteResult!.Diagnostics.Any(d =>
                d.StartsWith("External newobj rewritten:", StringComparison.Ordinal)),
            "Expected an 'External newobj rewritten' diagnostic.");
    }

    [TestMethod]
    public void Diagnostics_SkippedReason_IsRecordedForUnsupportedExternalCtor()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget<ExternalByRefContext>()
            .RewriteAssembly(TargetAssemblyPath);

        var skip = harness.LastRewriteResult!.Diagnostics
            .FirstOrDefault(d => d.StartsWith("External newobj skipped:", StringComparison.Ordinal));

        Assert.IsNotNull(skip, "Expected an 'External newobj skipped' diagnostic.");
        StringAssert.Contains(skip!, "Skipped reason:");
        StringAssert.Contains(skip!, "by-ref parameter is not supported");
    }

    [TestMethod]
    public void Diagnostics_RegistryKey_IsRecordedOnRegister()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
            .RewriteAssembly(TargetAssemblyPath);

        using (ShimContext.Create())
        {
            harness.RegisterShim(ExternalTypeName, ExternalAssemblySimpleName, new FakeExternalDbContext());

            Assert.IsTrue(
                harness.Diagnostics.Any(d =>
                    d.StartsWith("Registry key used:", StringComparison.Ordinal)
                    && d.Contains(ExternalTypeName)
                    && d.Contains(ExternalAssemblySimpleName)),
                "Expected a 'Registry key used' diagnostic with the FullName and assembly simple name.");
        }
    }

    [TestMethod]
    public void Diagnostics_DuplicateFullNameRisk_IsRecorded()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
            .RewriteAssembly(TargetAssemblyPath);

        using (ShimContext.Create())
        {
            // Same FullName registered under two different assembly simple names -> ambiguity risk.
            harness.RegisterShim(ExternalTypeName, ExternalAssemblySimpleName, new FakeExternalDbContext());
            harness.RegisterShim(ExternalTypeName, "SomeOtherAssembly", new FakeExternalDbContext());

            Assert.IsTrue(
                harness.Diagnostics.Any(d =>
                    d.StartsWith("Duplicate FullName risk:", StringComparison.Ordinal)),
                "Expected a 'Duplicate FullName risk' diagnostic.");
        }
    }
}
