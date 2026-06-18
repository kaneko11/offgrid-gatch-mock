using System;
using System.IO;
using System.Linq;
using CrossAssemblySample;
using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    /// <summary>
    /// Phase 23 — Easy Shims API on .NET Framework 4.8 (C# 7.3, using-statement form).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48EasyShimsApiTests
    {
        private const string ServiceTypeName = "CrossAssemblySample.CrossAssemblyUserService";
        private const string ExternalDbTypeName = "ExternalLib.ExternalDbContext";

        private static string TargetAssemblyPath
        {
            get { return typeof(CrossAssemblyUserService).Assembly.Location; }
        }

        private static string ExternalAssemblyPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExternalLib.dll"); }
        }

        private sealed class FakeExternalDbContext : ExternalDbContext
        {
            private readonly string _tag;

            public FakeExternalDbContext() : this("fake")
            {
            }

            public FakeExternalDbContext(string tag)
            {
                _tag = tag;
            }

            public override string GetName(int id)
            {
                return _tag + "-" + id;
            }
        }

        private sealed class FakeExternalLogger : ExternalLogger
        {
            public override string Tag()
            {
                return "fake-log";
            }
        }

        [TestMethod]
        public void ReplaceNew_Generic_ExternalSubstitutes()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath)
                .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext()))
            {
                object service = shims.CreateObject(ServiceTypeName);
                Assert.AreEqual("fake-1", shims.Invoke<string>(service, "GetDisplayName", 1));
            }
        }

        [TestMethod]
        public void ReplaceNew_StringBased_ExternalSubstitutes()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath)
                .ReplaceNew(ExternalAssemblyPath, ExternalDbTypeName, new FakeExternalDbContext()))
            {
                object service = shims.CreateObject(ServiceTypeName);
                Assert.AreEqual("fake-4", shims.Invoke<string>(service, "GetDisplayName", 4));
            }
        }

        [TestMethod]
        public void ReplaceNew_TwoExternalTargets_InOneSession()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath)
                .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext())
                .ReplaceNew<ExternalLogger>(new FakeExternalLogger()))
            {
                object service = shims.CreateObject(ServiceTypeName);
                Assert.AreEqual("real(fake-1|fake-log)", shims.Invoke<string>(service, "Run", 1));
            }
        }

        [TestMethod]
        public void ReplaceNew_MixedInternalAndExternal_InOneSession()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath)
                .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext())
                .ReplaceNew<InternalGreeter>(delegate(Shims s) { return s.CreateFake<InternalGreeter>("gfake"); }))
            {
                object service = shims.CreateObject(ServiceTypeName);
                Assert.AreEqual("gfake(fake-1|real-log)", shims.Invoke<string>(service, "Run", 1));
            }
        }

        [TestMethod]
        public void ReplaceNew_SameTargetTwice_LastStubWins()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath)
                .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext("first"))
                .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext("last")))
            {
                object service = shims.CreateObject(ServiceTypeName);
                Assert.AreEqual("last-1", shims.Invoke<string>(service, "GetDisplayName", 1));
            }
        }

        [TestMethod]
        public void ReplaceNew_AfterRewriteFinalized_Throws()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath)
                .ReplaceNew<ExternalDbContext>(new FakeExternalDbContext()))
            {
                object service = shims.CreateObject(ServiceTypeName);
                Assert.IsNotNull(service);

                Assert.ThrowsException<InvalidOperationException>(
                    delegate { shims.ReplaceNew<ExternalLogger>(new FakeExternalLogger()); });
            }
        }

        [TestMethod]
        public void Diagnostics_AreForwarded_AndContextCleanedUp()
        {
            int before = ShimContext.ActiveContextCount;

            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath)
                .ReplaceNew(ExternalAssemblyPath, ExternalDbTypeName, new FakeExternalDbContext()))
            {
                object service = shims.CreateObject(ServiceTypeName);
                Assert.AreEqual("fake-1", shims.Invoke<string>(service, "GetDisplayName", 1));

                Assert.IsTrue(shims.Diagnostics.Any(
                    delegate(string d) { return d.StartsWith("External target registered:", StringComparison.Ordinal); }));
            }

            Assert.AreEqual(before, ShimContext.ActiveContextCount);
        }
    }
}
