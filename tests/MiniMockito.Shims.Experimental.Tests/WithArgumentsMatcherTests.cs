using System.Reflection;
using MiniMockito.Shims.Experimental.Sample;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WithArgumentsMatcherTests
{
    // =========================================================================
    // ShimArg.Any<T> unit tests
    // =========================================================================

    [TestMethod]
    public void AnyMatcher_MatchesStringValue()
    {
        var matcher = ShimArg.Any<string>();
        Assert.IsTrue(matcher.Matches("hello"));
    }

    [TestMethod]
    public void AnyMatcher_MatchesNullForReferenceType()
    {
        // Reference types: null is accepted by Any<T>.
        var matcher = ShimArg.Any<string>();
        Assert.IsTrue(matcher.Matches(null));
    }

    [TestMethod]
    public void AnyMatcher_MatchesBoxedInt()
    {
        var matcher = ShimArg.Any<int>();
        Assert.IsTrue(matcher.Matches((object)42));
    }

    [TestMethod]
    public void AnyMatcher_RejectsNullForNonNullableValueType()
    {
        // Non-nullable value type: null must NOT match Any<int>().
        var matcher = ShimArg.Any<int>();
        Assert.IsFalse(matcher.Matches(null));
    }

    [TestMethod]
    public void AnyMatcher_MatchesNullForNullableValueType()
    {
        var matcher = ShimArg.Any<int?>();
        Assert.IsTrue(matcher.Matches(null));
    }

    [TestMethod]
    public void AnyMatcher_Describe_ContainsTypeName()
    {
        var matcher = ShimArg.Any<string>();
        StringAssert.Contains(matcher.Describe(), "Any<String>()");
    }

    // =========================================================================
    // ShimArg.Eq<T> unit tests
    // =========================================================================

    [TestMethod]
    public void EqMatcher_MatchesExpectedString()
    {
        var matcher = ShimArg.Eq("prod");
        Assert.IsTrue(matcher.Matches("prod"));
    }

    [TestMethod]
    public void EqMatcher_RejectsDifferentString()
    {
        var matcher = ShimArg.Eq("prod");
        Assert.IsFalse(matcher.Matches("dev"));
    }

    [TestMethod]
    public void EqMatcher_MatchesBoxedInt()
    {
        var matcher = ShimArg.Eq<int>(1);
        Assert.IsTrue(matcher.Matches((object)1));
    }

    [TestMethod]
    public void EqMatcher_RejectsDifferentBoxedInt()
    {
        var matcher = ShimArg.Eq<int>(1);
        Assert.IsFalse(matcher.Matches((object)2));
    }

    [TestMethod]
    public void EqMatcher_MatchesBoxedBoolTrue()
    {
        var matcher = ShimArg.Eq<bool>(true);
        Assert.IsTrue(matcher.Matches((object)true));
    }

    [TestMethod]
    public void EqMatcher_RejectsDifferentBool()
    {
        var matcher = ShimArg.Eq<bool>(true);
        Assert.IsFalse(matcher.Matches((object)false));
    }

    [TestMethod]
    public void EqMatcher_MatchesNullWhenExpectedIsNull()
    {
        var matcher = ShimArg.Eq<string?>(null);
        Assert.IsTrue(matcher.Matches(null));
    }

    [TestMethod]
    public void EqMatcher_RejectsNonNullWhenExpectedIsNull()
    {
        var matcher = ShimArg.Eq<string?>(null);
        Assert.IsFalse(matcher.Matches("not-null"));
    }

    [TestMethod]
    public void EqMatcher_Describe_ContainsExpectedValue()
    {
        var matcher = ShimArg.Eq("prod");
        StringAssert.Contains(matcher.Describe(), "prod");
    }

    // =========================================================================
    // ShimArg.Is<T> unit tests
    // =========================================================================

    [TestMethod]
    public void IsMatcher_MatchesWhenPredicateReturnsTrue()
    {
        var matcher = ShimArg.Is<string>(s => s?.StartsWith("prod") == true);
        Assert.IsTrue(matcher.Matches("prod-server"));
    }

    [TestMethod]
    public void IsMatcher_RejectsWhenPredicateReturnsFalse()
    {
        var matcher = ShimArg.Is<string>(s => s?.StartsWith("prod") == true);
        Assert.IsFalse(matcher.Matches("dev-server"));
    }

    [TestMethod]
    public void IsMatcher_PredicateThrows_WrapsInShimException()
    {
        var matcher = ShimArg.Is<string>(_ => throw new InvalidOperationException("boom"));
        var ex = Assert.ThrowsException<ShimException>(() => matcher.Matches("any"));
        StringAssert.Contains(ex.Message, "Is<String>()");
        StringAssert.Contains(ex.Message, "boom");
        Assert.IsInstanceOfType<InvalidOperationException>(ex.InnerException);
    }

    [TestMethod]
    public void IsMatcher_Describe_ContainsIsMatcher()
    {
        var matcher = ShimArg.Is<string>(_ => true);
        StringAssert.Contains(matcher.Describe(), "Is<String>(predicate)");
    }

    // =========================================================================
    // Registry / ShimDispatcher dispatcher tests
    // =========================================================================

    [TestMethod]
    public void WithArguments_AnyString_MatchesStringArg()
    {
        var fake = new UserRepository("matched");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Any<string>())
                .Returns(fake);

            var result = ShimDispatcher.NewWithArgs<UserRepository>(["anything"]);
            Assert.AreSame(fake, result);
        }
    }

    [TestMethod]
    public void WithArguments_EqProd_MatchesProd()
    {
        var fake = new UserRepository("eq-matched");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Eq("prod"))
                .Returns(fake);

            var result = ShimDispatcher.NewWithArgs<UserRepository>(["prod"]);
            Assert.AreSame(fake, result);
        }
    }

    [TestMethod]
    public void WithArguments_EqProd_DoesNotMatchDev()
    {
        var fake = new UserRepository("eq-matched");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Eq("prod"))
                .Returns(fake);

            // "dev" does not match Eq("prod") — falls back to real constructor
            var result = ShimDispatcher.NewWithArgs<UserRepository>(["dev"]);
            Assert.AreNotSame(fake, result);
        }
    }

    [TestMethod]
    public void WithArguments_IsMatcher_MatchesWhenPredicateTrue()
    {
        var fake = new UserRepository("is-matched");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Is<string>(s => s?.Length > 3))
                .Returns(fake);

            var result = ShimDispatcher.NewWithArgs<UserRepository>(["long-name"]);
            Assert.AreSame(fake, result);
        }
    }

    [TestMethod]
    public void WithArguments_IntArg_EqInt_Matches()
    {
        var fake = new ArgsTestTarget(0);
        using (ShimContext.Create())
        {
            Shim.New<ArgsTestTarget>()
                .WithArguments(ShimArg.Eq<int>(99))
                .Returns(fake);

            var result = ShimDispatcher.NewWithArgs<ArgsTestTarget>([(object)99]);
            Assert.AreSame(fake, result);
        }
    }

    [TestMethod]
    public void WithArguments_BoolArg_EqBool_Matches()
    {
        var fake = new ArgsTestTarget(false);
        using (ShimContext.Create())
        {
            Shim.New<ArgsTestTarget>()
                .WithArguments(ShimArg.Eq<bool>(true))
                .Returns(fake);

            var result = ShimDispatcher.NewWithArgs<ArgsTestTarget>([(object)true]);
            Assert.AreSame(fake, result);
        }
    }

    [TestMethod]
    public void WithArguments_NullArg_EqNull_Matches()
    {
        var fake = new UserRepository("null-match");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Eq<string?>(null))
                .Returns(fake);

            var result = ShimDispatcher.NewWithArgs<UserRepository>([null]);
            Assert.AreSame(fake, result);
        }
    }

    [TestMethod]
    public void WithArguments_MultiArg_OrderMatters()
    {
        string? capturedFirst = null;
        int capturedSecond = -1;

        using (ShimContext.Create())
        {
            var fake = new ArgsTestTarget("fake");
            Shim.New<ArgsTestTarget>()
                .WithArguments(ShimArg.Any<string>(), ShimArg.Eq<int>(5))
                .Returns((object?[] args) =>
                {
                    capturedFirst = (string?)args[0];
                    capturedSecond = (int)args[1]!;
                    return fake;
                });

            var result = ShimDispatcher.NewWithArgs<ArgsTestTarget>(["hello", (object)5]);

            Assert.AreSame(fake, result);
            Assert.AreEqual("hello", capturedFirst);
            Assert.AreEqual(5, capturedSecond);
        }
    }

    [TestMethod]
    public void WithArguments_MatcherCountMismatch_NoMatch()
    {
        var fake = new ArgsTestTarget(0);
        using (ShimContext.Create())
        {
            // Register a rule requiring 1 int arg
            Shim.New<ArgsTestTarget>()
                .WithArguments(ShimArg.Any<int>())
                .Returns(fake);

            // Call with 2 args (string, int) — matcher count (1) != actual count (2) → no match → real ctor
            var result = ShimDispatcher.NewWithArgs<ArgsTestTarget>(["hello", (object)42]);
            Assert.AreNotSame(fake, result,
                "Matcher count mismatch should not match the rule.");
        }
    }

    [TestMethod]
    public void MultipleRules_LastRegisteredWins_WhenBothMatch()
    {
        var fake1 = new UserRepository("first");
        var fake2 = new UserRepository("second");

        using (ShimContext.Create())
        {
            // Register catch-all first (order 1)
            Shim.New<UserRepository>().Returns(fake1);
            // Register specific matcher second (order 2)
            Shim.New<UserRepository>().WithArguments(ShimArg.Eq("prod")).Returns(fake2);

            // "prod" matches both rules; last registered (fake2) wins
            var result = ShimDispatcher.NewWithArgs<UserRepository>(["prod"]);
            Assert.AreSame(fake2, result, "Last registered rule should win when both match.");
        }
    }

    [TestMethod]
    public void CatchAllRule_MatchesWhenSpecificRuleDoesNot()
    {
        var catchAll = new UserRepository("catch-all");
        var specific = new UserRepository("specific");

        using (ShimContext.Create())
        {
            // Register catch-all first (order 1)
            Shim.New<UserRepository>().Returns(catchAll);
            // Register specific matcher second (order 2) — only matches "prod"
            Shim.New<UserRepository>().WithArguments(ShimArg.Eq("prod")).Returns(specific);

            // "dev" does not match Eq("prod"), falls through to catch-all
            var result = ShimDispatcher.NewWithArgs<UserRepository>(["dev"]);
            Assert.AreSame(catchAll, result, "Catch-all should be used when specific rule doesn't match.");
        }
    }

    [TestMethod]
    public void WithArguments_NoMatchAtAll_FallsBackToRealConstructor()
    {
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Eq("prod"))
                .Returns(new UserRepository("fake"));

            // "other" doesn't match Eq("prod") — fall back to real
            var result = ShimDispatcher.NewWithArgs<UserRepository>(["real-prefix"]);
            Assert.AreEqual("real-prefix-42", result.GetName(42),
                "No matching rule should fall back to real constructor.");
        }
    }

    [TestMethod]
    public void NoWithArguments_IsCatchAll_MatchesAnyArgs()
    {
        var fake = new UserRepository("catch-all");
        using (ShimContext.Create())
        {
            // No WithArguments → catch-all → matches any args
            Shim.New<UserRepository>().Returns(fake);

            var result1 = ShimDispatcher.NewWithArgs<UserRepository>(["prod"]);
            var result2 = ShimDispatcher.NewWithArgs<UserRepository>(["dev"]);
            var result3 = ShimDispatcher.NewWithArgs<UserRepository>([null]);

            Assert.AreSame(fake, result1);
            Assert.AreSame(fake, result2);
            Assert.AreSame(fake, result3);
        }
    }

    [TestMethod]
    public void Regression_ArgsFactory_StillWorks()
    {
        string? captured = null;
        using (ShimContext.Create())
        {
            var fake = new UserRepository("fake");
            Shim.New<UserRepository>().Returns((object?[] args) =>
            {
                captured = (string?)args[0];
                return fake;
            });

            ShimDispatcher.NewWithArgs<UserRepository>(["my-arg"]);

            Assert.AreEqual("my-arg", captured, "Returns(args => ...) should still work after Phase 8.");
        }
    }

    [TestMethod]
    public void Regression_ContextFactory_StillWorks()
    {
        Type? capturedType = null;
        using (ShimContext.Create())
        {
            var fake = new UserRepository("fake");
            Shim.New<UserRepository>().Returns((ShimConstructorContext ctx) =>
            {
                capturedType = ctx.TargetType;
                return fake;
            });

            ShimDispatcher.NewWithArgs<UserRepository>(["ctx-arg"]);

            Assert.AreEqual(typeof(UserRepository), capturedType, "Returns(ctx => ...) should still work after Phase 8.");
        }
    }

    [TestMethod]
    public void Regression_ParameterlessShim_StillWorks()
    {
        var fake = new UserRepository("parameterless");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>().Returns(fake);
            var result = ShimDispatcher.New<UserRepository>();
            Assert.AreSame(fake, result, "Parameterless constructor shim should still work after Phase 8.");
        }
    }

    // =========================================================================
    // Rewriter integration tests
    // =========================================================================

    [TestMethod]
    public void RewrittenAssembly_EqProd_MatchesAndReturnsShimmed()
    {
        var outputPath = CreateOutputPath();
        AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputPath,
            new RewriteOptions { TargetTypes = [typeof(UserRepository)] });

        using var loader = new RewrittenAssemblyLoader(outputPath);
        var assembly = loader.Load();
        var serviceType = RequireType(assembly, typeof(UserService).FullName!);
        var repoType = RequireType(assembly, typeof(UserRepository).FullName!);
        var service = Activator.CreateInstance(serviceType)!;

        using (ShimContext.Create())
        {
            var fakeRepo = Activator.CreateInstance(repoType, "shimmed-eq")!;
            RegisterWithArgsMatcher(repoType, fakeRepo, [ShimArg.Eq("prod")]);

            var method = serviceType.GetMethod(
                nameof(UserService.GetDisplayNameWithArgRepository),
                BindingFlags.Instance | BindingFlags.Public)!;
            var result = (string)method.Invoke(service, [7])!;

            // UserService.GetDisplayNameWithArgRepository passes "prod" to new UserRepository("prod")
            Assert.AreEqual("shimmed-eq-7", result, "Eq(\"prod\") should match the \"prod\" constructor arg.");
        }
    }

    [TestMethod]
    public void RewrittenAssembly_AnyString_MatchesAndReturnsShimmed()
    {
        var outputPath = CreateOutputPath();
        AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputPath,
            new RewriteOptions { TargetTypes = [typeof(UserRepository)] });

        using var loader = new RewrittenAssemblyLoader(outputPath);
        var assembly = loader.Load();
        var serviceType = RequireType(assembly, typeof(UserService).FullName!);
        var repoType = RequireType(assembly, typeof(UserRepository).FullName!);
        var service = Activator.CreateInstance(serviceType)!;

        using (ShimContext.Create())
        {
            var fakeRepo = Activator.CreateInstance(repoType, "shimmed-any")!;
            RegisterWithArgsMatcher(repoType, fakeRepo, [ShimArg.Any<string>()]);

            var method = serviceType.GetMethod(
                nameof(UserService.GetDisplayNameWithArgRepository),
                BindingFlags.Instance | BindingFlags.Public)!;
            var result = (string)method.Invoke(service, [3])!;

            Assert.AreEqual("shimmed-any-3", result, "Any<string>() should match any string constructor arg.");
        }
    }

    [TestMethod]
    public void RewrittenAssembly_Mismatch_FallsBackToRealConstructor()
    {
        var outputPath = CreateOutputPath();
        AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputPath,
            new RewriteOptions { TargetTypes = [typeof(UserRepository)] });

        using var loader = new RewrittenAssemblyLoader(outputPath);
        var assembly = loader.Load();
        var serviceType = RequireType(assembly, typeof(UserService).FullName!);
        var repoType = RequireType(assembly, typeof(UserRepository).FullName!);
        var service = Activator.CreateInstance(serviceType)!;

        using (ShimContext.Create())
        {
            var fakeRepo = Activator.CreateInstance(repoType, "shimmed")!;
            // Register matcher for "dev" — but GetDisplayNameWithArgRepository passes "prod"
            RegisterWithArgsMatcher(repoType, fakeRepo, [ShimArg.Eq("dev")]);

            var method = serviceType.GetMethod(
                nameof(UserService.GetDisplayNameWithArgRepository),
                BindingFlags.Instance | BindingFlags.Public)!;
            var result = (string)method.Invoke(service, [5])!;

            // "prod" does not match Eq("dev") → falls back to real UserRepository("prod")
            Assert.AreEqual("prod-5", result, "Mismatched matcher should fall back to real constructor.");
        }
    }

    [TestMethod]
    public void RewrittenAssembly_MultiArg_MatcherOrderCorrect()
    {
        var outputPath = CreateOutputPath();
        AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputPath,
            new RewriteOptions { TargetTypes = [typeof(ArgsTestTarget)] });

        using var loader = new RewrittenAssemblyLoader(outputPath);
        var assembly = loader.Load();
        var serviceType = RequireType(assembly, typeof(UserService).FullName!);
        var argsType = RequireType(assembly, typeof(ArgsTestTarget).FullName!);
        var service = Activator.CreateInstance(serviceType)!;

        using (ShimContext.Create())
        {
            var fakeTarget = Activator.CreateInstance(argsType, "multi-match")!;
            // Match (string, int) where string starts with "hello" and int == 42
            RegisterWithArgsMatcher(argsType, fakeTarget, [
                ShimArg.Is<string>(s => s == "hello"),
                ShimArg.Eq<int>(42)
            ]);

            var method = serviceType.GetMethod(
                nameof(UserService.GetArgsTargetByStringAndInt),
                BindingFlags.Instance | BindingFlags.Public)!;
            var result = (string)method.Invoke(service, ["hello", 42])!;

            // ArgsTestTarget("multi-match") stores "str:multi-match" via the single-string constructor.
            Assert.AreEqual("str:multi-match", result,
                "Multi-arg matchers should match in order.");
        }
    }

    [TestMethod]
    public void RewrittenAssembly_OriginalAssemblyNotModified()
    {
        var originalPath = typeof(UserService).Assembly.Location;
        var originalHash = ComputeHash(originalPath);
        var outputPath = CreateOutputPath();

        AssemblyRewriter.RewriteNewObj(
            originalPath, outputPath,
            new RewriteOptions { TargetTypes = [typeof(UserRepository)] });

        CollectionAssert.AreEqual(originalHash, ComputeHash(originalPath),
            "Original assembly must not be modified.");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void RegisterWithArgsMatcher(Type targetType, object instance, IShimArgumentMatcher[] matchers)
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

    private static string CreateOutputPath()
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "MiniMockito.Shims.Experimental.Tests",
            "with-arguments",
            Guid.NewGuid().ToString("N"));
        return Path.Combine(dir, Path.GetFileName(typeof(UserService).Assembly.Location));
    }

    private static Type RequireType(System.Reflection.Assembly assembly, string fullName)
        => assembly.GetType(fullName, throwOnError: true)!;

    private static byte[] ComputeHash(string path)
    {
        using var fs = File.OpenRead(path);
        return System.Security.Cryptography.SHA256.HashData(fs);
    }
}
