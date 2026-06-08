using System.Reflection;
using System.Security.Cryptography;
using MiniMockito.Shims.Experimental.Sample;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

[TestClass]
public sealed class NewObjRewritePocTests
{
    [TestMethod]
    public void RewriteNewObj_CreatesRewrittenAssemblyAndReportsRewriteCount()
    {
        var originalAssemblyPath = typeof(UserService).Assembly.Location;
        var originalHash = ComputeSha256(originalAssemblyPath);
        var outputAssemblyPath = CreateOutputAssemblyPath();

        var result = AssemblyRewriter.RewriteNewObj(
            originalAssemblyPath,
            outputAssemblyPath,
            new RewriteOptions
            {
                TargetTypes = [typeof(UserRepository)],
            });

        Assert.IsTrue(File.Exists(outputAssemblyPath));
        Assert.AreEqual(1, result.RewrittenCallSiteCount);
        Assert.IsTrue(result.Report.SupportedCallSites.Count >= 1);
        Assert.IsTrue(result.Diagnostics.Any(message => message.Contains("Rewrote", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(originalHash, ComputeSha256(originalAssemblyPath));
        Assert.IsFalse(string.Equals(
            Path.GetFullPath(originalAssemblyPath),
            Path.GetFullPath(result.RewrittenAssemblyPath),
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void RewrittenAssembly_UsesShimDispatcherForAllowlistedNewObj()
    {
        var outputAssemblyPath = RewriteSampleAssembly([typeof(UserRepository)]);

        using var loader = new RewrittenAssemblyLoader(outputAssemblyPath);
        var assembly = loader.Load();
        var userServiceType = RequireType(assembly, typeof(UserService).FullName!);
        var userRepositoryType = RequireType(assembly, typeof(UserRepository).FullName!);
        var service = Activator.CreateInstance(userServiceType)!;

        using (ShimContext.Create())
        {
            var fakeRepository = Activator.CreateInstance(userRepositoryType, "fake")!;
            RegisterNewShim(userRepositoryType, fakeRepository);

            var actual = InvokeGetDisplayName(userServiceType, service, 42);

            Assert.AreEqual("fake-42", actual);
        }
    }

    [TestMethod]
    public void RewrittenAssembly_CleansRulesAfterShimContextDispose()
    {
        var outputAssemblyPath = RewriteSampleAssembly([typeof(UserRepository)]);

        using var loader = new RewrittenAssemblyLoader(outputAssemblyPath);
        var assembly = loader.Load();
        var userServiceType = RequireType(assembly, typeof(UserService).FullName!);
        var userRepositoryType = RequireType(assembly, typeof(UserRepository).FullName!);
        var service = Activator.CreateInstance(userServiceType)!;

        using (ShimContext.Create())
        {
            var fakeRepository = Activator.CreateInstance(userRepositoryType, "fake")!;
            RegisterNewShim(userRepositoryType, fakeRepository);

            Assert.AreEqual("fake-7", InvokeGetDisplayName(userServiceType, service, 7));
        }

        Assert.AreEqual("real-7", InvokeGetDisplayName(userServiceType, service, 7));
    }

    [TestMethod]
    public void RewriteNewObj_DoesNotOverwriteOriginalAssembly()
    {
        var originalAssemblyPath = typeof(UserService).Assembly.Location;
        var originalHash = ComputeSha256(originalAssemblyPath);
        var outputAssemblyPath = CreateOutputAssemblyPath();

        AssemblyRewriter.RewriteNewObj(
            originalAssemblyPath,
            outputAssemblyPath,
            new RewriteOptions
            {
                TargetTypes = [typeof(UserRepository)],
            });

        CollectionAssert.AreEqual(originalHash, ComputeSha256(originalAssemblyPath));
        Assert.IsFalse(string.Equals(originalAssemblyPath, outputAssemblyPath, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void RewriteNewObj_ReportsUnsupportedPatternsWithoutRewritingThem()
    {
        var outputAssemblyPath = CreateOutputAssemblyPath();

        var result = AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputAssemblyPath,
            new RewriteOptions
            {
                TargetTypes = [typeof(UserRepository), typeof(GenericRepository<string>)],
            });

        Assert.AreEqual(1, result.RewrittenCallSiteCount);
        Assert.IsTrue(
            result.Report.UnsupportedCallSites.Any(callSite =>
                callSite.TargetTypeName == typeof(UserRepository).FullName
                && callSite.UnsupportedReason == "ConstructorArgumentsNotSupported"));
        Assert.IsTrue(
            result.Report.UnsupportedCallSites.Any(callSite =>
                callSite.TargetTypeName.Contains(nameof(GenericRepository<string>), StringComparison.Ordinal)
                && callSite.UnsupportedReason == "GenericTypeNotSupported"));
    }

    private static string RewriteSampleAssembly(Type[] targetTypes)
    {
        var outputAssemblyPath = CreateOutputAssemblyPath();

        AssemblyRewriter.RewriteNewObj(
            typeof(UserService).Assembly.Location,
            outputAssemblyPath,
            new RewriteOptions
            {
                TargetTypes = targetTypes,
            });

        return outputAssemblyPath;
    }

    private static string CreateOutputAssemblyPath()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "MiniMockito.Shims.Experimental.Tests",
            Guid.NewGuid().ToString("N"));

        return Path.Combine(outputDirectory, Path.GetFileName(typeof(UserService).Assembly.Location));
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        return assembly.GetType(fullName, throwOnError: true)!;
    }

    private static string InvokeGetDisplayName(Type userServiceType, object service, int id)
    {
        var method = userServiceType.GetMethod(nameof(UserService.GetDisplayName), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new AssertFailedException("UserService.GetDisplayName could not be found in the rewritten assembly.");
        return (string)method.Invoke(service, [id])!;
    }

    private static void RegisterNewShim(Type targetType, object instance)
    {
        var shimNewMethod = typeof(Shim).GetMethod(nameof(Shim.New), BindingFlags.Public | BindingFlags.Static)
            ?? throw new AssertFailedException("Shim.New<T>() could not be found.");

        var builder = shimNewMethod.MakeGenericMethod(targetType).Invoke(null, null)
            ?? throw new AssertFailedException("Shim.New<T>() did not return a builder.");

        var returnsMethod = builder.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method =>
                method.Name == nameof(NewShimBuilder<object>.Returns)
                && method.GetParameters() is [var parameter]
                && parameter.ParameterType == targetType);

        returnsMethod.Invoke(builder, [instance]);
    }

    private static byte[] ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }
}
