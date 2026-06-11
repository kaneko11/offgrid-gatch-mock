using System;
using MiniMockito.Shims.Experimental.Sample;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

/// <summary>
/// Phase 17 — high-level <see cref="Shims"/> facade tests (net8.0).
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class Phase17HighLevelApiTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // new interception
    // ─────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void HighLevel_ParameterlessNew_IsShimmed()
    {
        using var shims = Shims.For<UserService>().WithNew<UserRepository>();

        var fakeRepo = shims.CreateFake<UserRepository>("fake");
        shims.New<UserRepository>().Returns(fakeRepo);

        var service = shims.CreateObject(typeof(UserService).FullName!);
        var result = shims.Invoke<string>(service, nameof(UserService.GetDisplayName), 1);

        Assert.AreEqual("fake-1", result);
    }

    [TestMethod]
    public void HighLevel_ConstructorArgsNew_IsShimmed()
    {
        using var shims = Shims.For<UserService>().WithNew<UserRepository>();

        var fakeRepo = shims.CreateFake<UserRepository>("fake");
        shims.New<UserRepository>()
             .WithArguments(ShimArg.Eq<string>("prod"))
             .Returns(fakeRepo);

        var service = shims.CreateObject(typeof(UserService).FullName!);
        var result = shims.Invoke<string>(
            service, nameof(UserService.GetDisplayNameWithArgRepository), 1);

        Assert.AreEqual("fake-1", result);
    }

    [TestMethod]
    public void HighLevel_WithArguments_NoMatch_FallsBackToReal()
    {
        using var shims = Shims.For<UserService>().WithNew<UserRepository>();

        var fakeRepo = shims.CreateFake<UserRepository>("fake");
        shims.New<UserRepository>()
             .WithArguments(ShimArg.Eq<string>("other"))
             .Returns(fakeRepo);

        var service = shims.CreateObject(typeof(UserService).FullName!);
        var result = shims.Invoke<string>(
            service, nameof(UserService.GetDisplayNameWithArgRepository), 5);

        // new UserRepository("prod") does not match Eq("other") → real ctor → "prod-5".
        Assert.AreEqual("prod-5", result);
    }

    [TestMethod]
    public void HighLevel_ShimCaptor_CapturesConstructorArg()
    {
        using var shims = Shims.For<UserService>().WithNew<UserRepository>();

        var captor = ShimCaptor.For<string>();
        var fakeRepo = shims.CreateFake<UserRepository>("fake");
        shims.New<UserRepository>()
             .WithArguments(captor)
             .Returns(fakeRepo);

        var service = shims.CreateObject(typeof(UserService).FullName!);
        shims.Invoke<string>(service, nameof(UserService.GetDisplayNameWithArgRepository), 1);

        Assert.IsTrue(captor.HasValue);
        Assert.AreEqual("prod", captor.Value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // user-defined static method interception
    // ─────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void HighLevel_StaticMethod_IsShimmed()
    {
        using var shims = Shims.For<TimedService>().WithStatic(typeof(StaticClock));

        shims.Static<string>(typeof(StaticClock), nameof(StaticClock.GetName), typeof(int))
             .WithArguments(ShimArg.Eq(1))
             .Returns("fake-clock");

        var service = shims.CreateObject(typeof(TimedService).FullName!);
        var result = shims.Invoke<string>(service, nameof(TimedService.GetDisplayName), 1);

        Assert.AreEqual("fake-clock", result);
    }

    [TestMethod]
    public void HighLevel_VoidStaticMethod_IsShimmed()
    {
        using var shims = Shims.For<LoggingService>().WithStatic(typeof(StaticClock));

        string? recorded = null;
        shims.Static(typeof(StaticClock), nameof(StaticClock.LogCall), typeof(string))
             .Callback(args => recorded = (string?)args[0]);

        var service = shims.CreateObject(typeof(LoggingService).FullName!);
        shims.Invoke(service, nameof(LoggingService.Run), "hello");

        Assert.AreEqual("hello", recorded);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // new + static coexistence in one session
    // ─────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void HighLevel_NewAndStatic_Coexist()
    {
        using var shims = Shims.For<UserService>()
            .WithNew<UserRepository>()
            .WithStatic(typeof(StaticClock));

        var fakeRepo = shims.CreateFake<UserRepository>("fake");
        shims.New<UserRepository>().Returns(fakeRepo);
        shims.Static<string>(typeof(StaticClock), nameof(StaticClock.GetName), typeof(int))
             .Returns("static-name");

        var userService = shims.CreateObject(typeof(UserService).FullName!);
        Assert.AreEqual("fake-1", shims.Invoke<string>(
            userService, nameof(UserService.GetDisplayName), 1));

        var timedService = shims.CreateObject(typeof(TimedService).FullName!);
        Assert.AreEqual("static-name", shims.Invoke<string>(
            timedService, nameof(TimedService.GetDisplayName), 1));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Create<T>() — works for shared contract, throws for concrete
    // ─────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void HighLevel_Create_SharedContract_Works()
    {
        using var shims = Shims.For<CreatableService>().WithNew<UserRepository>();

        var fakeRepo = shims.CreateFake<UserRepository>("fake");
        shims.New<UserRepository>().Returns(fakeRepo);

        IShimCreatable service = shims.Create<IShimCreatable>();
        var result = service.Describe();

        Assert.AreEqual("fake-99", result);
    }

    [TestMethod]
    public void HighLevel_Create_ConcreteType_ThrowsWithGuidance()
    {
        using var shims = Shims.For<UserService>().WithNew<UserRepository>();

        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => shims.Create<UserService>());

        StringAssert.Contains(ex.Message, "isolated load context");
        StringAssert.Contains(ex.Message, "CreateObject");
        StringAssert.Contains(ex.Message, "Invoke");
    }

    [TestMethod]
    public void HighLevel_CreateObject_And_Invoke_Fallback_Works()
    {
        using var shims = Shims.For<UserService>().WithNew<UserRepository>();

        var fakeRepo = shims.CreateFake<UserRepository>("fake");
        shims.New<UserRepository>().Returns(fakeRepo);

        var service = shims.CreateObject(typeof(UserService).FullName!);
        var result = shims.Invoke<string>(service, nameof(UserService.GetDisplayName), 1);

        Assert.AreEqual("fake-1", result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // lifecycle / validation
    // ─────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void HighLevel_WithNew_AfterFinalize_Throws()
    {
        using var shims = Shims.For<UserService>().WithNew<UserRepository>();

        // First CreateObject finalizes the rewrite.
        shims.CreateObject(typeof(UserService).FullName!);

        Assert.ThrowsException<InvalidOperationException>(() => shims.WithNew<UserRepository>());
    }

    [TestMethod]
    public void HighLevel_WithStatic_AfterFinalize_Throws()
    {
        using var shims = Shims.For<UserService>().WithNew<UserRepository>();

        shims.CreateObject(typeof(UserService).FullName!);

        Assert.ThrowsException<InvalidOperationException>(() => shims.WithStatic(typeof(StaticClock)));
    }

    [TestMethod]
    public void HighLevel_New_UnregisteredTarget_Throws()
    {
        using var shims = Shims.For<UserService>().WithStatic(typeof(StaticClock));

        // UserRepository was never registered with WithNew.
        Assert.ThrowsException<InvalidOperationException>(() => shims.New<UserRepository>());
    }

    [TestMethod]
    public void HighLevel_Dispose_ClearsRules_AndIsIdempotent()
    {
        var shims = Shims.For<UserService>().WithNew<UserRepository>();

        var fakeRepo = shims.CreateFake<UserRepository>("fake");
        shims.New<UserRepository>().Returns(fakeRepo);

        // Rules are active inside the session.
        Assert.IsTrue(ShimContext.ActiveContextCount >= 1);

        shims.Dispose();
        shims.Dispose(); // must not throw

        // Using the session after dispose throws.
        Assert.ThrowsException<ObjectDisposedException>(
            () => shims.New<UserRepository>());
    }

    [TestMethod]
    public void HighLevel_Diagnostics_AreForwarded()
    {
        using var shims = Shims.For<UserService>().WithNew<UserRepository>();

        var fakeRepo = shims.CreateFake<UserRepository>("fake");
        shims.New<UserRepository>().Returns(fakeRepo);

        var service = shims.CreateObject(typeof(UserService).FullName!);
        shims.Invoke<string>(service, nameof(UserService.GetDisplayName), 1);

        Assert.IsNotNull(shims.LastNewDispatchDiagnostics);
        Assert.IsNotNull(shims.GetAlcDiagnostics());
    }
}
