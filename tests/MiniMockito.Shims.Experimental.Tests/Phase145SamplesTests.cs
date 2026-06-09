using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental.Sample;
using static MiniMockito.Shims.Experimental.ShimArg;

namespace MiniMockito.Shims.Experimental.Tests;

/// <summary>
/// Phase 14.5 — executable sample tests.
///
/// Each test method demonstrates one usage pattern and doubles as living documentation.
/// Run these tests with `dotnet test` to verify the patterns compile and pass.
///
/// <b>Constraint:</b> Parallel test execution is disabled at the assembly level via
/// <c>[assembly: DoNotParallelize]</c> in AssemblyInfo.cs.
/// Never remove that attribute — the shim dispatcher uses process-wide state.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class Phase145SamplesTests
{
    // =========================================================================
    // Pattern 1 — Parameterless constructor new shim
    //
    // When a method under test calls `new UserRepository()`, the shim returns
    // a pre-configured fake instance instead.
    // =========================================================================

    [TestMethod]
    public void Sample_NewShim_ParameterlessConstructor()
    {
        // Arrange — create a fake repository with a known name prefix.
        var fakeRepo = new UserRepository("fake");

        using (ShimContext.Create())
        {
            // Register: intercept every `new UserRepository()` and return fakeRepo.
            Shim.New<UserRepository>().Returns(fakeRepo);

            // Act — call the shim dispatcher directly (or let the rewritten assembly call it).
            var result = ShimDispatcher.New<UserRepository>();

            // Assert
            Assert.AreSame(fakeRepo, result,
                "New<T>() must return the registered fake instance.");
        }

        // After the context is disposed, the rule is removed automatically.
        var realResult = ShimDispatcher.New<UserRepository>();
        Assert.AreEqual("real", realResult.GetName(0).Split('-')[0],
            "No shim active after context disposal — real constructor must be used.");
    }

    // =========================================================================
    // Pattern 2 — Constructor arguments new shim
    //
    // `new UserRepository("prod")` is intercepted when you register a rule for
    // any constructor argument (no WithArguments) or for a specific argument value.
    // =========================================================================

    [TestMethod]
    public void Sample_NewShim_ConstructorArguments_CatchAll()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var fakeRepo = harness.CreateFake<UserRepository>("args-fake");

        using (ShimContext.Create())
        {
            // Catch-all: matches new UserRepository(anyString).
            harness.RegisterShim<UserRepository>(fakeRepo);

            var service = harness.Create<UserService>();
            // GetDisplayNameWithArgRepository calls new UserRepository("prod").
            var result = harness.Invoke<string>(
                service, nameof(UserService.GetDisplayNameWithArgRepository), 1);

            Assert.AreEqual("args-fake-1", result);
        }
    }

    // =========================================================================
    // Pattern 3 — WithArguments(Any / Eq / Is) matchers
    //
    // Restrict a shim rule to specific constructor argument values.
    // =========================================================================

    [TestMethod]
    public void Sample_WithArguments_AnyMatcher()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var fakeRepo = harness.CreateFake<UserRepository>("any-fake");

        using (ShimContext.Create())
        {
            // Any<string>() accepts any non-null string argument.
            harness.RegisterShimWithMatchers<UserRepository>(fakeRepo, Any<string>());

            var service = harness.Create<UserService>();
            var result = harness.Invoke<string>(
                service, nameof(UserService.GetDisplayNameWithArgRepository), 7);

            Assert.AreEqual("any-fake-7", result,
                "Any<string>() must match 'prod'.");
        }
    }

    [TestMethod]
    public void Sample_WithArguments_EqMatcher_MatchesSpecificValue()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var prodFake = harness.CreateFake<UserRepository>("prod-fake");
        var catchAllFake = harness.CreateFake<UserRepository>("catch-all");

        using (ShimContext.Create())
        {
            // Catch-all registered first (lowest priority).
            harness.RegisterShimWithMatchers<UserRepository>(catchAllFake);
            // Eq("prod") registered last (highest priority).
            harness.RegisterShimWithMatchers<UserRepository>(prodFake, Eq<string>("prod"));

            var service = harness.Create<UserService>();
            // GetDisplayNameWithArgRepository calls new UserRepository("prod") → Eq matches.
            var result = harness.Invoke<string>(
                service, nameof(UserService.GetDisplayNameWithArgRepository), 3);

            Assert.AreEqual("prod-fake-3", result,
                "Eq(\"prod\") must intercept new UserRepository(\"prod\").");
        }
    }

    [TestMethod]
    public void Sample_WithArguments_IsMatcher_Predicate()
    {
        using (ShimContext.Create())
        {
            // Is<int> matches when the predicate returns true.
            Shim.New<UserRepository>()
                .WithArguments(Is<string>(s => s != null && s.StartsWith("prod", StringComparison.Ordinal)))
                .Returns(new UserRepository("is-match"));

            // Act via dispatcher — simulates what the rewritten assembly would call.
            // (Direct dispatcher call with args, for unit-level verification.)
            var result = ShimDispatcher.NewWithArgs<UserRepository>(["prod-xyz"]);

            // GetName(0) returns "{prefix}-{id}", so "is-match-0" for prefix="is-match"
            Assert.AreEqual("is-match-0", result.GetName(0));
        }
    }

    // =========================================================================
    // Pattern 4 — ShimCaptor
    //
    // Capture the actual constructor argument that was passed.
    // =========================================================================

    [TestMethod]
    public void Sample_ShimCaptor_CapturesConstructorArgument()
    {
        var captor = ShimCaptor.For<string>();

        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var fakeRepo = harness.CreateFake<UserRepository>("captor-fake");

        using (ShimContext.Create())
        {
            // Captor acts as a matcher AND captures the argument.
            harness.RegisterShimWithMatchers<UserRepository>(fakeRepo, captor);

            var service = harness.Create<UserService>();
            // GetDisplayNameWithArgRepository calls new UserRepository("prod").
            harness.Invoke<string>(service, nameof(UserService.GetDisplayNameWithArgRepository), 1);
        }

        Assert.IsTrue(captor.HasValue, "Captor must have captured the argument.");
        Assert.AreEqual("prod", captor.Value,
            "The captured value must be 'prod' — the string passed to new UserRepository(\"prod\").");
    }

    // =========================================================================
    // Pattern 5 — No match fallback
    //
    // When no shim rule matches, the real constructor is called.
    // =========================================================================

    [TestMethod]
    public void Sample_NoMatchFallback_CallsRealConstructor()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            // Register Eq("other") only — does not match "prod".
            var neverReached = harness.CreateFake<UserRepository>("never");
            harness.RegisterShimWithMatchers<UserRepository>(neverReached, Eq<string>("other"));

            var service = harness.Create<UserService>();
            // GetDisplayNameWithArgRepository calls new UserRepository("prod") → no match → real.
            var result = harness.Invoke<string>(
                service, nameof(UserService.GetDisplayNameWithArgRepository), 5);

            Assert.AreEqual("prod-5", result,
                "Real UserRepository(\"prod\") must be used when no matcher matches.");
        }
    }

    // =========================================================================
    // Pattern 6 — Last stub wins
    //
    // When multiple rules match, the most recently registered rule wins.
    // =========================================================================

    [TestMethod]
    public void Sample_LastStubWins()
    {
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>().Returns(new UserRepository("first"));
            Shim.New<UserRepository>().Returns(new UserRepository("last"));

            var result = ShimDispatcher.New<UserRepository>();

            Assert.AreEqual("last", result.GetName(0).Split('-')[0],
                "Last registered stub wins when multiple catch-all rules exist.");
        }
    }

    // =========================================================================
    // Pattern 7 — Isolated ALC harness (full integration)
    //
    // Demonstrates the complete recommended workflow:
    //   1. Create a harness and specify which types to intercept.
    //   2. Call RewriteTargetTypeAssembly() — rewrites a copy of the assembly in a temp dir.
    //   3. Load the rewritten assembly into an isolated, collectible AssemblyLoadContext.
    //   4. Inside a ShimContext, register shim rules.
    //   5. Create service/fake instances from the rewritten assembly.
    //   6. Invoke methods via reflection; the shims fire inside the isolated ALC.
    // =========================================================================

    [TestMethod]
    public void Sample_AlcHarness_ParameterlessNew_FullWorkflow()
    {
        // Step 1 — build the harness (rewrites the assembly once).
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()           // intercept `new UserRepository()`
            .RewriteTargetTypeAssembly();

        // Original assembly is never modified.
        Assert.IsFalse(
            string.Equals(
                typeof(UserRepository).Assembly.Location,
                harness.OutputAssemblyPath,
                StringComparison.OrdinalIgnoreCase),
            "Rewritten assembly path must differ from the original assembly path.");

        // Step 2 — create fake and service from the REWRITTEN assembly.
        var fakeRepo = harness.CreateFake<UserRepository>("harness-fake");

        using (ShimContext.Create())
        {
            // Step 3 — register the fake; this wires up the shim rule.
            harness.RegisterShim<UserRepository>(fakeRepo);

            var service = harness.Create<UserService>();

            // Step 4 — invoke; internally `new UserRepository()` is replaced by the shim.
            var result = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 42);

            Assert.AreEqual("harness-fake-42", result,
                "The shim must intercept `new UserRepository()` inside UserService.");
        }
    }

    // =========================================================================
    // Pattern 8 — User-defined static method shim (non-void)
    //
    // Shim a static method so that it returns a controlled value.
    // Requires the assembly to be rewritten via NewInterceptionHarness.WithStaticTarget().
    // =========================================================================

    [TestMethod]
    public void Sample_StaticShim_NonVoid_ParameterlessMethod()
    {
        var fixedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using var harness = NewInterceptionHarness.Create()
            .WithStaticTarget(typeof(StaticClock))   // intercept static calls on StaticClock
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            // Register: StaticClock.Now() always returns fixedTime.
            Shim.Static<DateTime>(typeof(StaticClock).FullName!, nameof(StaticClock.Now))
                .Returns(fixedTime);

            var service = harness.Create<TimedService>();
            // GetTimedName calls StaticClock.Now() internally.
            var result = harness.Invoke<string>(service, nameof(TimedService.GetTimedName), 1);

            Assert.AreEqual($"1-{fixedTime:yyyyMMdd}", result,
                "StaticClock.Now() must return the shimmed DateTime.");
        }
    }

    [TestMethod]
    public void Sample_StaticShim_WithArgumentMatcher_IntParam()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithStaticTarget(typeof(StaticClock))
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            // Register with Eq(10) — only matches when id == 10.
            Shim.Static<string>(
                    typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .WithArguments(Eq(10))
                .Returns("shimmed-name-10");

            var service = harness.Create<TimedService>();

            var match = harness.Invoke<string>(service, nameof(TimedService.GetDisplayName), 10);
            Assert.AreEqual("shimmed-name-10", match, "Eq(10) must match id=10.");

            var noMatch = harness.Invoke<string>(service, nameof(TimedService.GetDisplayName), 99);
            Assert.AreEqual("real-name-99", noMatch, "No match → fallback to real method.");
        }
    }

    [TestMethod]
    public void Sample_StaticShim_TypeBasedApi()
    {
        // The Type-based overload is equivalent to the string-based overload but
        // derives the full name automatically.
        using (ShimContext.Create())
        {
            Shim.Static<string>(typeof(StaticClock), nameof(StaticClock.GetName), typeof(int))
                .Returns("type-api-result");

            StaticShimDispatcher.TryInvoke<string>(
                typeof(StaticClock).FullName!, "GetName",
                [typeof(int)], [(object)1],
                out var result);

            Assert.AreEqual("type-api-result", result);
        }
    }

    // =========================================================================
    // Pattern 9 — Void static method shim
    //
    // Intercept a void static method to suppress side-effects or record calls.
    // =========================================================================

    [TestMethod]
    public void Sample_StaticShim_VoidMethod_Callback()
    {
        var recorded = new List<string>();

        using (ShimContext.Create())
        {
            // Register: intercept LogCall and capture the message.
            Shim.Static(typeof(StaticClock).FullName!, nameof(StaticClock.LogCall), typeof(string))
                .Callback(args => recorded.Add((string?)args[0] ?? ""));

            // Simulate the dispatcher call (the rewritten assembly would produce this).
            StaticShimDispatcher.TryInvokeVoid(
                typeof(StaticClock).FullName!, "LogCall",
                [typeof(string)], [(object)"hello-world"]);
        }

        Assert.AreEqual(1, recorded.Count, "Callback must have been invoked once.");
        Assert.AreEqual("hello-world", recorded[0]);
    }

    [TestMethod]
    public void Sample_StaticShim_VoidMethod_DoNothing()
    {
        // DoNothing() suppresses the side-effect entirely.
        using (ShimContext.Create())
        {
            Shim.Static(typeof(StaticClock).FullName!, nameof(StaticClock.LogCall), typeof(string))
                .DoNothing();

            var found = StaticShimDispatcher.TryInvokeVoid(
                typeof(StaticClock).FullName!, "LogCall",
                [typeof(string)], [(object)"suppressed"]);

            Assert.IsTrue(found, "DoNothing shim must signal that the call was handled.");
        }
    }

    // =========================================================================
    // Pattern 10 — newobj shim and static shim coexisting
    //
    // Both interception modes can be active in the same ShimContext and harness.
    // =========================================================================

    [TestMethod]
    public void Sample_NewAndStaticShim_Coexist()
    {
        var fixedTime = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()               // newobj interception
            .WithStaticTarget(typeof(StaticClock))      // static call interception
            .RewriteTargetTypeAssembly();

        var fakeRepo = harness.CreateFake<UserRepository>("coexist-fake");

        using (ShimContext.Create())
        {
            // newobj shim
            harness.RegisterShim<UserRepository>(fakeRepo);

            // static shim
            Shim.Static<DateTime>(typeof(StaticClock).FullName!, nameof(StaticClock.Now))
                .Returns(fixedTime);

            // --- verify newobj shim ---
            var userService = harness.Create<UserService>();
            var nameResult = harness.Invoke<string>(userService, nameof(UserService.GetDisplayName), 7);
            Assert.AreEqual("coexist-fake-7", nameResult,
                "newobj shim must work alongside static shim.");

            // --- verify static shim ---
            var timedService = harness.Create<TimedService>();
            var timeResult = harness.Invoke<string>(timedService, nameof(TimedService.GetTimedName), 3);
            Assert.AreEqual($"3-{fixedTime:yyyyMMdd}", timeResult,
                "static shim must work alongside newobj shim.");
        }
    }

    // =========================================================================
    // Pattern 11 — Unsupported BCL static: skipped with a diagnostic
    //
    // Putting a BCL type in StaticTargetTypes does not cause an exception;
    // the rewriter simply skips BCL call sites and records a diagnostic message.
    // =========================================================================

    [TestMethod]
    public void Sample_BclStaticTarget_IsSkipped_WithDiagnosticReason()
    {
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"Phase145_BclSkip_{Guid.NewGuid():N}.dll");

        // Pass a BCL type (DateTime) as a static target.
        // The rewriter will find DateTime.get_Now() call sites inside StaticClock.Now()
        // and flag them as BCL — no rewrites will be performed.
        var result = AssemblyRewriter.RewriteNewObj(
            typeof(StaticClock).Assembly.Location,
            outputPath,
            new RewriteOptions
            {
                StaticTargetTypes = [typeof(DateTime)],
                CopyRuntimeFiles = false,
            });

        // No BCL call sites should be rewritten.
        // (Newobj count is also 0 because TargetTypes is empty.)
        Assert.IsNotNull(result, "RewriteNewObj must not throw for BCL static targets.");

        var hasBclSkipMessage = result.Diagnostics.Any(
            d => d.Contains("Skipped BCL", StringComparison.OrdinalIgnoreCase) ||
                 d.Contains("BCL type", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(hasBclSkipMessage,
            "Diagnostics must contain at least one BCL-skip message. " +
            $"Actual diagnostics:\n{string.Join("\n", result.Diagnostics)}");
    }

    // =========================================================================
    // Pattern 12 — Original assembly is never modified
    //
    // The rewritten output is written to a separate temp directory.
    // =========================================================================

    [TestMethod]
    public void Sample_OriginalAssembly_IsNeverModified()
    {
        var originalPath = typeof(UserRepository).Assembly.Location;
        var beforeWrite = File.GetLastWriteTimeUtc(originalPath);

        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var afterWrite = File.GetLastWriteTimeUtc(originalPath);

        Assert.AreEqual(beforeWrite, afterWrite,
            "The original assembly must not be touched by the rewriter.");
        Assert.IsFalse(
            string.Equals(originalPath, harness.OutputAssemblyPath, StringComparison.OrdinalIgnoreCase),
            "The rewritten output path must differ from the original assembly path.");
    }

    // =========================================================================
    // Pattern 13 — Throws shim
    // =========================================================================

    [TestMethod]
    public void Sample_StaticShim_Throws()
    {
        using (ShimContext.Create())
        {
            Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .Throws(new InvalidOperationException("static-throws-sample"));

            var ex = Assert.ThrowsException<InvalidOperationException>(() =>
                StaticShimDispatcher.TryInvoke<string>(
                    typeof(StaticClock).FullName!, "GetName",
                    [typeof(int)], [(object)1],
                    out _));

            StringAssert.Contains(ex.Message, "static-throws-sample");
        }
    }

    // =========================================================================
    // Pattern 14 — Diagnostics
    //
    // ShimContext.LastStaticDispatchDiagnostics exposes per-call matching details.
    // =========================================================================

    [TestMethod]
    public void Sample_StaticShim_Diagnostics_AfterMismatch()
    {
        using var ctx = ShimContext.Create();

        Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
            .WithArguments(Eq(99))
            .Returns("only-99");

        // Call with id=1 — no match, falls back to real method.
        StaticShimDispatcher.TryInvoke<string>(
            typeof(StaticClock).FullName!, "GetName",
            [typeof(int)], [(object)1],
            out _);

        var diag = ctx.LastStaticDispatchDiagnostics;
        Assert.IsNotNull(diag, "Diagnostics must be recorded even on no-match.");
        Assert.IsFalse(diag.MatchFound, "MatchFound must be false when Eq(99) does not match 1.");
        Assert.IsTrue(diag.FalledBack, "FalledBack must be true.");
        Assert.IsTrue(diag.TriedRules.Count > 0, "TriedRules must list the evaluated rule.");

        var formatted = diag.Format();
        StringAssert.Contains(formatted, "Target:");
        StringAssert.Contains(formatted, "Tried rules:");
        StringAssert.Contains(formatted, "Fallback:");
    }

    // =========================================================================
    // Regression — existing tests must not be affected by Phase 14.5 changes
    // =========================================================================

    [TestMethod]
    public void Regression_NewShim_UnaffectedByPhase145()
    {
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>().Returns(new UserRepository("regression"));
            var result = ShimDispatcher.New<UserRepository>();
            Assert.AreEqual("regression", result.GetName(0).Split('-')[0]);
        }
    }

    [TestMethod]
    public void Regression_ShimContext_DisposeClearsStaticRulesAndNewRules()
    {
        ShimContext ctx;
        using (ctx = ShimContext.Create())
        {
            Shim.New<UserRepository>().Returns(new UserRepository("r"));
            Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .Returns("s");
            Assert.IsTrue(ctx.Registry.Count > 0, "New-shim registry must have rules.");
            Assert.IsTrue(ctx.StaticRegistry.Count > 0, "Static registry must have rules.");
        }

        Assert.AreEqual(0, ctx.Registry.Count, "New-shim registry cleared on dispose.");
        Assert.AreEqual(0, ctx.StaticRegistry.Count, "Static registry cleared on dispose.");
    }
}
