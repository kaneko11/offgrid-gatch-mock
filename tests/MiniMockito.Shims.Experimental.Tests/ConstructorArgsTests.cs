using System.Reflection;
using MiniMockito.Shims.Experimental.Sample;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ConstructorArgsTests
{
    // =========================================================================
    // ShimDispatcher.NewWithArgs<T> unit tests (no IL rewrite required)
    // =========================================================================

    [TestMethod]
    public void NewWithArgs_PassesStringArgumentToArgsFactory()
    {
        using (ShimContext.Create())
        {
            string? captured = null;
            Shim.New<UserRepository>().Returns((object?[] args) =>
            {
                captured = (string?)args[0];
                return new UserRepository("fake");
            });

            ShimDispatcher.NewWithArgs<UserRepository>(["hello"]);

            Assert.AreEqual("hello", captured);
        }
    }

    [TestMethod]
    public void NewWithArgs_PassesIntArgumentToArgsFactory()
    {
        using (ShimContext.Create())
        {
            object? captured = null;
            Shim.New<ArgsTestTarget>().Returns((object?[] args) =>
            {
                captured = args[0];
                return new ArgsTestTarget(0);
            });

            ShimDispatcher.NewWithArgs<ArgsTestTarget>([42]);

            Assert.AreEqual(42, captured);
        }
    }

    [TestMethod]
    public void NewWithArgs_PassesBoolArgumentToArgsFactory()
    {
        using (ShimContext.Create())
        {
            object? captured = null;
            Shim.New<ArgsTestTarget>().Returns((object?[] args) =>
            {
                captured = args[0];
                return new ArgsTestTarget(false);
            });

            ShimDispatcher.NewWithArgs<ArgsTestTarget>([true]);

            Assert.AreEqual(true, captured);
        }
    }

    [TestMethod]
    public void NewWithArgs_PassesNullArgumentToArgsFactory()
    {
        using (ShimContext.Create())
        {
            bool wasCalled = false;
            object? capturedArg = "not-null";
            Shim.New<UserRepository>().Returns((object?[] args) =>
            {
                wasCalled = true;
                capturedArg = args[0];
                return new UserRepository("fake");
            });

            ShimDispatcher.NewWithArgs<UserRepository>([null]);

            Assert.IsTrue(wasCalled);
            Assert.IsNull(capturedArg);
        }
    }

    [TestMethod]
    public void NewWithArgs_ReturnsShimmedInstance_WhenRuleRegistered()
    {
        var fake = new UserRepository("fake");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>().Returns((object?[] _) => fake);

            var result = ShimDispatcher.NewWithArgs<UserRepository>(["prod"]);

            Assert.AreSame(fake, result);
        }
    }

    [TestMethod]
    public void NewWithArgs_CreatesRealInstance_WhenNoRuleRegistered()
    {
        var result = ShimDispatcher.NewWithArgs<UserRepository>(["real-prefix"]);

        Assert.IsNotNull(result);
        Assert.AreEqual("real-prefix-99", result.GetName(99));
    }

    [TestMethod]
    public void NewWithArgs_ContextFactory_ReceivesCorrectTargetTypeAndArguments()
    {
        using (ShimContext.Create())
        {
            Type? capturedType = null;
            object?[]? capturedArgs = null;

            Shim.New<UserRepository>().Returns((ShimConstructorContext ctx) =>
            {
                capturedType = ctx.TargetType;
                capturedArgs = [.. ctx.Arguments];
                return new UserRepository("fake");
            });

            ShimDispatcher.NewWithArgs<UserRepository>(["test-value"]);

            Assert.AreEqual(typeof(UserRepository), capturedType);
            Assert.IsNotNull(capturedArgs);
            Assert.AreEqual(1, capturedArgs!.Length);
            Assert.AreEqual("test-value", capturedArgs[0]);
        }
    }

    [TestMethod]
    public void NewWithArgs_ContextFactory_GetArgument_ReturnsTypedValue()
    {
        using (ShimContext.Create())
        {
            string? capturedString = null;
            Shim.New<UserRepository>().Returns((ShimConstructorContext ctx) =>
            {
                capturedString = ctx.GetArgument<string>(0);
                return new UserRepository("fake");
            });

            ShimDispatcher.NewWithArgs<UserRepository>(["typed-value"]);

            Assert.AreEqual("typed-value", capturedString);
        }
    }

    [TestMethod]
    public void New_ParameterlessStillWorks_AfterPhase7()
    {
        var fake = new UserRepository("fake");
        using (ShimContext.Create())
        {
            Shim.New<UserRepository>().Returns(fake);
            var result = ShimDispatcher.New<UserRepository>();
            Assert.AreSame(fake, result);
        }
    }

    // =========================================================================
    // Rewriter + runtime integration tests
    // =========================================================================

    [TestMethod]
    public void RewriteNewObj_WithStringArg_RewritesCallSite()
    {
        var result = RewriteSampleAssembly([typeof(UserRepository)]);

        Assert.IsTrue(
            result.RewrittenCallSiteDescriptions.Any(d =>
                d.Contains(nameof(UserRepository), StringComparison.Ordinal) &&
                d.Contains("NewWithArgs", StringComparison.Ordinal)),
            "String-arg constructor should be rewritten via NewWithArgs.");
    }

    [TestMethod]
    public void RewrittenAssembly_StringArgConstructor_ShimReturnsReplacement()
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
            RegisterArgsShim(repoType, fakeRepo);

            var method = serviceType.GetMethod(
                nameof(UserService.GetDisplayNameWithArgRepository),
                BindingFlags.Instance | BindingFlags.Public)!;
            var result = (string)method.Invoke(service, [42])!;

            Assert.AreEqual("shimmed-42", result);
        }
    }

    [TestMethod]
    public void RewrittenAssembly_StringArgConstructor_FallsBackToReal_WhenNoShim()
    {
        var outputPath = CreateOutputPath();
        AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputPath,
            new RewriteOptions { TargetTypes = [typeof(UserRepository)] });

        using var loader = new RewrittenAssemblyLoader(outputPath);
        var assembly = loader.Load();
        var serviceType = RequireType(assembly, typeof(UserService).FullName!);
        var service = Activator.CreateInstance(serviceType)!;

        using (ShimContext.Create())
        {
            var method = serviceType.GetMethod(
                nameof(UserService.GetDisplayNameWithArgRepository),
                BindingFlags.Instance | BindingFlags.Public)!;
            var result = (string)method.Invoke(service, [5])!;

            Assert.AreEqual("prod-5", result);
        }
    }

    [TestMethod]
    public void RewrittenAssembly_IntArg_BoxingPreservesValue()
    {
        var outputPath = CreateOutputPath();
        AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputPath,
            new RewriteOptions { TargetTypes = [typeof(ArgsTestTarget)] });

        using var loader = new RewrittenAssemblyLoader(outputPath);
        var assembly = loader.Load();
        var serviceType = RequireType(assembly, typeof(UserService).FullName!);
        var argsTargetType = RequireType(assembly, typeof(ArgsTestTarget).FullName!);
        var service = Activator.CreateInstance(serviceType)!;

        using (ShimContext.Create())
        {
            object? capturedArg = null;
            var fakeReturn = Activator.CreateInstance(argsTargetType, "fake")!;
            var ctx = ShimContext.RequireCurrent();
            ctx.Registry.RegisterNewRule(argsTargetType, args =>
            {
                capturedArg = args[0];
                return fakeReturn;
            }, ctx.ContextId);

            var method = serviceType.GetMethod(
                nameof(UserService.GetArgsTargetByInt),
                BindingFlags.Instance | BindingFlags.Public)!;
            method.Invoke(service, [99]);

            Assert.AreEqual(99, capturedArg, "int argument should be boxed and passed correctly.");
        }
    }

    [TestMethod]
    public void RewrittenAssembly_BoolArg_BoxingPreservesValue()
    {
        var outputPath = CreateOutputPath();
        AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputPath,
            new RewriteOptions { TargetTypes = [typeof(ArgsTestTarget)] });

        using var loader = new RewrittenAssemblyLoader(outputPath);
        var assembly = loader.Load();
        var serviceType = RequireType(assembly, typeof(UserService).FullName!);
        var argsTargetType = RequireType(assembly, typeof(ArgsTestTarget).FullName!);
        var service = Activator.CreateInstance(serviceType)!;

        using (ShimContext.Create())
        {
            object? capturedArg = null;
            var fakeReturn = Activator.CreateInstance(argsTargetType, "fake")!;
            var ctx = ShimContext.RequireCurrent();
            ctx.Registry.RegisterNewRule(argsTargetType, args =>
            {
                capturedArg = args[0];
                return fakeReturn;
            }, ctx.ContextId);

            var method = serviceType.GetMethod(
                nameof(UserService.GetArgsTargetByBool),
                BindingFlags.Instance | BindingFlags.Public)!;
            method.Invoke(service, [true]);

            Assert.AreEqual(true, capturedArg, "bool argument should be boxed and passed correctly.");
        }
    }

    [TestMethod]
    public void RewrittenAssembly_MultipleArgs_OrderPreserved()
    {
        var outputPath = CreateOutputPath();
        AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputPath,
            new RewriteOptions { TargetTypes = [typeof(ArgsTestTarget)] });

        using var loader = new RewrittenAssemblyLoader(outputPath);
        var assembly = loader.Load();
        var serviceType = RequireType(assembly, typeof(UserService).FullName!);
        var argsTargetType = RequireType(assembly, typeof(ArgsTestTarget).FullName!);
        var service = Activator.CreateInstance(serviceType)!;

        using (ShimContext.Create())
        {
            object?[]? capturedArgs = null;
            var fakeReturn = Activator.CreateInstance(argsTargetType, "fake")!;
            var ctx = ShimContext.RequireCurrent();
            ctx.Registry.RegisterNewRule(argsTargetType, args =>
            {
                capturedArgs = args;
                return fakeReturn;
            }, ctx.ContextId);

            var method = serviceType.GetMethod(
                nameof(UserService.GetArgsTargetByStringAndInt),
                BindingFlags.Instance | BindingFlags.Public)!;
            method.Invoke(service, ["hello", 123]);

            Assert.IsNotNull(capturedArgs);
            Assert.AreEqual("hello", capturedArgs![0], "First argument (string) should be in position 0.");
            Assert.AreEqual(123, capturedArgs[1], "Second argument (int) should be in position 1.");
        }
    }

    [TestMethod]
    public void RewriteNewObj_ByRefConstructor_IsSkippedWithDiagnostic()
    {
        var result = RewriteSampleAssembly([typeof(ByRefTarget)]);

        Assert.AreEqual(0, result.RewrittenCallSiteCount,
            "By-ref constructor call site should not be rewritten.");
        Assert.IsTrue(
            result.SkippedCallSiteDescriptions.Any(d =>
                d.Contains("by-ref", StringComparison.OrdinalIgnoreCase) ||
                d.Contains("not supported", StringComparison.OrdinalIgnoreCase)),
            "By-ref parameter should be reported as skipped.");
    }

    [TestMethod]
    public void RewriteNewObj_OriginalAssemblyNotModified()
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

    [TestMethod]
    public void Regression_ParameterlessConstructor_StillRewrittenAfterPhase7()
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
            var fakeRepo = Activator.CreateInstance(repoType, "phase7-fake")!;
            RegisterShim(repoType, fakeRepo);

            var result = InvokeGetDisplayName(serviceType, service, 1);
            Assert.AreEqual("phase7-fake-1", result,
                "Parameterless constructor shim must still work after Phase 7.");
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static RewriteResult RewriteSampleAssembly(Type[] targetTypes)
    {
        return AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            CreateOutputPath(),
            new RewriteOptions { TargetTypes = targetTypes });
    }

    private static string CreateOutputPath()
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "MiniMockito.Shims.Experimental.Tests",
            "ctor-args",
            Guid.NewGuid().ToString("N"));
        return Path.Combine(dir, Path.GetFileName(typeof(UserService).Assembly.Location));
    }

    private static Type RequireType(System.Reflection.Assembly assembly, string fullName)
        => assembly.GetType(fullName, throwOnError: true)!;

    private static string InvokeGetDisplayName(Type serviceType, object service, int id)
    {
        var m = serviceType.GetMethod(
            nameof(UserService.GetDisplayName),
            BindingFlags.Instance | BindingFlags.Public)!;
        return (string)m.Invoke(service, [id])!;
    }

    private static void RegisterShim(Type targetType, object instance)
    {
        var shimNew = typeof(Shim).GetMethod(nameof(Shim.New), BindingFlags.Public | BindingFlags.Static)!;
        var builder = shimNew.MakeGenericMethod(targetType).Invoke(null, null)!;
        var returns = builder.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(m => m.Name == "Returns" && m.GetParameters() is [var p] && p.ParameterType == targetType);
        returns.Invoke(builder, [instance]);
    }

    private static void RegisterArgsShim(Type targetType, object instance)
    {
        var ctx = ShimContext.RequireCurrent();
        ctx.Registry.RegisterNewRule(targetType, _ => instance, ctx.ContextId);
    }

    private static byte[] ComputeHash(string path)
    {
        using var fs = File.OpenRead(path);
        return System.Security.Cryptography.SHA256.HashData(fs);
    }
}
