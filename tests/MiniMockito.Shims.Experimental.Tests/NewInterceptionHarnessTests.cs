using MiniMockito.Shims.Experimental.Sample;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

[TestClass]
[DoNotParallelize]
public sealed class NewInterceptionHarnessTests
{
    [TestMethod]
    public void Harness_CanRunSampleService_WithShim()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            var fake = harness.CreateFake<UserRepository>("harness-fake");
            harness.RegisterShim<UserRepository>(fake);

            var service = harness.Create<UserService>();
            var result = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 42);

            Assert.AreEqual("harness-fake-42", result);
        }
    }

    [TestMethod]
    public void Harness_WithoutShim_UsesRealConstructor()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            // No shim registered — dispatcher falls back to real UserRepository.
            var service = harness.Create<UserService>();
            var result = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 7);

            Assert.AreEqual("real-7", result);
        }
    }

    [TestMethod]
    public void Harness_AfterShimContextDispose_FallsBackToReal()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var service = harness.Create<UserService>();

        using (ShimContext.Create())
        {
            var fake = harness.CreateFake<UserRepository>("fake");
            harness.RegisterShim<UserRepository>(fake);
            Assert.AreEqual("fake-1", harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 1));
        }

        // After context is disposed, the real constructor should be used again.
        Assert.AreEqual("real-2", harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 2));
    }

    [TestMethod]
    public void Harness_ExposesLastRewriteResult()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        Assert.IsNotNull(harness.LastRewriteResult);
        // Phase 7: parameterless + two string-arg UserRepository call sites are all rewritten.
        Assert.AreEqual(3, harness.LastRewriteResult.RewrittenCallSiteCount);
    }

    [TestMethod]
    public void Harness_OutputAssemblyPathIsSet()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        Assert.IsNotNull(harness.OutputAssemblyPath);
        Assert.IsTrue(File.Exists(harness.OutputAssemblyPath), "Rewritten assembly should exist on disk.");
    }

    [TestMethod]
    public void Harness_RewrittenAssemblyIsDifferentFromOriginal()
    {
        var originalPath = typeof(UserService).Assembly.Location;

        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        Assert.IsFalse(
            string.Equals(originalPath, harness.OutputAssemblyPath, StringComparison.OrdinalIgnoreCase),
            "The rewritten assembly must not overwrite the original.");
    }

    [TestMethod]
    public void Harness_GetRewrittenType_ReturnsDifferentTypeFromOriginal()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var rewrittenType = harness.GetRewrittenType(typeof(UserRepository));

        Assert.IsNotNull(rewrittenType);
        Assert.AreEqual(typeof(UserRepository).FullName, rewrittenType.FullName);
        Assert.AreNotSame(typeof(UserRepository), rewrittenType,
            "The rewritten type should come from a different assembly load context.");
    }

    [TestMethod]
    public void Harness_WithNoTargets_Throws()
    {
        var harness = NewInterceptionHarness.Create();

        Assert.ThrowsException<InvalidOperationException>(() => harness.RewriteTargetTypeAssembly());

        harness.Dispose();
    }

    [TestMethod]
    public void Harness_CreateBeforeRewrite_Throws()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>();

        Assert.ThrowsException<InvalidOperationException>(() => harness.Create<UserService>());
    }

    [TestMethod]
    public void Harness_RegisterShimOutsideContext_ThrowsShimException()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var fake = harness.CreateFake<UserRepository>();

        // No active ShimContext.
        Assert.ThrowsException<ShimException>(() => harness.RegisterShim<UserRepository>(fake));
    }

    [TestMethod]
    public void Harness_Dispose_CanBeCalledTwice()
    {
        var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        harness.Dispose();
        harness.Dispose(); // Must not throw.
    }
}
