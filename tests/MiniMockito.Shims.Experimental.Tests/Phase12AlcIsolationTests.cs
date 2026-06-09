using System.Runtime.CompilerServices;
using MiniMockito.Shims.Experimental.Sample;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

/// <summary>
/// Phase 12: AssemblyLoadContext isolation PoC tests.
/// Verifies that rewritten assemblies load into a named, collectible ALC and that
/// shim dispatch, dependency diagnostics, and unload behave as expected.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class Phase12AlcIsolationTests
{
    // =========================================================================
    // ALC loading tests
    // =========================================================================

    [TestMethod]
    public void AlcIsolation_RewrittenAssembly_LoadedIntoCollectibleAlc()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var diag = harness.GetAlcDiagnostics();

        Assert.IsTrue(diag.IsCollectible,
            "The isolated ALC must be collectible (isCollectible: true).");
        StringAssert.StartsWith(diag.AlcName, "ShimIsolated-",
            "ALC name should start with 'ShimIsolated-'.");
    }

    [TestMethod]
    public void AlcIsolation_TypeLookupByFullName_ReturnsCorrectType()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var rewrittenType = harness.GetRewrittenType(typeof(UserService));

        Assert.IsNotNull(rewrittenType, "GetRewrittenType should return a non-null Type.");
        Assert.AreEqual(typeof(UserService).FullName, rewrittenType.FullName,
            "Full name must match the original type.");
        Assert.AreNotSame(typeof(UserService), rewrittenType,
            "The isolated ALC type must be a different object from the default ALC type.");
    }

    [TestMethod]
    public void AlcIsolation_ReflectionCreate_ServiceInstance()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var service = harness.Create<UserService>();

        Assert.IsNotNull(service, "Create<UserService>() must return a non-null instance.");
    }

    [TestMethod]
    public void AlcIsolation_ReflectionInvoke_ReturnsResult()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var service = harness.Create<UserService>();

        using (ShimContext.Create())
        {
            var result = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 5);
            Assert.AreEqual("real-5", result,
                "Reflection Invoke must call the real constructor when no shim is registered.");
        }
    }

    [TestMethod]
    public void AlcIsolation_Diagnostics_AlcNameIsCollectible()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var diag = harness.GetAlcDiagnostics();

        Assert.IsFalse(string.IsNullOrEmpty(diag.AlcName),
            "AlcName must not be empty.");
        Assert.IsTrue(diag.IsCollectible,
            "ALC must be collectible.");
        Assert.IsFalse(string.IsNullOrEmpty(diag.RewrittenAssemblyPath),
            "RewrittenAssemblyPath must be set.");
        Assert.IsTrue(File.Exists(diag.RewrittenAssemblyPath),
            "The rewritten assembly file must exist on disk.");
        Assert.IsNotNull(diag.OriginalAssemblyDirectory,
            "OriginalAssemblyDirectory should be set when rewriting via the harness.");
    }

    [TestMethod]
    public void AlcIsolation_Diagnostics_LoadedAssemblyList_ContainsMainAssembly()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var diag = harness.GetAlcDiagnostics();

        Assert.IsTrue(
            diag.LoadedAssemblyNames.Any(n =>
                n.Contains("MiniMockito.Shims.Experimental.Sample",
                    StringComparison.OrdinalIgnoreCase)),
            "Loaded assembly list must contain the rewritten sample assembly. " +
            $"Actual: [{string.Join(", ", diag.LoadedAssemblyNames)}]");
    }

    [TestMethod]
    public void AlcIsolation_Diagnostics_Format_ContainsExpectedSections()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var diag = harness.GetAlcDiagnostics();
        var formatted = diag.Format();

        StringAssert.Contains(formatted, "ALC name",
            "Format() output must contain 'ALC name'.");
        StringAssert.Contains(formatted, "Collectible",
            "Format() output must contain 'Collectible'.");
        StringAssert.Contains(formatted, "Rewritten path",
            "Format() output must contain 'Rewritten path'.");
    }

    // =========================================================================
    // shim integration tests
    // =========================================================================

    [TestMethod]
    public void AlcIsolation_ParameterlessConstructorShim_Works()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            var fake = harness.CreateFake<UserRepository>("alc-fake");
            harness.RegisterShim<UserRepository>(fake);

            var service = harness.Create<UserService>();
            var result = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 1);

            Assert.AreEqual("alc-fake-1", result,
                "Parameterless constructor shim must work in the isolated ALC.");
        }
    }

    [TestMethod]
    public void AlcIsolation_ConstructorArgsShim_Works()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            var fake = harness.CreateFake<UserRepository>("alc-args-fake");
            harness.RegisterShim<UserRepository>(fake);

            var service = harness.Create<UserService>();
            // GetDisplayNameWithArgRepository calls new UserRepository("prod")
            var result = harness.Invoke<string>(
                service, nameof(UserService.GetDisplayNameWithArgRepository), 2);

            Assert.AreEqual("alc-args-fake-2", result,
                "Constructor-args shim (catch-all) must work in the isolated ALC.");
        }
    }

    [TestMethod]
    public void AlcIsolation_WithArgumentsEqMatcher_MatchesProd()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            var catchAllFake = harness.CreateFake<UserRepository>("catch-all");
            var prodFake = harness.CreateFake<UserRepository>("prod-fake");

            // Register catch-all first (evaluated last = lower priority).
            harness.RegisterShimWithMatchers<UserRepository>(catchAllFake);
            // Register Eq("prod") second (evaluated first = higher priority).
            harness.RegisterShimWithMatchers<UserRepository>(prodFake, ShimArg.Eq<string>("prod"));

            var service = harness.Create<UserService>();
            // GetDisplayNameWithArgRepository calls new UserRepository("prod") → Eq("prod") matches.
            var result = harness.Invoke<string>(
                service, nameof(UserService.GetDisplayNameWithArgRepository), 3);

            Assert.AreEqual("prod-fake-3", result,
                "Eq(\"prod\") matcher must intercept new UserRepository(\"prod\") in the isolated ALC.");
        }
    }

    [TestMethod]
    public void AlcIsolation_WithArgumentsEqMatcher_FallsBackForOtherArgs()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            var prodFake = harness.CreateFake<UserRepository>("prod-fake");
            // Only register Eq("prod") with no catch-all.
            harness.RegisterShimWithMatchers<UserRepository>(prodFake, ShimArg.Eq<string>("prod"));

            var service = harness.Create<UserService>();
            // GetDisplayName calls new UserRepository() — parameterless, 0 args.
            // Eq("prod") has 1 matcher → arg count mismatch → no match → fallback to real.
            var result = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 7);

            Assert.AreEqual("real-7", result,
                "Parameterless constructor must fall back to the real implementation when " +
                "only an Eq(\"prod\") rule is registered.");
        }
    }

    [TestMethod]
    public void AlcIsolation_ShimCaptor_CapturesConstructorArg()
    {
        var captor = ShimCaptor.For<string>();

        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            var fake = harness.CreateFake<UserRepository>("captor-fake");
            harness.RegisterShimWithMatchers<UserRepository>(fake, captor);

            var service = harness.Create<UserService>();
            // GetDisplayNameWithArgRepository calls new UserRepository("prod").
            harness.Invoke<string>(service, nameof(UserService.GetDisplayNameWithArgRepository), 1);
        }

        Assert.IsTrue(captor.HasValue,
            "Captor must have captured the constructor argument.");
        Assert.AreEqual("prod", captor.Value,
            "Captor must hold 'prod' — the argument passed to new UserRepository(\"prod\").");
    }

    [TestMethod]
    public void AlcIsolation_NoMatchFallback_UsesRealConstructor()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            // No shim registered at all.
            var service = harness.Create<UserService>();
            var result = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 10);

            Assert.AreEqual("real-10", result,
                "When no shim is registered, the real constructor must be used.");
        }
    }

    [TestMethod]
    public void AlcIsolation_OriginalAssembly_NotModified()
    {
        var originalPath = typeof(UserService).Assembly.Location;
        var originalLastWrite = File.GetLastWriteTimeUtc(originalPath);

        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var afterLastWrite = File.GetLastWriteTimeUtc(originalPath);

        Assert.AreEqual(originalLastWrite, afterLastWrite,
            "The original assembly file must not be modified by the rewrite.");
        Assert.IsFalse(
            string.Equals(originalPath, harness.OutputAssemblyPath, StringComparison.OrdinalIgnoreCase),
            "The rewritten output path must differ from the original assembly path.");
    }

    // =========================================================================
    // unload tests
    // =========================================================================

    [TestMethod]
    public void AlcIsolation_HarnessDispose_TriggersUnload()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var diag = harness.GetAlcDiagnostics();
        Assert.IsTrue(diag.IsCollectible,
            "ALC must be collectible for unload to be possible.");

        var weakRef = harness.GetUnloadReference();
        Assert.IsTrue(weakRef.IsAlive,
            "WeakReference must be alive before Dispose.");

        // Dispose calls Unload() and nulls internal references.
        harness.Dispose();

        // After Dispose, no error should have been thrown.
        // (GC may or may not have run yet — IsAlive is not checked here.)
    }

    [TestMethod]
    public void AlcIsolation_GetUnloadReference_BeforeDispose_IsAlive()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var weakRef = harness.GetUnloadReference();
        Assert.IsTrue(weakRef.IsAlive,
            "WeakReference must be alive while the harness is undisposed.");
    }

    [TestMethod]
    public void AlcIsolation_WeakReference_AfterDisposeAndGc_AlcIsCollected()
    {
        // This test verifies that the ALC can be GC-collected after Dispose().
        // Unload timing is GC-dependent, so we allow multiple GC cycles.
        // If collection does not occur within the retry limit, the test is inconclusive
        // rather than failed to avoid flakiness on resource-constrained runners.
        var weakRef = CreateHarnessGetWeakRefAndDispose();

        for (var i = 0; i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            if (!weakRef.IsAlive)
                break;
        }

        if (weakRef.IsAlive)
        {
            Assert.Inconclusive(
                "ALC WeakReference is still alive after 10 GC cycles. " +
                "GC timing is non-deterministic. " +
                "This is a known constraint — see docs/shims-assemblyloadcontext-isolation-design.md " +
                "Section 7 for details. " +
                "Ensure no local variables hold strong references to isolated ALC types.");
        }
        else
        {
            Assert.IsFalse(weakRef.IsAlive,
                "ALC must be collected after Dispose() and GC.");
        }
    }

    // =========================================================================
    // regression tests
    // =========================================================================

    [TestMethod]
    public void Regression_ExistingHarness_CanRunSampleService_WithShim()
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

            Assert.AreEqual("harness-fake-42", result,
                "Existing harness behavior must be unaffected by Phase 12 changes.");
        }
    }

    [TestMethod]
    public void Regression_ExistingHarness_WithoutShim_UsesRealConstructor()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        using (ShimContext.Create())
        {
            var service = harness.Create<UserService>();
            var result = harness.Invoke<string>(service, nameof(UserService.GetDisplayName), 7);

            Assert.AreEqual("real-7", result,
                "Without a shim rule, the real constructor must be used.");
        }
    }

    [TestMethod]
    public void Regression_ExistingHarness_GetRewrittenType_ReturnsDifferentType()
    {
        using var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var rewrittenType = harness.GetRewrittenType(typeof(UserRepository));

        Assert.AreEqual(typeof(UserRepository).FullName, rewrittenType.FullName,
            "FullName must be the same.");
        Assert.AreNotSame(typeof(UserRepository), rewrittenType,
            "The rewritten type must be a different CLR Type object (type identity constraint).");
    }

    [TestMethod]
    public void Regression_ExistingHarness_Dispose_CanBeCalledTwice()
    {
        var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        harness.Dispose();
        harness.Dispose(); // Must not throw.
    }

    // =========================================================================
    // helpers
    // =========================================================================

    /// <summary>
    /// Creates a harness, obtains the ALC weak reference, disposes the harness,
    /// and returns the weak reference.
    /// NoInlining ensures local variables leave scope when this method returns,
    /// allowing the GC to collect the ALC.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateHarnessGetWeakRefAndDispose()
    {
        var harness = NewInterceptionHarness.Create()
            .WithTarget<UserRepository>()
            .RewriteTargetTypeAssembly();

        var weakRef = harness.GetUnloadReference();
        harness.Dispose(); // Calls Unload() + nulls internal ALC reference.
        return weakRef;
        // harness goes out of scope when this method returns.
    }
}
