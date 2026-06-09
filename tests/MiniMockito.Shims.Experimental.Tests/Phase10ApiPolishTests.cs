using System.Reflection;
using MiniMockito.Shims.Experimental.Sample;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MiniMockito.Shims.Experimental.ShimArg;

namespace MiniMockito.Shims.Experimental.Tests;

[TestClass]
[DoNotParallelize]
public sealed class Phase10ApiPolishTests
{
    // =========================================================================
    // static using tests
    // =========================================================================

    [TestMethod]
    public void StaticUsing_Any_CompilesAndMatchesValue()
    {
        // Any<string>() called without "ShimArg." prefix via static using
        var matcher = Any<string>();
        Assert.IsTrue(matcher.Matches("hello"), "Any<string>() via static using should match a string.");
        Assert.IsTrue(matcher.Matches(null), "Any<string>() via static using should match null.");
    }

    [TestMethod]
    public void StaticUsing_Eq_CompilesAndMatchesValue()
    {
        // Eq("prod") called without "ShimArg." prefix
        var matcher = Eq("prod");
        Assert.IsTrue(matcher.Matches("prod"), "Eq(\"prod\") via static using should match \"prod\".");
        Assert.IsFalse(matcher.Matches("dev"), "Eq(\"prod\") via static using should not match \"dev\".");
    }

    [TestMethod]
    public void StaticUsing_Is_CompilesAndMatchesValue()
    {
        // Is<string>() called without "ShimArg." prefix
        var matcher = Is<string>(s => s?.StartsWith("prod") == true);
        Assert.IsTrue(matcher.Matches("prod-server"), "Is<string>() via static using should match.");
        Assert.IsFalse(matcher.Matches("dev-server"), "Is<string>() via static using should not match.");
    }

    [TestMethod]
    public void StaticUsing_Captor_CompilesAndCapturesValue()
    {
        // Captor<string>() called without "ShimArg." prefix
        var captor = Captor<string>();
        Assert.IsNotNull(captor, "Captor<string>() via static using should return a ShimCaptor.");
        Assert.IsFalse(captor.HasValue, "Captor should have no value before Matches is called.");

        captor.Matches("captured");
        Assert.IsTrue(captor.HasValue, "Captor should have value after match.");
        Assert.AreEqual("captured", captor.Value);
    }

