using System.Reflection;
using MiniMockito.Shims.Experimental.Sample;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ShimCaptorTests
{
    // =========================================================================
    // ShimCaptor<T> unit tests
    // =========================================================================

    [TestMethod]
    public void Captor_CapturesString()
    {
        var captor = ShimCaptor.For<string>();
        var matched = captor.Matches("prod");
        Assert.IsTrue(matched);
        Assert.IsTrue(captor.HasValue);
        Assert.AreEqual("prod", captor.Value);
    }

    [TestMethod]
    public void Captor_CapturesNullForReferenceType()
    {
        var captor = ShimCaptor.For<string>();
        var matched = captor.Matches(null);
        Assert.IsTrue(matched, "null should match for reference type.");
        Assert.IsTrue(captor.HasValue);
        Assert.IsNull(captor.Value);
    }

    [TestMethod]
    public void Captor_CapturesBoxedInt()
    {
        var captor = ShimCaptor.For<int>();
        var matched = captor.Matches((object)42);
        Assert.IsTrue(matched);
        Assert.AreEqual(42, captor.Value);
    }

    [TestMethod]
    public void Captor_CapturesBoxedBool()
    {
        var captor = ShimCaptor.For<bool>();
        var matched = captor.Matches((object)true);
        Assert.IsTrue(matched);
        Assert.AreEqual(true, captor.Value);
    }

    [TestMethod]
    public void Captor_RejectsNullForNonNullableValueType()
    {
        var captor = ShimCaptor.For<int>();
        var matched = captor.Matches(null);
        Assert.IsFalse(matched, "null should not match non-nullable value type.");
        Assert.IsFalse(captor.HasValue);
    }

    [TestMethod]
    public void Captor_AcceptsNullForNullableValueType()
    {
        var captor = ShimCaptor.For<int?>();
        var matched = captor.Matches(null);
        Assert.IsTrue(matched, "null should match Nullable<int>.");
        Assert.IsTrue(captor.HasValue);
        Assert.IsNull(captor.Value);
    }

    [TestMethod]
    public void Captor_MismatchType_DoesNotCapture()
    {
        var captor = ShimCaptor.For<string>();
        var matched = captor.Matches((object)42); // int, not string
        Assert.IsFalse(matched, "Type mismatch should not match.");
        Assert.IsFalse(captor.HasValue);
    }

    [TestMethod]
    public void Captor_MultipleCaptures_AllStoredInValues()
    {
        var captor = ShimCaptor.For<string>();
        captor.Matches("first");
        captor.Matches("second");
        captor.Matches("third");

        Assert.AreEqual(3, captor.Values.Count);
        Assert.AreEqual("first", captor.Values[0]);
        Assert.AreEqual("second", captor.Values[1]);
        Assert.AreEqual("third", captor.Values[2]);
    }

    [TestMethod]
    public void Captor_Value_ReturnsLastCaptured()
    {
        var captor = ShimCaptor.For<string>();
        captor.Matches("first");
        captor.Matches("second");
        Assert.AreEqual("second", captor.Value);
    }

    [TestMethod]
    public void Captor_HasValue_FalseBeforeCapture_TrueAfter()
    {
        var captor = ShimCaptor.For<string>();
        Assert.IsFalse(captor.HasValue, "HasValue should be false before any capture.");
        captor.Matches("hello");
        Assert.IsTrue(captor.HasValue, "HasValue should be true after capture.");
    }

    [TestMethod]
    public void Captor_Clear_RemovesCapturedValues()
    {
        var captor = ShimCaptor.For<string>();
        captor.Matches("a");
        captor.Matches("b");
        Assert.AreEqual(2, captor.Values.Count);

        captor.Clear();

        Assert.AreEqual(0, captor.Values.Count);
        Assert.IsFalse(captor.HasValue);
    }

    [TestMethod]
    public void Captor_Value_BeforeCapture_ThrowsShimException()
    {
        var captor = ShimCaptor.For<string>();
        var ex = Assert.ThrowsException<ShimException>(() => _ = captor.Value);
        StringAssert.Contains(ex.Message, "ShimCaptor<String>");
        StringAssert.Contains(ex.Message, "Captured count: 0");
        StringAssert.Contains(ex.Message, "Hint:");
    }

    [TestMethod]
    public void Captor_Describe_ReturnsReadableString()
    {
        var captor = ShimCaptor.For<string>();
        StringAssert.Contains(captor.Describe(), "Capture<String>()");
    }

    // =========================================================================
    // ShimArg.Captor<T>() convenience API
    // =========================================================================

    [TestMethod]
    public void ShimArgCaptor_ReturnsShimCaptorInstance()
    {
        var captor = ShimArg.Captor<string>();
        Assert.IsNotNull(captor);
        Assert.IsInstanceOfType<ShimCaptor<string>>(captor);
    }

    [TestMethod]
    public void ShimArgCaptor_CanCaptureViaWithArguments()
    {
        var captor = ShimArg.Captor<string>();
        var fake = new UserRepository("fake");

        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(captor)
                .Returns(fake);

            ShimDispatcher.NewWithArgs<UserRepository>(["captured-value"]);
        }

        Assert.AreEqual("captured-value", captor.Value);
    }

    // =========================================================================
    // Registry / ShimDispatcher dispatcher tests
    // =========================================================================

    [TestMethod]
    public void WithArguments_Captor_CapturesArgument()
    {
        var captor = ShimCaptor.For<string>();
        var fake = new UserRepository("fake");

        using (ShimContext.Create())
        {
            Shim.New<UserRepository>()
                .WithArguments(captor)
                .Returns(fake);

            ShimDispatcher.NewWithArgs<UserRepository>(["hello"]);
        }

        Assert.AreEqual("hello", captor.Value,
            "ShimCaptor should capture the constructor argument.");
    }

    [TestMethod]
    public void WithArguments_EqAndCaptor_CapturesSecondArg()
    {
        var nameCaptor = ShimCaptor.For<int>();
        var fake = new ArgsTestTarget(0);

        using (ShimContext.Create())
        {
            Shim.New<ArgsTestTarget>()
                .WithArguments(ShimArg.Any<string>(), nameCaptor)
                .Returns(fake);

            ShimDispatcher.NewWithArgs<ArgsTestTarget>(["str", (object)99]);
        }

        Assert.AreEqual(99, nameCaptor.Value,
            "Captor in second position should capture the second arg.");
    }

    [TestMethod]
    public void WithArguments_CaptorAndAny_CapturesFirstArg()
    {
        var strCaptor = ShimCaptor.For<string>();
        var fake = new ArgsTestTarget(0);

        using (ShimContext.Create())
        {
            Shim.New<ArgsTestTarget>()
                .WithArguments(strCaptor, ShimArg.Any<int>())
                .Returns(fake);

            ShimDispatcher.NewWithArgs<ArgsTestTarget>(["captured", (object)7]);
        }

        Assert.AreEqual("captured", strCaptor.Value);
    }

    [TestMethod]
    public void WithArguments_Mismatch_DoesNotCapture()
    {
        var captor = ShimCaptor.For<int>();

        using (ShimContext.Create())
        {
            var fake = new UserRepository("fake");
            // captor expects int, but called with a string arg
            Shim.New<UserRepository>()
                .WithArguments(captor)
                .Returns(fake);

            // string is not int — no match, no capture; falls back to real ctor
            ShimDispatcher.NewWithArgs<UserRepository>(["not-an-int"]);
        }

        Assert.IsFalse(captor.HasValue, "Captor should not capture on a type mismatch.");
    }

    [TestMethod]
    public void MultipleRules_OnlySelectedRuleCaptorCaptures()
    {
        // Both rules have a single captor matcher (both match any string).
        // Last-registered rule (rule 2 / captor2) is evaluated first and wins.
        // Rule 1 / captor1 is never evaluated → captor1 has no value.
        var captor1 = ShimCaptor.For<string>();
        var captor2 = ShimCaptor.For<string>();
        var fake1 = new UserRepository("fake1");
        var fake2 = new UserRepository("fake2");

        object? selected;
        using (ShimContext.Create())
        {
            // Rule 1 (order 1): captor1 — matches any string
            Shim.New<UserRepository>()
                .WithArguments(captor1)
                .Returns(fake1);

            // Rule 2 (order 2): captor2 — matches any string, registered last → wins
            Shim.New<UserRepository>()
                .WithArguments(captor2)
                .Returns(fake2);

            selected = ShimDispatcher.NewWithArgs<UserRepository>(["prod"]);
        }

        Assert.AreSame(fake2, selected, "Last registered rule should win.");
        Assert.IsFalse(captor1.HasValue, "captor1 should not capture when its rule was never evaluated.");
        Assert.IsTrue(captor2.HasValue, "captor2 should capture when its rule was selected.");
        Assert.AreEqual("prod", captor2.Value);
    }

    [TestMethod]
    public void CatchAllRule_CaptorInSpecificRule_NotCapturedWhenCatchAllSelected()
    {
        var captor = ShimCaptor.For<string>();
        var catchAllFake = new UserRepository("catch-all");
        var specificFake = new UserRepository("specific");

        using (ShimContext.Create())
        {
            // Catch-all registered first (order 1)
            Shim.New<UserRepository>().Returns(catchAllFake);
            // Specific rule registered second (order 2), with captor
            Shim.New<UserRepository>()
                .WithArguments(ShimArg.Eq("prod"), captor)
                .Returns(specificFake);

            // "dev" doesn't match Eq("prod") in specific rule; catch-all selected
            var result = ShimDispatcher.NewWithArgs<UserRepository>(["dev"]);

            Assert.AreSame(catchAllFake, result, "Catch-all should be used when specific rule doesn't match.");
        }

        Assert.IsFalse(captor.HasValue, "Captor should not capture when its rule is not selected.");
    }

    [TestMethod]
    public void Regression_ArgsFactory_StillWorks_WithCaptor()
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

            Assert.AreEqual("my-arg", captured, "Returns(args => ...) should still work after Phase 9.");
        }
    }

    [TestMethod]
    public void Regression_ContextFactory_StillWorks_WithCaptor()
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

            Assert.AreEqual(typeof(UserRepository), capturedType);
        }
    }

    [TestMethod]
    public void Regression_ParameterlessShim_StillWorks_WithCaptor()
    {
        var fake = new UserRepository("parameterless");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>().Returns(fake);
            var result = ShimDispatcher.New<UserRepository>();
            Assert.AreSame(fake, result);
        }
    }

    // =========================================================================
    // Rewriter integration tests
    // =========================================================================

    [TestMethod]
    public void RewrittenAssembly_CaptorCapturesProdArg()
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

        var captor = ShimCaptor.For<string>();

        using (ShimContext.Create())
        {
            var fakeRepo = Activator.CreateInstance(repoType, "shimmed")!;
            RegisterCaptorShim(repoType, fakeRepo, [captor]);

            var method = serviceType.GetMethod(
                nameof(UserService.GetDisplayNameWithArgRepository),
                BindingFlags.Instance | BindingFlags.Public)!;
            method.Invoke(service, [1]);
        }

        Assert.IsTrue(captor.HasValue, "Captor should have captured a value.");
        Assert.AreEqual("prod", captor.Value,
            "Captor should capture the 'prod' argument passed to new UserRepository(\"prod\").");
    }

    [TestMethod]
    public void RewrittenAssembly_MultipleCalls_CaptureManyValues()
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

        var captor = ShimCaptor.For<string>();

        using (ShimContext.Create())
        {
            var fakeRepo = Activator.CreateInstance(repoType, "shimmed")!;
            RegisterCaptorShim(repoType, fakeRepo, [captor]);

            var method = serviceType.GetMethod(
                nameof(UserService.GetDisplayNameWithArgRepository),
                BindingFlags.Instance | BindingFlags.Public)!;
            method.Invoke(service, [1]);
            method.Invoke(service, [2]);
            method.Invoke(service, [3]);
        }

        Assert.AreEqual(3, captor.Values.Count, "Each call should add a captured value.");
        CollectionAssert.AreEqual(new[] { "prod", "prod", "prod" }, captor.Values.ToArray());
    }

    [TestMethod]
    public void RewrittenAssembly_MultiArgConstructor_PartialCapture()
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

        var strCaptor = ShimCaptor.For<string>();
        var intCaptor = ShimCaptor.For<int>();

        using (ShimContext.Create())
        {
            var fakeTarget = Activator.CreateInstance(argsType, "fake")!;
            RegisterCaptorShim(argsType, fakeTarget, [strCaptor, intCaptor]);

            var method = serviceType.GetMethod(
                nameof(UserService.GetArgsTargetByStringAndInt),
                BindingFlags.Instance | BindingFlags.Public)!;
            method.Invoke(service, ["hello", 42]);
        }

        Assert.AreEqual("hello", strCaptor.Value, "String argument should be captured.");
        Assert.AreEqual(42, intCaptor.Value, "Int argument should be captured.");
    }

    [TestMethod]
    public void RewrittenAssembly_EqAndCaptorMixed_CapturesSecondArg()
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

        var intCaptor = ShimCaptor.For<int>();

        using (ShimContext.Create())
        {
            var fakeTarget = Activator.CreateInstance(argsType, "fake")!;
            RegisterCaptorShim(argsType, fakeTarget, [ShimArg.Any<string>(), intCaptor]);

            var method = serviceType.GetMethod(
                nameof(UserService.GetArgsTargetByStringAndInt),
                BindingFlags.Instance | BindingFlags.Public)!;
            method.Invoke(service, ["world", 77]);
        }

        Assert.AreEqual(77, intCaptor.Value, "Int arg should be captured alongside Any<string>() matcher.");
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

    private static void RegisterCaptorShim(Type targetType, object instance, IShimArgumentMatcher[] matchers)
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
            "captor",
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
