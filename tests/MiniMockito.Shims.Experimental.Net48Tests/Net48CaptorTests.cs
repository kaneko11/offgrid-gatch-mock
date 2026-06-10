using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;
using MiniMockito.Shims.Experimental.Net48Tests.Samples;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48CaptorTests
    {
        // =====================================================================
        // ShimCaptor — captures constructor argument passed to new T(args)
        // =====================================================================

        [TestMethod]
        public void Captor_CapturesConstructorArgument_WithHarness()
        {
            ShimCaptor<string> captor = ShimCaptor.For<string>();

            using (var harness = NewInterceptionHarness.Create()
                .WithTarget<Net48UserRepository>()
                .RewriteTargetTypeAssembly())
            {
                object fakeRepo = harness.CreateFake<Net48UserRepository>("captor-fake");

                using (ShimContext.Create())
                {
                    // Captor acts as a matcher AND captures the argument.
                    harness.RegisterShimWithMatchers<Net48UserRepository>(fakeRepo, captor);

                    object service = harness.Create<Net48UserService>();
                    // GetDisplayNameWithArg calls new Net48UserRepository("prod").
                    harness.Invoke<string>(service, "GetDisplayNameWithArg", 1);
                }
            }

            Assert.IsTrue(captor.HasValue, "Captor must have captured the argument.");
            Assert.AreEqual("prod", captor.Value,
                "The captured value must be \"prod\" — the string passed to new Net48UserRepository(\"prod\").");
        }

        // =====================================================================
        // ShimCaptor — captures argument for static method dispatch
        // =====================================================================

        [TestMethod]
        public void Captor_StaticArg_CapturesArgument_UnitLevel()
        {
            ShimCaptor<int> captor = ShimCaptor.For<int>();

            using (ShimContext.Create())
            {
                Shim.Static<string>(
                        typeof(Net48StaticClock).FullName,
                        "GetLabel",
                        typeof(int))
                    .WithArguments(captor)
                    .Returns("captured-label");

                bool found = StaticShimDispatcher.TryInvoke<string>(
                    typeof(Net48StaticClock).FullName,
                    "GetLabel",
                    new System.Type[] { typeof(int) },
                    new object[] { (object)42 },
                    out string result);

                Assert.IsTrue(found, "The static shim rule must be found.");
                Assert.AreEqual("captured-label", result);
            }

            Assert.IsTrue(captor.HasValue, "Captor must have captured the int argument.");
            Assert.AreEqual(42, captor.Value, "Captured value must be 42.");
        }
    }
}
