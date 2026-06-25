using System.Collections.Generic;
using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

/// <summary>
/// Phase 25 — instance method call shim (call-site rewrite).
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class MethodShimTests
{
    private const string ServiceTypeName = "CrossAssemblySample.GatewayUserService";
    private const string GatewayTypeName = "ExternalLib.ExternalGateway";

    private static string TargetAssemblyPath =>
        typeof(CrossAssemblySample.GatewayUserService).Assembly.Location;

    private static string ExternalAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "ExternalLib.dll");

    [TestMethod]
    public void ReplaceMethod_NonVirtual_Substitutes()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceMethod(ExternalAssemblyPath, GatewayTypeName, "GetName", (receiver, args) => "fake-" + args[0]))
        {
            var svc = shims.CreateObject(ServiceTypeName);
            Assert.AreEqual("fake-1", shims.Invoke<string>(svc, "Run", 1));
        }
    }

    [TestMethod]
    public void ReplaceMethod_GenericMethod_Substitutes()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceMethod(ExternalAssemblyPath, GatewayTypeName, "Query",
                (receiver, args) => new List<GatewayItem> { new GatewayItem("fake-1") },
                typeof(IEnumerable<>)))
        {
            var svc = shims.CreateObject(ServiceTypeName);
            var rows = shims.Invoke<List<GatewayItem>>(svc, "LoadRows");

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("fake-1", rows[0].Name);
        }
    }

    [TestMethod]
    public void ReplaceMethod_ReturnTypeSubstitution_RawResultConsumedAsIEnumerable()
    {
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceMethod(ExternalAssemblyPath, GatewayTypeName, "RawQuery",
                (receiver, args) => new List<GatewayItem> { new GatewayItem("fake-raw") },
                typeof(IEnumerable<>)))
        {
            var svc = shims.CreateObject(ServiceTypeName);
            var rows = shims.Invoke<List<GatewayItem>>(svc, "LoadRawRows");

            // The real RawQuery throws; a value here proves the shim replaced the call (return substituted).
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("fake-raw", rows[0].Name);
        }
    }

    [TestMethod]
    public void MethodShim_InternalVirtualMethod_Substitutes()
    {
        // CrossAssemblyUserService.Run calls internal InternalGreeter.Decorate (virtual) in the target assembly.
        using (var shims = Shims.ForAssembly(TargetAssemblyPath)
            .ReplaceMethod<CrossAssemblySample.InternalGreeter>("Decorate", (receiver, args) => "INT[" + args[0] + "]"))
        {
            var svc = shims.CreateObject("CrossAssemblySample.CrossAssemblyUserService");
            var result = shims.Invoke<string>(svc, "Run", 1);

            // db/logger are not shimmed (real-1 / real-log); only Decorate is replaced.
            Assert.AreEqual("INT[real-1|real-log]", result);
        }
    }

    [TestMethod]
    public void MethodShim_NoMatch_FallsBackToRealMethod()
    {
        // Declare the GetName target but register NO shim -> wrapper falls back to the real method.
        using var harness = NewInterceptionHarness.Create()
            .WithMethodTarget(ExternalAssemblyPath, GatewayTypeName, "GetName")
            .RewriteAssembly(TargetAssemblyPath);

        using (ShimContext.Create())
        {
            var svc = harness.CreateObject(ServiceTypeName);
            Assert.AreEqual("real-2", harness.Invoke<string>(svc, "Run", 2));
        }
    }

    [TestMethod]
    public void MethodShim_GenericWithoutSubstitute_IsSkippedWithDiagnostic()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithMethodTarget(ExternalAssemblyPath, GatewayTypeName, "Query") // generic, no substitute -> skipped
            .RewriteAssembly(TargetAssemblyPath);

        Assert.IsTrue(
            harness.LastRewriteResult!.Diagnostics.Any(d =>
                d.StartsWith("Method call site skipped", System.StringComparison.Ordinal)
                && d.Contains("requires a return interface")),
            "Expected a skip diagnostic for a generic method without a return interface.");
    }

    [TestMethod]
    public void MethodShim_Diagnostics_AreRecorded()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithMethodTarget(ExternalAssemblyPath, GatewayTypeName, "GetName")
            .RewriteAssembly(TargetAssemblyPath);

        var diags = harness.LastRewriteResult!.Diagnostics;
        Assert.IsTrue(diags.Any(d => d.StartsWith("Method shim target registered", System.StringComparison.Ordinal)));
        Assert.IsTrue(diags.Any(d => d.StartsWith("Method call site detected", System.StringComparison.Ordinal)));
        Assert.IsTrue(diags.Any(d => d.StartsWith("Method call site rewritten", System.StringComparison.Ordinal)));
    }
}
