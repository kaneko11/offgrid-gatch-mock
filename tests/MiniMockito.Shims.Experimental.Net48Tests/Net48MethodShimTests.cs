using System;
using System.Collections.Generic;
using System.IO;
using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    /// <summary>
    /// Phase 25 — instance method call shim on .NET Framework 4.8 (C# 7.3, using-statement form).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48MethodShimTests
    {
        private const string ServiceTypeName = "CrossAssemblySample.GatewayUserService";
        private const string GatewayTypeName = "ExternalLib.ExternalGateway";

        private static string TargetAssemblyPath
        {
            get { return typeof(CrossAssemblySample.GatewayUserService).Assembly.Location; }
        }

        private static string ExternalAssemblyPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExternalLib.dll"); }
        }

        [TestMethod]
        public void ReplaceMethod_NonVirtual_Substitutes()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath)
                .ReplaceMethod(ExternalAssemblyPath, GatewayTypeName, "GetName",
                    delegate(object receiver, object[] args) { return "fake-" + args[0]; }))
            {
                object svc = shims.CreateObject(ServiceTypeName);
                Assert.AreEqual("fake-1", shims.Invoke<string>(svc, "Run", 1));
            }
        }

        [TestMethod]
        public void ReplaceMethod_GenericMethod_Substitutes()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath)
                .ReplaceMethod(ExternalAssemblyPath, GatewayTypeName, "Query",
                    delegate(object receiver, object[] args) { return new List<GatewayItem> { new GatewayItem("fake-1") }; },
                    typeof(IEnumerable<>)))
            {
                object svc = shims.CreateObject(ServiceTypeName);
                List<GatewayItem> rows = shims.Invoke<List<GatewayItem>>(svc, "LoadRows");

                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual("fake-1", rows[0].Name);
            }
        }

        [TestMethod]
        public void ReplaceMethod_ReturnTypeSubstitution_RawResultConsumedAsIEnumerable()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath)
                .ReplaceMethod(ExternalAssemblyPath, GatewayTypeName, "RawQuery",
                    delegate(object receiver, object[] args) { return new List<GatewayItem> { new GatewayItem("fake-raw") }; },
                    typeof(IEnumerable<>)))
            {
                object svc = shims.CreateObject(ServiceTypeName);
                List<GatewayItem> rows = shims.Invoke<List<GatewayItem>>(svc, "LoadRawRows");

                Assert.AreEqual(1, rows.Count);
                Assert.AreEqual("fake-raw", rows[0].Name);
            }
        }

        [TestMethod]
        public void MethodShim_NoMatch_FallsBackToRealMethod()
        {
            using (NewInterceptionHarness harness = NewInterceptionHarness.Create()
                .WithMethodTarget(ExternalAssemblyPath, GatewayTypeName, "GetName")
                .RewriteAssembly(TargetAssemblyPath))
            {
                using (ShimContext.Create())
                {
                    object svc = harness.CreateObject(ServiceTypeName);
                    Assert.AreEqual("real-2", harness.Invoke<string>(svc, "Run", 2));
                }
            }
        }
    }
}
