using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;
using MiniMockito.Shims.Experimental.Net48Tests.Samples;
using static MiniMockito.Shims.Experimental.ShimArg;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48ConstructorArgsTests
    {
        // =====================================================================
        // Catch-all (no matchers): matches any constructor arguments
        // =====================================================================

        [TestMethod]
        public void CtorArgs_CatchAll_WithHarness_ReturnsShim()
        {
            using (var harness = NewInterceptionHarness.Create()
                .WithTarget<Net48UserRepository>()
                .RewriteTargetTypeAssembly())
            {
                object fakeRepo = harness.CreateFake<Net48UserRepository>("catch-all-fake");

                using (ShimContext.Create())
                {
                    // Catch-all: matches new Net48UserRepository(anyString).
                    harness.RegisterShim<Net48UserRepository>(fakeRepo);

                    object service = harness.Create<Net48UserService>();
                    string result = harness.Invoke<string>(service, "GetDisplayNameWithArg", 1);

                    Assert.AreEqual("catch-all-fake-1", result,
                        "Catch-all must intercept new Net48UserRepository(\"prod\").");
                }
            }
        }

        // =====================================================================
        // Any<T> matcher: matches any non-null argument of type T
        // =====================================================================

        [TestMethod]
        public void CtorArgs_AnyMatcher_WithHarness_MatchesAnyValue()
        {
            using (var harness = NewInterceptionHarness.Create()
                .WithTarget<Net48UserRepository>()
                .RewriteTargetTypeAssembly())
            {
                object fakeRepo = harness.CreateFake<Net48UserRepository>("any-fake");

                using (ShimContext.Create())
                {
                    harness.RegisterShimWithMatchers<Net48UserRepository>(fakeRepo, Any<string>());

                    object service = harness.Create<Net48UserService>();
                    string result = harness.Invoke<string>(service, "GetDisplayNameWithArg", 7);

                    Assert.AreEqual("any-fake-7", result,
                        "Any<string>() must match \"prod\".");
                }
            }
        }

        // =====================================================================
        // Eq<T> matcher: matches only a specific value
        // =====================================================================

        [TestMethod]
        public void CtorArgs_EqMatcher_MatchesSpecificValue()
        {
            using (var harness = NewInterceptionHarness.Create()
                .WithTarget<Net48UserRepository>()
                .RewriteTargetTypeAssembly())
            {
                object prodFake = harness.CreateFake<Net48UserRepository>("eq-prod-fake");
                object catchAllFake = harness.CreateFake<Net48UserRepository>("eq-catch-all");

                using (ShimContext.Create())
                {
                    // Catch-all registered first (lowest priority).
                    harness.RegisterShimWithMatchers<Net48UserRepository>(catchAllFake);
                    // Eq("prod") registered last (highest priority).
                    harness.RegisterShimWithMatchers<Net48UserRepository>(prodFake, Eq<string>("prod"));

                    object service = harness.Create<Net48UserService>();
                    string result = harness.Invoke<string>(service, "GetDisplayNameWithArg", 3);

                    Assert.AreEqual("eq-prod-fake-3", result,
                        "Eq(\"prod\") must win over the catch-all.");
                }
            }
        }

        // =====================================================================
        // Is<T> predicate matcher: unit-level test via NewWithArgs
        // =====================================================================

        [TestMethod]
        public void CtorArgs_IsMatcher_Predicate_UnitLevel()
        {
            Net48UserRepository fakeRepo = new Net48UserRepository("is-match");

            using (ShimContext.Create())
            {
                Shim.New<Net48UserRepository>()
                    .WithArguments(Is<string>(s => s != null && s.StartsWith("prod")))
                    .Returns(fakeRepo);

                Net48UserRepository result = ShimDispatcher.NewWithArgs<Net48UserRepository>(
                    new object[] { (object)"prod-xyz" });

                Assert.AreEqual("is-match-0", result.GetName(0),
                    "Is<string>() must match a string that starts with \"prod\".");
            }
        }

        // =====================================================================
        // No-match fallback: when no rule matches, the real constructor is used
        // =====================================================================

        [TestMethod]
        public void CtorArgs_NoMatch_FallsBackToRealConstructor()
        {
            using (var harness = NewInterceptionHarness.Create()
                .WithTarget<Net48UserRepository>()
                .RewriteTargetTypeAssembly())
            {
                object neverFake = harness.CreateFake<Net48UserRepository>("never");

                using (ShimContext.Create())
                {
                    // Eq("other") does NOT match "prod".
                    harness.RegisterShimWithMatchers<Net48UserRepository>(neverFake, Eq<string>("other"));

                    object service = harness.Create<Net48UserService>();
                    string result = harness.Invoke<string>(service, "GetDisplayNameWithArg", 5);

                    Assert.AreEqual("prod-5", result,
                        "Real Net48UserRepository(\"prod\") must be used when no matcher matches.");
                }
            }
        }

        // =====================================================================
        // Last stub wins: most recently registered rule takes priority
        // =====================================================================

        [TestMethod]
        public void CtorArgs_LastStubWins_UnitLevel()
        {
            using (ShimContext.Create())
            {
                Shim.New<Net48UserRepository>().Returns(new Net48UserRepository("first"));
                Shim.New<Net48UserRepository>().Returns(new Net48UserRepository("last"));

                Net48UserRepository result = ShimDispatcher.New<Net48UserRepository>();

                Assert.AreEqual("last-0", result.GetName(0),
                    "Last registered stub must win when multiple catch-all rules exist.");
            }
        }
    }
}
