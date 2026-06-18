using System;
using System.IO;
using System.Linq;
using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    /// <summary>
    /// Phase 21 — string-based external target API and diagnostics on .NET Framework 4.8.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48CrossAssemblyStringTargetTests
    {
        private const string ServiceTypeName = "CrossAssemblySample.CrossAssemblyUserService";
        private const string ExternalTypeName = "ExternalLib.ExternalDbContext";
        private const string ExternalAssemblySimpleName = "ExternalLib";

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

        private static string ExternalAssemblyPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExternalLib.dll"); }
        }

        [TestMethod]
        public void WithExternalTarget_StringBased_RegisterShim_Substitutes()
        {
            using (NewInterceptionHarness harness = NewInterceptionHarness.Create()
                .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
                .RewriteAssembly(TargetAssemblyPath))
            {
                using (ShimContext.Create())
                {
                    harness.RegisterShim(ExternalTypeName, new FakeExternalDbContext());

                    object service = harness.CreateObject(ServiceTypeName);
                    string result = harness.Invoke<string>(service, "GetDisplayName", 1);

                    Assert.AreEqual("fake-1", result);
                }
            }
        }

        [TestMethod]
        public void RegisterShim_ByFullNameAndAssemblySimpleName_Substitutes()
        {
            using (NewInterceptionHarness harness = NewInterceptionHarness.Create()
                .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
                .RewriteAssembly(TargetAssemblyPath))
            {
                using (ShimContext.Create())
                {
                    harness.RegisterShim(ExternalTypeName, ExternalAssemblySimpleName, new FakeExternalDbContext());

                    object service = harness.CreateObject(ServiceTypeName);
                    string result = harness.Invoke<string>(service, "GetDisplayName", 4);

                    Assert.AreEqual("fake-4", result);
                }
            }
        }

        [TestMethod]
        public void WithExternalTarget_NonexistentAssemblyPath_Throws()
        {
            NewInterceptionHarness harness = NewInterceptionHarness.Create();
            string missingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DoesNotExist.dll");

            Assert.ThrowsException<ShimExternalTargetException>(
                delegate { harness.WithExternalTarget(missingPath, ExternalTypeName); });

            harness.Dispose();
        }

        [TestMethod]
        public void WithExternalTarget_NonexistentTypeFullName_Throws()
        {
            NewInterceptionHarness harness = NewInterceptionHarness.Create();

            Assert.ThrowsException<ShimExternalTargetException>(
                delegate { harness.WithExternalTarget(ExternalAssemblyPath, "ExternalLib.NoSuchType"); });

            harness.Dispose();
        }

        [TestMethod]
        public void CreateFakeExternal_ByFullName_Succeeds()
        {
            using (NewInterceptionHarness harness = NewInterceptionHarness.Create()
                .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
                .RewriteAssembly(TargetAssemblyPath))
            {
                object fake = harness.CreateFakeExternal(ExternalTypeName);

                Assert.IsNotNull(fake);
                Assert.IsInstanceOfType(fake, typeof(ExternalDbContext));
            }
        }

        [TestMethod]
        public void CreateFakeExternal_SealedType_ThrowsNotSupported()
        {
            using (NewInterceptionHarness harness = NewInterceptionHarness.Create()
                .WithExternalTarget<ExternalDbContext>()
                .RewriteAssembly(TargetAssemblyPath))
            {
                Assert.ThrowsException<NotSupportedException>(
                    delegate { harness.CreateFakeExternal(typeof(SealedExternalContext)); });
            }
        }

        [TestMethod]
        public void Diagnostics_ExternalTargetRegisteredAndRegistryKey_AreRecorded()
        {
            using (NewInterceptionHarness harness = NewInterceptionHarness.Create()
                .WithExternalTarget(ExternalAssemblyPath, ExternalTypeName)
                .RewriteAssembly(TargetAssemblyPath))
            {
                Assert.IsTrue(harness.Diagnostics.Any(
                    d => d.StartsWith("External target registered:", StringComparison.Ordinal)));

                using (ShimContext.Create())
                {
                    harness.RegisterShim(ExternalTypeName, ExternalAssemblySimpleName, new FakeExternalDbContext());

                    Assert.IsTrue(harness.Diagnostics.Any(
                        d => d.StartsWith("Registry key used:", StringComparison.Ordinal)
                             && d.Contains(ExternalTypeName)));
                }
            }
        }
    }
}
