using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;
using MiniMockito.Shims.Experimental.Net48Tests.Samples;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48RegressionTests
    {
        // =====================================================================
        // Original assembly must never be modified by the rewriter
        // =====================================================================

        [TestMethod]
        public void OriginalAssembly_IsNeverModified_NewTarget()
        {
            string originalPath = typeof(Net48UserRepository).Assembly.Location;
            DateTime beforeWrite = File.GetLastWriteTimeUtc(originalPath);

            using (var harness = NewInterceptionHarness.Create()
                .WithTarget<Net48UserRepository>()
                .RewriteTargetTypeAssembly())
            {
                DateTime afterWrite = File.GetLastWriteTimeUtc(originalPath);

                Assert.AreEqual(beforeWrite, afterWrite,
                    "The original assembly must not be touched by the rewriter.");
                Assert.IsFalse(
                    string.Equals(originalPath, harness.OutputAssemblyPath, StringComparison.OrdinalIgnoreCase),
                    "The rewritten output path must differ from the original assembly path.");
            }
        }

        [TestMethod]
        public void OriginalAssembly_IsNeverModified_StaticTarget()
        {
            string originalPath = typeof(Net48StaticClock).Assembly.Location;
            DateTime beforeWrite = File.GetLastWriteTimeUtc(originalPath);

            using (var harness = NewInterceptionHarness.Create()
                .WithStaticTarget(typeof(Net48StaticClock))
                .RewriteTargetTypeAssembly())
            {
                DateTime afterWrite = File.GetLastWriteTimeUtc(originalPath);

                Assert.AreEqual(beforeWrite, afterWrite,
                    "The original assembly must not be modified by the static call rewrite.");
                Assert.IsFalse(
                    string.Equals(originalPath, harness.OutputAssemblyPath, StringComparison.OrdinalIgnoreCase),
                    "The rewritten output path must differ from the original assembly path.");
            }
        }

        // =====================================================================
        // ShimContext.Dispose clears rules — no rule leaks between tests
        // =====================================================================

        [TestMethod]
        public void ShimContext_Dispose_ClearsNewRules()
        {
            using (ShimContext.Create())
            {
                Shim.New<Net48UserRepository>().Returns(new Net48UserRepository("ctx-test"));
                Net48UserRepository r1 = ShimDispatcher.New<Net48UserRepository>();
                Assert.AreEqual("ctx-test-0", r1.GetName(0));
            }

            Net48UserRepository r2 = ShimDispatcher.New<Net48UserRepository>();
            Assert.AreEqual("real-0", r2.GetName(0),
                "Shim rules must be cleared after context disposal.");
        }

        // =====================================================================
        // Newobj shim and static shim coexist in the same harness and context
        // =====================================================================

        [TestMethod]
        public void NewAndStatic_Coexist_InSameHarness()
        {
            using (var harness = NewInterceptionHarness.Create()
                .WithTarget<Net48UserRepository>()
                .WithStaticTarget(typeof(Net48StaticClock))
                .RewriteTargetTypeAssembly())
            {
                object fakeRepo = harness.CreateFake<Net48UserRepository>("coexist-fake");

                using (ShimContext.Create())
                {
                    // newobj shim
                    harness.RegisterShim<Net48UserRepository>(fakeRepo);

                    // static shim
                    Shim.Static<string>(
                            typeof(Net48StaticClock).FullName,
                            "GetLabel",
                            typeof(int))
                        .Returns("coexist-label");

                    // --- verify newobj shim ---
                    object userService = harness.Create<Net48UserService>();
                    string nameResult = harness.Invoke<string>(userService, "GetDisplayName", 7);
                    Assert.AreEqual("coexist-fake-7", nameResult,
                        "newobj shim must work alongside static shim.");

                    // --- verify static shim ---
                    object timedService = harness.Create<Net48TimedService>();
                    string labelResult = harness.Invoke<string>(timedService, "GetLabel", 3);
                    Assert.AreEqual("coexist-label", labelResult,
                        "static shim must work alongside newobj shim.");
                }
            }
        }
    }
}