    [TestMethod]
    public void StaticUsing_WithDispatcher_AnyMatcherRoutesRule()
    {
        // End-to-end: use Any<string>() via static using, pass to dispatcher
        var fake = new UserRepository("static-using-fake");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(Any<string>())
                .Returns(fake);

            var result = ShimDispatcher.NewWithArgs<UserRepository>(["any-value"]);
            Assert.AreSame(fake, result, "Any<string>() via static using should route to the registered rule.");
        }
    }

    [TestMethod]
    public void StaticUsing_WithRewrittenAssembly_AnyMatcherWorks()
    {
        var outputPath = CreateOutputPath("static-using");
        AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputPath,
            new RewriteOptions { TargetTypes = [typeof(UserRepository)] });

        using var loader = new RewrittenAssemblyLoader(outputPath);
        var assembly = loader.Load();
        var serviceType = RequireType(assembly, typeof(UserService).FullName!);
        var repoType = RequireType(assembly, typeof(UserRepository).FullName!);
        var service = Activator.CreateInstance(serviceType)!;
        var fakeRepo = Activator.CreateInstance(repoType, "rewritten-fake")!;

        using (ShimContext.Create())
        {
            // Use Any<string>() via static using — no ShimArg. prefix
            RegisterShimWithMatchers(repoType, fakeRepo, [Any<string>()]);

            var method = serviceType.GetMethod(
                nameof(UserService.GetDisplayNameWithArgRepository),
                BindingFlags.Instance | BindingFlags.Public)!;
            var result = method.Invoke(service, [1]) as string;

            Assert.IsNotNull(result, "Rewritten assembly should return a non-null result.");
            StringAssert.Contains(result, "rewritten-fake",
                "Any<string>() matcher via static using should work in a rewritten assembly.");
        }
    }

    // =========================================================================
    // diagnostics tests
    // =========================================================================

    [TestMethod]
    public void Diagnostics_NoMatch_FalledBack_IsTrue()
    {
        var fake = new UserRepository("fake");
        using (var ctx = ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Eq("prod"))
                .Returns(fake);

            // "dev" does not match Eq("prod") → fallback to real constructor
            ShimDispatcher.NewWithArgs<UserRepository>(["dev"]);

            Assert.IsNotNull(ctx.LastDispatchDiagnostics, "Diagnostics should be recorded.");
            Assert.IsTrue(ctx.LastDispatchDiagnostics!.FalledBack,
                "FalledBack should be true when no rule matches.");
            Assert.IsFalse(ctx.LastDispatchDiagnostics.MatchFound,
                "MatchFound should be false when no rule matches.");
        }
    }

    [TestMethod]
    public void Diagnostics_MatchFound_IsTrue()
    {
        var fake = new UserRepository("fake");
        using (var ctx = ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Eq("prod"))
                .Returns(fake);

            ShimDispatcher.NewWithArgs<UserRepository>(["prod"]);

            Assert.IsNotNull(ctx.LastDispatchDiagnostics);
            Assert.IsTrue(ctx.LastDispatchDiagnostics!.MatchFound, "MatchFound should be true.");
            Assert.IsFalse(ctx.LastDispatchDiagnostics.FalledBack, "FalledBack should be false.");
        }
    }

    [TestMethod]
    public void Diagnostics_TriedRules_ContainsMatcherDescribe()
    {
        var fake = new UserRepository("fake");
        using (var ctx = ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Eq("prod"))
                .Returns(fake);

            // "dev" triggers mismatch; diagnostics should record tried rules
            ShimDispatcher.NewWithArgs<UserRepository>(["dev"]);

            var diag = ctx.LastDispatchDiagnostics!;
            Assert.AreEqual(1, diag.TriedRules.Count, "One rule was registered and tried.");
            Assert.AreEqual(1, diag.TriedRules[0].MatcherDescriptions.Count,
                "The rule has one matcher.");

            // Describe() output from ShimEqMatcher<T>: Eq<String>("prod")
            StringAssert.Contains(diag.TriedRules[0].MatcherDescriptions[0], "Eq<String>",
                "Matcher description should contain type name.");
            StringAssert.Contains(diag.TriedRules[0].MatcherDescriptions[0], "prod",
                "Matcher description should contain expected value.");
        }
    }

    [TestMethod]
    public void Diagnostics_Format_ContainsTargetTypeAndActualValue()
    {
        var fake = new UserRepository("fake");
        using (var ctx = ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Eq("prod"))
                .Returns(fake);

            ShimDispatcher.NewWithArgs<UserRepository>(["dev"]);

            var format = ctx.LastDispatchDiagnostics!.Format();

            StringAssert.Contains(format, "UserRepository",
                "Format should contain the target type name.");
            StringAssert.Contains(format, "\"dev\"",
                "Format should contain the actual argument value.");
            StringAssert.Contains(format, "mismatch",
                "Format should contain mismatch result for the tried rule.");
            StringAssert.Contains(format, "Fallback: real constructor",
                "Format should indicate fallback when no rule matched.");
        }
    }

    [TestMethod]
    public void Diagnostics_Format_ContainsMatcherDescribeOutput()
    {
        var fake = new UserRepository("fake");
        using (var ctx = ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Any<string>())
                .Returns(fake);

            ShimDispatcher.NewWithArgs<UserRepository>(["anything"]);

            var format = ctx.LastDispatchDiagnostics!.Format();

            // Any<string>().Describe() returns "Any<String>()"
            StringAssert.Contains(format, "Any<String>()",
                "Format should contain the matcher Describe() output.");
            StringAssert.Contains(format, "matched",
                "Format should show matched when rule was found.");
        }
    }

    [TestMethod]
    public void Diagnostics_ActualArguments_RecordedInDiagnostics()
    {
        var fake = new ArgsTestTarget(0);
        using (var ctx = ShimContext.Create())
        {
            Shim.New<ArgsTestTarget>()
                .WithArguments(ShimArg.Eq<int>(99))
                .Returns(fake);

            ShimDispatcher.NewWithArgs<ArgsTestTarget>([(object)42]);

            var diag = ctx.LastDispatchDiagnostics!;
            Assert.AreEqual(1, diag.ActualArguments.Count);
            Assert.AreEqual(42, diag.ActualArguments[0]);
        }
    }

    [TestMethod]
    public void Diagnostics_CaptorPartialCapture_BehaviorFixed()
    {
        // Partial capture: captor (at position 0) captures when its Matches() is called,
        // but the second matcher fails. The rule does not match overall.
        // A catch-all rule (registered first) then matches and is returned.
        // Captor has already captured the value — this is partial capture behavior.
        var captor = ShimCaptor.For<string>();
        var catchAllFake = new ArgsTestTarget(0);
        var specificFake = new ArgsTestTarget(1);

        using (ShimContext.Create())
        {
            // Rule 1 (registered first, evaluated last): catch-all — always matches
            Shim.New<ArgsTestTarget>().Returns(catchAllFake);

            // Rule 2 (registered second, evaluated first):
            // captor on arg[0], Eq<string>("strict") on arg[1]
            Shim.New<ArgsTestTarget>()
                .WithArguments(captor, ShimArg.Eq<string>("strict"))
                .Returns(specificFake);

            // arg[0]="first" → captor captures "first", returns true
            // arg[1]="not-strict" → Eq("strict") returns false
            // Rule 2 fails → Rule 1 (catch-all) matches → returns catchAllFake
            var result = ShimDispatcher.NewWithArgs<ArgsTestTarget>(["first", "not-strict"]);
            Assert.AreSame(catchAllFake, result, "Catch-all rule should win when specific rule fails.");
        }

        // Captor captured arg[0] even though Rule 2 did not match overall.
        // This is the documented partial capture behavior.
        Assert.IsTrue(captor.HasValue,
            "Captor should have captured arg[0] even though the rule did not match (partial capture).");
        Assert.AreEqual("first", captor.Value,
            "Captor should hold the value passed as arg[0].");
    }

    // =========================================================================
    // regression tests
    // =========================================================================

    [TestMethod]
    public void Regression_Phase7_ConstructorArgs_ArgsFactory_Unbroken()
    {
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .Returns((object?[] args) => new UserRepository("wrapped-" + (string?)args[0]));

            var result = ShimDispatcher.NewWithArgs<UserRepository>(["prod"]);
            Assert.AreEqual("wrapped-prod-1", result.GetName(1),
                "Returns(args => ...) should still work in Phase 10.");
        }
    }

    [TestMethod]
    public void Regression_Phase8_WithArguments_EqMatcher_Unbroken()
    {
        var fake = new UserRepository("phase8-regression");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Eq("prod"))
                .Returns(fake);

            var matched = ShimDispatcher.NewWithArgs<UserRepository>(["prod"]);
            Assert.AreSame(fake, matched, "Eq(\"prod\") rule should match \"prod\".");

            var fallback = ShimDispatcher.NewWithArgs<UserRepository>(["dev"]);
            Assert.AreNotSame(fake, fallback, "Eq(\"prod\") rule should not match \"dev\".");
        }
    }

    [TestMethod]
    public void Regression_Phase9_ShimCaptor_Unbroken()
    {
        var captor = ShimCaptor.For<string>();
        var fake = new UserRepository("phase9-regression");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(captor)
                .Returns(fake);

            ShimDispatcher.NewWithArgs<UserRepository>(["captured-value"]);
        }

        Assert.IsTrue(captor.HasValue, "Captor should have captured the value.");
        Assert.AreEqual("captured-value", captor.Value);
    }

    // =========================================================================
    // helpers
    // =========================================================================

    private static void RegisterShimWithMatchers(Type targetType, object instance, IShimArgumentMatcher[] matchers)
    {
        var shimNew = typeof(Shim).GetMethod(nameof(Shim.New), BindingFlags.Public | BindingFlags.Static)!;
        var builder = shimNew.MakeGenericMethod(targetType).Invoke(null, null)!;

        var withArgs = builder.GetType()
            .GetMethod(nameof(NewShimBuilder<object>.WithArguments))!;
        builder = withArgs.Invoke(builder, new object[] { matchers })!;

        var returns = builder.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(m => m.Name == "Returns" && m.GetParameters() is [var p] && p.ParameterType == targetType);
        returns.Invoke(builder, [instance]);
    }

    private static string CreateOutputPath(string tag)
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "MiniMockito.Shims.Experimental.Tests",
            "phase10",
            tag,
            Guid.NewGuid().ToString("N"));
        return Path.Combine(dir, Path.GetFileName(typeof(UserService).Assembly.Location));
    }

    private static Type RequireType(System.Reflection.Assembly assembly, string fullName)
        => assembly.GetType(fullName, throwOnError: true)!;
}
