using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;
using MiniMockito.Shims.Experimental.Net48Tests.Samples;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48NewObjShimTests
    {
        // =====================================================================
        // Pattern 1 — Unit-level dispatcher test (no harness)
        //
        // Shim.New<T>() without a rewritten assembly.
        // Tests the dispatcher directly, without IL rewrite.
        // =====================================================================

        [TestMethod]
        public void ParameterlessNew_UnitLevel_ReturnsRegisteredFake()
        {
            Net48UserRepository fakeRepo = new Net48UserRepository("net48-unit-fake");

            using (ShimContext.Create())
            {
                Shim.New<Net48UserRepository>().Returns(fakeRepo);

                Net48UserRepository result = ShimDispatcher.New<Net48UserRepository>();

                Assert.AreSame(fakeRepo, result,
                    "ShimDispatcher.New<T>() must return the registered fake instance.");
            }
        }

        [TestMethod]
        public void ParameterlessNew_AfterContextDispose_UsesRealConstructor()
        {
            using (ShimContext.Create())
            {
                Shim.New<Net48UserRepository>().Returns(new Net48UserRepository("tmp"));
            }

            Net48UserRepository realResult = ShimDispatcher.New<Net48UserRepository>();
            Assert.AreEqual("real-0", realResult.GetName(0),
                "After context disposal, the real constructor must be used.");
        }

        // =====================================================================
        // Pattern 2 — Full harness workflow (with IL rewrite + LoadFrom)
        //
        // Demonstrates the complete recommended workflow on net48:
        //   1. Create a harness and specify which types to intercept.
        //   2. RewriteTargetTypeAssembly() — writes a copy to a temp dir.
        //   3. Load the rewritten assembly via Assembly.LoadFrom.
        //   4. Inside a ShimContext, register shim rules.
        //   5. Create service/fake from the rewritten assembly via reflection.
        //   6. Invoke methods via harness.Invoke<T>().
        // =====================================================================

        [TestMethod]
        public void WithHarness_ParameterlessNew_FullWorkflow()
        {
            using (var harness = NewInterceptionHarness.Create()
                .WithTarget<Net48UserRepository>()
                .RewriteTargetTypeAssembly())
            {
                Assert.IsFalse(
                    string.Equals(
                        typeof(Net48UserRepository).Assembly.Location,
                        harness.OutputAssemblyPath,
                        StringComparison.OrdinalIgnoreCase),
                    "Rewritten assembly path must differ from the original.");

                object fakeRepo = harness.CreateFake<Net48UserRepository>("net48-harness-fake");

                using (ShimContext.Create())
                {
                    harness.RegisterShim<Net48UserRepository>(fakeRepo);

                    object service = harness.Create<Net48UserService>();
                    string result = harness.Invoke<string>(service, "GetDisplayName", 1);

                    Assert.AreEqual("net48-harness-fake-1", result,
                        "The shim must intercept `new Net48UserRepository()` inside Net48UserService.");
                }
            }
        }
    }
}
