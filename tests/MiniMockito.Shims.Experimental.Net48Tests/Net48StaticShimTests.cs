using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;
using MiniMockito.Shims.Experimental.Net48Tests.Samples;
using static MiniMockito.Shims.Experimental.ShimArg;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48StaticShimTests
    {
        // =====================================================================
        // Pattern — Non-void static method with harness (IL rewrite)
        // =====================================================================

        [TestMethod]
        public void Static_NonVoid_WithHarness_ReturnsShimmedValue()
        {
            using (var harness = NewInterceptionHarness.Create()
                .WithStaticTarget(typeof(Net48StaticClock))
                .RewriteTargetTypeAssembly())
            {
                using (ShimContext.Create())
                {
                    Shim.Static<string>(
                            typeof(Net48StaticClock).FullName,
                            "GetLabel",
                            typeof(int))
                        .Returns("shimmed-label");

                    object service = harness.Create<Net48TimedService>();
                    string result = harness.Invoke<string>(service, "GetLabel", 5);

                    Assert.AreEqual("shimmed-label", result,
                        "Net48StaticClock.GetLabel() must return the shimmed value.");
                }
            }
        }

        [TestMethod]
        public void Static_EqMatcher_WithHarness_MatchesAndFallsBack()
        {
            using (var harness = NewInterceptionHarness.Create()
                .WithStaticTarget(typeof(Net48StaticClock))
                .RewriteTargetTypeAssembly())
            {
                using (ShimContext.Create())
                {
                    Shim.Static<string>(
                            typeof(Net48StaticClock).FullName,
                            "GetLabel",
                            typeof(int))
                        .WithArguments(Eq(10))
                        .Returns("eq-label-10");

                    object service = harness.Create<Net48TimedService>();

                    string matchResult = harness.Invoke<string>(service, "GetLabel", 10);
                    Assert.AreEqual("eq-label-10", matchResult, "Eq(10) must match id=10.");

                    string noMatchResult = harness.Invoke<string>(service, "GetLabel", 99);
                    Assert.AreEqual("real-99", noMatchResult, "No match must fall back to real method.");
                }
            }
        }

        [TestMethod]
        public void Static_AnyMatcher_WithHarness_MatchesAnyArg()
        {
            using (var harness = NewInterceptionHarness.Create()
                .WithStaticTarget(typeof(Net48StaticClock))
                .RewriteTargetTypeAssembly())
            {
                using (ShimContext.Create())
                {
                    Shim.Static<string>(
                            typeof(Net48StaticClock).FullName,
                            "GetLabel",
                            typeof(int))
                        .WithArguments(Any<int>())
                        .Returns("any-label");

                    object service = harness.Create<Net48TimedService>();

                    string r1 = harness.Invoke<string>(service, "GetLabel", 1);
                    string r2 = harness.Invoke<string>(service, "GetLabel", 200);

                    Assert.AreEqual("any-label", r1);
                    Assert.AreEqual("any-label", r2);
                }
            }
        }

        [TestMethod]
        public void Static_NoMatch_FallsBackToReal_WithHarness()
        {
            using (var harness = NewInterceptionHarness.Create()
                .WithStaticTarget(typeof(Net48StaticClock))
                .RewriteTargetTypeAssembly())
            {
                using (ShimContext.Create())
                {
                    // No shim registered — real method must be called.
                    object service = harness.Create<Net48TimedService>();
                    string result = harness.Invoke<string>(service, "GetLabel", 3);

                    Assert.AreEqual("real-3", result,
                        "With no shim registered, the real static method must execute.");
                }
            }
        }

        // =====================================================================
        // Pattern — Void static method (unit-level, no harness)
        // =====================================================================

        [TestMethod]
        public void Static_VoidMethod_Callback_UnitLevel()
        {
            List<string> recorded = new List<string>();

            using (ShimContext.Create())
            {
                Shim.Static(
                        typeof(Net48StaticClock).FullName,
                        "RecordCall",
                        typeof(string))
                    .Callback(args => recorded.Add((string)args[0]));

                bool found = StaticShimDispatcher.TryInvokeVoid(
                    typeof(Net48StaticClock).FullName,
                    "RecordCall",
                    new System.Type[] { typeof(string) },
                    new object[] { (object)"hello-net48" });

                Assert.IsTrue(found, "Void shim rule must signal handled=true.");
            }

            Assert.AreEqual(1, recorded.Count, "Callback must have been invoked once.");
            Assert.AreEqual("hello-net48", recorded[0]);
        }

        [TestMethod]
        public void Static_VoidMethod_DoNothing_UnitLevel()
        {
            using (ShimContext.Create())
            {
                Shim.Static(
                        typeof(Net48StaticClock).FullName,
                        "RecordCall",
                        typeof(string))
                    .DoNothing();

                bool found = StaticShimDispatcher.TryInvokeVoid(
                    typeof(Net48StaticClock).FullName,
                    "RecordCall",
                    new System.Type[] { typeof(string) },
                    new object[] { (object)"suppressed" });

                Assert.IsTrue(found, "DoNothing shim must signal that the call was handled.");
            }
        }

        // =====================================================================
        // Pattern — Last stub wins
        // =====================================================================

        [TestMethod]
        public void Static_LastStubWins_UnitLevel()
        {
            using (ShimContext.Create())
            {
                Shim.Static<string>(
                        typeof(Net48StaticClock).FullName,
                        "GetLabel",
                        typeof(int))
                    .Returns("first-stub");

                Shim.Static<string>(
                        typeof(Net48StaticClock).FullName,
                        "GetLabel",
                        typeof(int))
                    .Returns("last-stub");

                bool found = StaticShimDispatcher.TryInvoke<string>(
                    typeof(Net48StaticClock).FullName,
                    "GetLabel",
                    new System.Type[] { typeof(int) },
                    new object[] { (object)1 },
                    out string result);

                Assert.IsTrue(found);
                Assert.AreEqual("last-stub", result, "Last registered stub must win.");
            }
        }

        // =====================================================================
        // Pattern — No rule registered returns false
        // =====================================================================

        [TestMethod]
        public void Static_NoRule_TryInvoke_ReturnsFalse()
        {
            using (ShimContext.Create())
            {
                bool found = StaticShimDispatcher.TryInvoke<string>(
                    typeof(Net48StaticClock).FullName,
                    "GetLabel",
                    new System.Type[] { typeof(int) },
                    new object[] { (object)1 },
                    out string result);

                Assert.IsFalse(found, "No registered rule must return false.");
            }
        }
    }
}
