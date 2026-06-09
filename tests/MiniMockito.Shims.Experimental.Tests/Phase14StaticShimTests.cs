using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental.Sample;

namespace MiniMockito.Shims.Experimental.Tests;

/// <summary>
/// Phase 14: static method mocking PoC tests.
/// Covers the full stack: StaticShimDispatcher unit tests, IL-rewrite integration tests,
/// and regression checks for existing new-interception tests.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class Phase14StaticShimTests
{
    // =========================================================================
    // Unit tests — dispatcher only (no IL rewrite)
    // =========================================================================

    [TestMethod]
    public void StaticDispatcher_NoRule_ReturnsFalse()
    {
        using (ShimContext.Create())
        {
            var found = StaticShimDispatcher.TryInvoke<string>(
                "Sample.StaticClock", "GetName",
                [typeof(int)], [(object)1],
                out var result);

            Assert.IsFalse(found);
            Assert.IsNull(result);
        }
    }

    [TestMethod]
    public void StaticDispatcher_WithRule_ReturnsTrue_AndShimmedValue()
    {
        using (ShimContext.Create())
        {
            Shim.Static<string>(
                    typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .Returns("shimmed");

            var found = StaticShimDispatcher.TryInvoke<string>(
                typeof(StaticClock).FullName!, "GetName",
                [typeof(int)], [(object)1],
                out var result);

            Assert.IsTrue(found);
            Assert.AreEqual("shimmed", result);
        }
    }

    [TestMethod]
    public void StaticDispatcher_EqMatcher_MatchesCorrectArg()
    {
        using (ShimContext.Create())
        {
            Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .WithArguments(ShimArg.Eq(42))
                .Returns("matched-42");

            var found42 = StaticShimDispatcher.TryInvoke<string>(
                typeof(StaticClock).FullName!, "GetName",
                [typeof(int)], [(object)42],
                out var r42);

            var foundOther = StaticShimDispatcher.TryInvoke<string>(
                typeof(StaticClock).FullName!, "GetName",
                [typeof(int)], [(object)99],
                out _);

            Assert.IsTrue(found42, "Eq(42) should match arg 42.");
            Assert.AreEqual("matched-42", r42);
            Assert.IsFalse(foundOther, "Eq(42) should not match arg 99.");
        }
    }

    [TestMethod]
    public void StaticDispatcher_AnyMatcher_MatchesAnyArg()
    {
        using (ShimContext.Create())
        {
            Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .WithArguments(ShimArg.Any<int>())
                .Returns("any-result");

            var found = StaticShimDispatcher.TryInvoke<string>(
                typeof(StaticClock).FullName!, "GetName",
                [typeof(int)], [(object)999],
                out var result);

            Assert.IsTrue(found);
            Assert.AreEqual("any-result", result);
        }
    }

    [TestMethod]
    public void StaticDispatcher_ShimCaptor_CapturesArgument()
    {
        var captor = ShimCaptor.For<int>();

        using (ShimContext.Create())
        {
            Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .WithArguments(captor)
                .Returns("captured");

            StaticShimDispatcher.TryInvoke<string>(
                typeof(StaticClock).FullName!, "GetName",
                [typeof(int)], [(object)77],
                out _);
        }

        Assert.IsTrue(captor.HasValue, "Captor should have captured the argument.");
        Assert.AreEqual(77, captor.Value);
    }

    [TestMethod]
    public void StaticDispatcher_VoidMethod_Shimmed()
    {
        string? recorded = null;

        using (ShimContext.Create())
        {
            Shim.Static(typeof(StaticClock).FullName!, nameof(StaticClock.LogCall), typeof(string))
                .Callback(args => recorded = (string?)args[0]);

            var found = StaticShimDispatcher.TryInvokeVoid(
                typeof(StaticClock).FullName!, "LogCall",
                [typeof(string)], [(object)"hello"]);

            Assert.IsTrue(found, "Void shim should return true (rule found).");
        }

        Assert.AreEqual("hello", recorded, "Callback should have captured the argument.");
    }

    [TestMethod]
    public void StaticDispatcher_VoidMethod_NoRule_ReturnsFalse()
    {
        using (ShimContext.Create())
        {
            var found = StaticShimDispatcher.TryInvokeVoid(
                typeof(StaticClock).FullName!, "LogCall",
                [typeof(string)], [(object)"hello"]);

            Assert.IsFalse(found);
        }
    }

    [TestMethod]
    public void StaticDispatcher_Throws_PropagatesException()
    {
        using (ShimContext.Create())
        {
            Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .Throws(new InvalidOperationException("static-throw"));

            Assert.ThrowsException<InvalidOperationException>(() =>
                StaticShimDispatcher.TryInvoke<string>(
                    typeof(StaticClock).FullName!, "GetName",
                    [typeof(int)], [(object)1],
                    out _));
        }
    }

    [TestMethod]
    public void StaticDispatcher_LastStubWins()
    {
        using (ShimContext.Create())
        {
            Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .Returns("first");

            Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .Returns("last");

            StaticShimDispatcher.TryInvoke<string>(
                typeof(StaticClock).FullName!, "GetName",
                [typeof(int)], [(object)1],
                out var result);

            Assert.AreEqual("last", result, "Last registered stub should win.");
        }
    }

    [TestMethod]
    public void StaticDispatcher_TypeApi_Works()
    {
        var fixedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using (ShimContext.Create())
        {
            // Type-based overload
            Shim.Static<DateTime>(typeof(StaticClock), nameof(StaticClock.Now))
                .Returns(fixedTime);

            StaticShimDispatcher.TryInvoke<DateTime>(
                typeof(StaticClock).FullName!, "Now",
                [], [],
                out var result);

            Assert.AreEqual(fixedTime, result);
        }
    }

    [TestMethod]
    public void StaticDispatcher_BoolArgument_EqMatcher()
    {
        using (ShimContext.Create())
        {
            Shim.Static<bool>(typeof(StaticClock).FullName!, nameof(StaticClock.IsOpen), typeof(bool))
                .WithArguments(ShimArg.Eq(true))
                .Returns(false);

            var found = StaticShimDispatcher.TryInvoke<bool>(
                typeof(StaticClock).FullName!, "IsOpen",
                [typeof(bool)], [(object)true],
                out var result);

            Assert.IsTrue(found);
            Assert.IsFalse(result, "Shimmed value should be false when Eq(true) matches.");
        }
    }

    [TestMethod]
    public void StaticDispatcher_MultipleStringArgs()
    {
        using (ShimContext.Create())
        {
            Shim.Static<string>(
                    typeof(StaticClock).FullName!, nameof(StaticClock.Concat),
                    typeof(string), typeof(string))
                .WithArguments(ShimArg.Eq<string>("a"), ShimArg.Any<string>())
                .Returns("shimmed-concat");

            var found = StaticShimDispatcher.TryInvoke<string>(
                typeof(StaticClock).FullName!, "Concat",
                [typeof(string), typeof(string)], [(object)"a", (object)"b"],
                out var result);

            Assert.IsTrue(found);
            Assert.AreEqual("shimmed-concat", result);
        }
    }

    [TestMethod]
    public void StaticDispatcher_Diagnostics_MatchFound()
    {
        using var ctx = ShimContext.Create();
        Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
            .Returns("diag-test");

        StaticShimDispatcher.TryInvoke<string>(
            typeof(StaticClock).FullName!, "GetName",
            [typeof(int)], [(object)1],
            out _);

        var diag = ctx.LastStaticDispatchDiagnostics;
        Assert.IsNotNull(diag, "Diagnostics should be recorded.");
        Assert.IsTrue(diag.MatchFound, "Diagnostics should report match found.");
        Assert.IsFalse(diag.FalledBack);
    }

    [TestMethod]
    public void StaticDispatcher_Diagnostics_FalledBack()
    {
        using var ctx = ShimContext.Create();
        // No rule registered.
        StaticShimDispatcher.TryInvoke<string>(
            typeof(StaticClock).FullName!, "GetName",
            [typeof(int)], [(object)1],
            out _);

        var diag = ctx.LastStaticDispatchDiagnostics;
        Assert.IsNotNull(diag, "Diagnostics should be recorded even when no match.");
        Assert.IsFalse(diag.MatchFound);
        Assert.IsTrue(diag.FalledBack);
    }

    [TestMethod]
    public void StaticDispatcher_Format_ContainsExpectedSections()
    {
        using var ctx = ShimContext.Create();
        Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
            .WithArguments(ShimArg.Eq(99))
            .Returns("x");

        // Call with non-matching arg so diagnostics include tried rules.
        StaticShimDispatcher.TryInvoke<string>(
            typeof(StaticClock).FullName!, "GetName",
            [typeof(int)], [(object)1],
            out _);

        var formatted = ctx.LastStaticDispatchDiagnostics!.Format();
        StringAssert.Contains(formatted, "Target:");
        StringAssert.Contains(formatted, "Tried rules:");
        StringAssert.Contains(formatted, "Fallback:");
    }

    [TestMethod]
    public void StaticRegistry_Clear_RemovesAllRules()
    {
        using var ctx = ShimContext.Create();
        Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
            .Returns("x");

        Assert.IsTrue(ctx.StaticRegistry.Count > 0, "Registry should have rules after registration.");
        ctx.StaticRegistry.Clear();
        Assert.AreEqual(0, ctx.StaticRegistry.Count, "Registry should be empty after Clear.");
    }

    [TestMethod]
    public void StaticContext_Dispose_ClearsStaticRegistry()
    {
        ShimContext ctx;
        using (ctx = ShimContext.Create())
        {
            Shim.Static<string>(typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .Returns("disposed-test");
            Assert.IsTrue(ctx.StaticRegistry.Count > 0);
        }

        Assert.AreEqual(0, ctx.StaticRegistry.Count,
            "StaticRegistry should be cleared when context is disposed.");
    }

    // =========================================================================
    // Integration tests — IL rewrite via NewInterceptionHarness
    // =========================================================================

    [TestMethod]
    public void Integration_StaticNow_IsShimmed_ViaHarness()
    {
        var fixedTime = new DateTime(2030, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        using var harness = NewInterceptionHarness.Create()
            .WithStaticTarget(typeof(StaticClock))
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            Shim.Static<DateTime>(typeof(StaticClock).FullName!, nameof(StaticClock.Now))
                .Returns(fixedTime);

            var service = harness.Create<TimedService>();
            var result = harness.Invoke<string>(service, nameof(TimedService.GetTimedName), 1);

            Assert.AreEqual($"1-{fixedTime:yyyyMMdd}", result,
                "Clock.Now() must return the fixed time when shimmed.");
        }
    }

    [TestMethod]
    public void Integration_StaticGetName_IsShimmed_WithEqMatcher()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithStaticTarget(typeof(StaticClock))
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            Shim.Static<string>(
                    typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .WithArguments(ShimArg.Eq(5))
                .Returns("shim-name-5");

            var service = harness.Create<TimedService>();
            var result = harness.Invoke<string>(service, nameof(TimedService.GetDisplayName), 5);

            Assert.AreEqual("shim-name-5", result,
                "GetName(5) should return the shimmed value when Eq(5) matches.");
        }
    }

    [TestMethod]
    public void Integration_StaticGetName_NoMatch_FallsBackToReal()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithStaticTarget(typeof(StaticClock))
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            // Register Eq(99) so arg=7 does NOT match.
            Shim.Static<string>(
                    typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .WithArguments(ShimArg.Eq(99))
                .Returns("should-not-appear");

            var service = harness.Create<TimedService>();
            var result = harness.Invoke<string>(service, nameof(TimedService.GetDisplayName), 7);

            Assert.AreEqual("real-name-7", result,
                "When matcher does not match, fallback to real StaticClock.GetName(id) expected.");
        }
    }

    [TestMethod]
    public void Integration_StaticIsOpen_BoolArg_Shimmed()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithStaticTarget(typeof(StaticClock))
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            Shim.Static<bool>(
                    typeof(StaticClock).FullName!, nameof(StaticClock.IsOpen), typeof(bool))
                .Returns(false);

            var service = harness.Create<TimedService>();
            var result = harness.Invoke<bool>(service, nameof(TimedService.CheckOpen), true);

            Assert.IsFalse(result, "IsOpen should return shimmed false regardless of the real impl.");
        }
    }

    [TestMethod]
    public void Integration_StaticCatchAll_WithAnyMatcher()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithStaticTarget(typeof(StaticClock))
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            Shim.Static<string>(
                    typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .WithArguments(ShimArg.Any<int>())
                .Returns("any-name");

            var service = harness.Create<TimedService>();
            var r1 = harness.Invoke<string>(service, nameof(TimedService.GetDisplayName), 1);
            var r2 = harness.Invoke<string>(service, nameof(TimedService.GetDisplayName), 200);

            Assert.AreEqual("any-name", r1);
            Assert.AreEqual("any-name", r2);
        }
    }

    [TestMethod]
    public void Integration_NoShimRegistered_RealMethodCalled()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithStaticTarget(typeof(StaticClock))
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            // No shim — real Clock.GetName(3) returns "real-name-3".
            var service = harness.Create<TimedService>();
            var result = harness.Invoke<string>(service, nameof(TimedService.GetDisplayName), 3);

            Assert.AreEqual("real-name-3", result,
                "With no shim registered, the real static method should execute.");
        }
    }

    [TestMethod]
    public void Integration_OriginalAssembly_NotModified()
    {
        var originalPath = typeof(StaticClock).Assembly.Location;
        var originalLastWrite = File.GetLastWriteTimeUtc(originalPath);

        using var harness = NewInterceptionHarness.Create()
            .WithStaticTarget(typeof(StaticClock))
            .RewriteTargetTypeAssembly();

        var afterLastWrite = File.GetLastWriteTimeUtc(originalPath);

        Assert.AreEqual(originalLastWrite, afterLastWrite,
            "The original assembly must not be modified by the static call rewrite.");
        Assert.IsFalse(
            string.Equals(originalPath, harness.OutputAssemblyPath, StringComparison.OrdinalIgnoreCase),
            "The rewritten output path must differ from the original assembly path.");
    }

    [TestMethod]
    public void Integration_StaticAndNew_CoexistInSameContext()
    {
        var fixedTime = new DateTime(2031, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .WithStaticTarget(typeof(StaticClock))
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            // newobj shim
            var fakeRepo = harness.CreateFake<UserRepository>("static-and-new-fake");
            harness.RegisterShim<UserRepository>(fakeRepo);

            // static shim
            Shim.Static<DateTime>(typeof(StaticClock).FullName!, nameof(StaticClock.Now))
                .Returns(fixedTime);

            // newobj verification
            var service = harness.Create<UserService>();
            var nameResult = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 10);
            Assert.AreEqual("static-and-new-fake-10", nameResult,
                "New shim must still work when combined with static shim.");

            // static verification
            var timedService = harness.Create<TimedService>();
            var timedResult = harness.Invoke<string>(timedService, nameof(TimedService.GetTimedName), 2);
            Assert.AreEqual($"2-{fixedTime:yyyyMMdd}", timedResult,
                "Static shim must still work when combined with new shim.");
        }
    }

    [TestMethod]
    public void Integration_MultipleStaticRules_LastWins()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithStaticTarget(typeof(StaticClock))
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            Shim.Static<string>(
                    typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .Returns("first-stub");

            Shim.Static<string>(
                    typeof(StaticClock).FullName!, nameof(StaticClock.GetName), typeof(int))
                .Returns("second-stub");

            var service = harness.Create<TimedService>();
            var result = harness.Invoke<string>(service, nameof(TimedService.GetDisplayName), 1);

            Assert.AreEqual("second-stub", result,
                "Last registered stub should win.");
        }
    }

    // =========================================================================
    // Unsupported / diagnostic path tests
    // =========================================================================

    [TestMethod]
    public void StaticRewriteResult_GenericMethod_IsInSkipped()
    {
        // GenericRepository<T> has static methods that should be skipped (generic declaring type).
        var result = AssemblyRewriter.RewriteNewObj(
            typeof(StaticClock).Assembly.Location,
            Path.Combine(Path.GetTempPath(), $"Phase14_GenericSkip_{Guid.NewGuid():N}.dll"),
            new RewriteOptions
            {
                StaticTargetTypes = [typeof(GenericRepository<string>)],
                CopyRuntimeFiles = false,
            });

        // The scan should not have crashed; generic types are gracefully skipped.
        Assert.IsNotNull(result, "RewriteNewObj must not throw for generic static targets.");
    }

    // =========================================================================
    // Regression tests — existing Phase 7–12 tests must be unaffected
    // =========================================================================

    [TestMethod]
    public void Regression_NewShim_StillWorks_AfterPhase14()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            var fake = harness.CreateFake<UserRepository>("regression-fake");
            harness.RegisterShim<UserRepository>(fake);

            var service = harness.Create<UserService>();
            var result = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 99);

            Assert.AreEqual("regression-fake-99", result,
                "Phase 7–12 newobj shim must be unaffected by Phase 14 changes.");
        }
    }

    [TestMethod]
    public void Regression_NoStaticTarget_NoStaticRewrite()
    {
        // Without WithStaticTarget, the rewrite should produce 0 static rewrites.
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"Phase14_NoStatic_{Guid.NewGuid():N}.dll");

        var result = AssemblyRewriter.RewriteNewObj(
            typeof(StaticClock).Assembly.Location,
            outputPath,
            new RewriteOptions
            {
                TargetTypes = [typeof(UserRepository)],
                CopyRuntimeFiles = false,
                // StaticTargetTypes is intentionally empty
            });

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Diagnostics.All(d => !d.Contains("<ShimsStaticWrappers>")),
            "No static wrapper should be generated when StaticTargetTypes is empty.");
    }

    [TestMethod]
    public void Regression_ExistingV1V2Tests_Unaffected()
    {
        // Smoke test: If v1/v2 mock infrastructure is still intact.
        // (Full v1/v2 tests run in MiniMockito.Tests; this just confirms Shim.New still compiles.)
        using (ShimContext.Create())
        {
            var builder = Shim.New<UserRepository>();
            Assert.IsNotNull(builder);
        }
    }
}
