using System;
using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    /// <summary>
    /// Phase 20 — cross-assembly newobj interception on .NET Framework 4.8.
    /// The rewrite target is CrossAssemblySample.dll; the intercepted newobj declaring type
    /// (ExternalLib.ExternalDbContext) lives in ExternalLib.dll.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48CrossAssemblyTests
    {
        private const string ServiceTypeName = "CrossAssemblySample.CrossAssemblyUserService";

        private sealed class FakeExternalDbContext : ExternalDbContext
        {
            public override string GetName(int id)
            {
                return "fake-" + id;
            }
        }

        private static string TargetAssemblyPath
        {
            get { return typeof(CrossAssemblySample.CrossAssemblyUserService).Assembly.Location; }
        }

        [TestMethod]
        public void WithExternalTarget_Generic_SubstitutesFake()
        {
            using (NewInterceptionHarness harness = NewInterceptionHarness.Create()
                .WithExternalTarget<ExternalDbContext>()
                .RewriteAssembly(TargetAssemblyPath))
            {
                using (ShimContext.Create())
                {
                    harness.RegisterShim<ExternalDbContext>(new FakeExternalDbContext());

                    object service = harness.CreateObject(ServiceTypeName);
                    string result = harness.Invoke<string>(service, "GetDisplayName", 1);

                    Assert.AreEqual("fake-1", result);
                }
            }
        }

        [TestMethod]
        public void WithExternalTarget_ByType_SubstitutesFake()
        {
            Type externalType = typeof(ExternalDbContext);

            using (NewInterceptionHarness harness = NewInterceptionHarness.Create()
                .WithExternalTarget(externalType)
                .RewriteAssembly(TargetAssemblyPath))
            {
                using (ShimContext.Create())
                {
                    harness.RegisterShim(externalType, new FakeExternalDbContext());

                    object service = harness.CreateObject(ServiceTypeName);
                    string result = harness.Invoke<string>(service, "GetDisplayName", 9);

                    Assert.AreEqual("fake-9", result);
                }
            }
        }

        [TestMethod]
        public void NoShimRegistered_FallsBackToRealConstructor()
        {
            using (NewInterceptionHarness harness = NewInterceptionHarness.Create()
                .WithExternalTarget<ExternalDbContext>()
                .RewriteAssembly(TargetAssemblyPath))
            {
                using (ShimContext.Create())
                {
                    object service = harness.CreateObject(ServiceTypeName);
                    string result = harness.Invoke<string>(service, "GetDisplayName", 2);

                    Assert.AreEqual("real-2", result);
                }
            }
        }

        [TestMethod]
        public void UnregisteredExternalType_IsNotRewritten()
        {
            using (NewInterceptionHarness harness = NewInterceptionHarness.Create()
                .WithExternalTarget<ExternalDbContext>()
                .RewriteAssembly(TargetAssemblyPath))
            {
                using (ShimContext.Create())
                {
                    object service = harness.CreateObject(ServiceTypeName);
                    string tag = harness.Invoke<string>(service, "GetOtherTag");

                    Assert.AreEqual("real-tag", tag);
                }
            }
        }

        [TestMethod]
        public void CreateFake_ForExternalTarget_ThrowsNotSupported()
        {
            using (NewInterceptionHarness harness = NewInterceptionHarness.Create()
                .WithExternalTarget<ExternalDbContext>()
                .RewriteAssembly(TargetAssemblyPath))
            {
                Assert.ThrowsException<NotSupportedException>(
                    delegate { harness.CreateFake<ExternalDbContext>(); });
            }
        }
    }
}
