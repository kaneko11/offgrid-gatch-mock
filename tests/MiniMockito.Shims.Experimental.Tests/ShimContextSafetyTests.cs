using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ShimContextSafetyTests
{
    // Record the count before each test so we can assert per-test delta.
    private int _countAtTestStart;

    [TestInitialize]
    public void TestInitialize()
    {
        _countAtTestStart = ShimContext.ActiveContextCount;
    }

    [TestCleanup]
    public void TestCleanup()
    {
        Assert.AreEqual(_countAtTestStart, ShimContext.ActiveContextCount,
            "A ShimContext created during this test was not disposed. Context leak detected.");
    }

    // -------------------------------------------------------------------------
    // Nested context behavior
    // -------------------------------------------------------------------------

    [TestMethod]
    public void NestedContext_InnerContextSeesItsOwnRules()
    {
        var outerFake = new SampleTarget("outer");
        var innerFake = new SampleTarget("inner");

        using var outer = ShimContext.Create();
        Shim.New<SampleTarget>().Returns(outerFake);
        Assert.AreSame(outerFake, ShimDispatcher.New<SampleTarget>(), "Outer context rule should be active.");

        using (ShimContext.Create())
        {
            Shim.New<SampleTarget>().Returns(innerFake);
            Assert.AreSame(innerFake, ShimDispatcher.New<SampleTarget>(), "Inner context should shadow the outer rule.");
        }

        Assert.AreSame(outerFake, ShimDispatcher.New<SampleTarget>(), "After inner dispose, outer context should be restored.");
    }

    [TestMethod]
    public void NestedContext_InnerContextDoesNotInheritOuterRules()
    {
        var outerFake = new SampleTarget("outer");

        using var outer = ShimContext.Create();
        Shim.New<SampleTarget>().Returns(outerFake);

        using (var inner = ShimContext.Create())
        {
            // Inner context has no rule for SampleTarget — dispatcher falls back to real ctor.
            var result = ShimDispatcher.New<SampleTarget>();
            Assert.AreEqual("real", result.Name, "Inner context should not inherit outer context rules.");
            Assert.IsFalse(inner.Registry.TryFindNewRule(typeof(SampleTarget), out _),
                "Inner registry should be empty unless rules are explicitly added.");
        }
    }

    [TestMethod]
    public void NestedContext_OuterContextRemainsIntactAfterInnerDispose()
    {
        var outerFake = new SampleTarget("outer");

        using var outer = ShimContext.Create();
        Shim.New<SampleTarget>().Returns(outerFake);

        using (ShimContext.Create())
        {
            // Inner scope — no rules added here.
        }

        Assert.AreEqual(1, outer.Registry.Count, "Outer registry should still have 1 rule after inner dispose.");
        Assert.AreSame(outerFake, ShimDispatcher.New<SampleTarget>());
    }

    [TestMethod]
    public void NestedContext_ThreeLevelsRestoreCorrectly()
    {
        var l1Fake = new SampleTarget("l1");
        var l2Fake = new SampleTarget("l2");
        var l3Fake = new SampleTarget("l3");

        using var l1 = ShimContext.Create();
        Shim.New<SampleTarget>().Returns(l1Fake);

        using (ShimContext.Create())
        {
            Shim.New<SampleTarget>().Returns(l2Fake);
            Assert.AreSame(l2Fake, ShimDispatcher.New<SampleTarget>());

            using (ShimContext.Create())
            {
                Shim.New<SampleTarget>().Returns(l3Fake);
                Assert.AreSame(l3Fake, ShimDispatcher.New<SampleTarget>());
            }

            Assert.AreSame(l2Fake, ShimDispatcher.New<SampleTarget>(), "After l3 dispose, l2 should be active.");
        }

        Assert.AreSame(l1Fake, ShimDispatcher.New<SampleTarget>(), "After l2 dispose, l1 should be active.");
    }

    // -------------------------------------------------------------------------
    // Dispose cleanup
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Dispose_ClearsRules()
    {
        var fake = new SampleTarget("fake");
        ShimContext context;

        using (context = ShimContext.Create())
        {
            Shim.New<SampleTarget>().Returns(fake);
            Assert.AreEqual(1, context.Registry.Count, "Rule should be registered.");
        }

        Assert.IsTrue(context.IsDisposed, "Context should be disposed.");
        Assert.AreEqual(0, context.Registry.Count, "Registry should be empty after dispose.");
    }

    [TestMethod]
    public void Dispose_DecrementsActiveContextCount()
    {
        var before = ShimContext.ActiveContextCount;

        var context = ShimContext.Create();
        Assert.AreEqual(before + 1, ShimContext.ActiveContextCount, "Count should increase after Create.");

        context.Dispose();
        Assert.AreEqual(before, ShimContext.ActiveContextCount, "Count should return to original after Dispose.");
    }

    [TestMethod]
    public void Dispose_CalledTwice_IsIdempotent()
    {
        var context = ShimContext.Create();
        context.Dispose();

        // Second Dispose must not throw or corrupt state.
        context.Dispose();

        Assert.IsTrue(context.IsDisposed);
    }

    [TestMethod]
    public void Dispose_RestoresPreviousContext()
    {
        using var outer = ShimContext.Create();
        var inner = ShimContext.Create();

        Assert.AreSame(inner, ShimContext.Current);

        inner.Dispose();

        Assert.AreSame(outer, ShimContext.Current, "After inner dispose, outer should be the current context.");
    }

    // -------------------------------------------------------------------------
    // CleanupException
    // -------------------------------------------------------------------------

    [TestMethod]
    public void CleanupException_IsNullWhenCleanupSucceeds()
    {
        ShimContext context;
        using (context = ShimContext.Create())
        {
            Shim.New<SampleTarget>().Returns(new SampleTarget("fake"));
        }

        Assert.IsNull(context.CleanupException, "CleanupException should be null after normal dispose.");
    }

    // -------------------------------------------------------------------------
    // Context-outside usage exceptions
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ShimNew_OutsideContext_MessageContainsNoActiveShimContext()
    {
        var ex = Assert.ThrowsException<ShimException>(() => Shim.New<SampleTarget>());

        StringAssert.Contains(ex.Message, "No active ShimContext.");
        StringAssert.Contains(ex.Message, "Hint:");
    }

    [TestMethod]
    public void ShimNew_OutsideContext_MessageContainsSupportedPattern()
    {
        var ex = Assert.ThrowsException<ShimException>(() => Shim.New<SampleTarget>());

        StringAssert.Contains(ex.Message, "ShimContext.Create()");
        StringAssert.Contains(ex.Message, "Shim.New<T>().Returns(fake)");
    }

    [TestMethod]
    public void ShimNewBuilder_Returns_AfterContextDispose_ThrowsWithContextId()
    {
        NewShimBuilder<SampleTarget> builder;
        Guid contextId;

        using (var context = ShimContext.Create())
        {
            contextId = context.ContextId;
            builder = Shim.New<SampleTarget>();
        }

        // Context is disposed; calling Returns on the stale builder should throw.
        var ex = Assert.ThrowsException<ShimException>(() => builder.Returns(new SampleTarget("x")));

        StringAssert.Contains(ex.Message, "ShimContext has already been disposed.");
        StringAssert.Contains(ex.Message, contextId.ToString());
        StringAssert.Contains(ex.Message, "Hint:");
    }

    // -------------------------------------------------------------------------
    // ActiveContextCount — leak detection helper
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ActiveContextCount_TracksSingleContextLifetime()
    {
        var before = ShimContext.ActiveContextCount;

        using var ctx = ShimContext.Create();
        Assert.AreEqual(before + 1, ShimContext.ActiveContextCount);

        ctx.Dispose();
        Assert.AreEqual(before, ShimContext.ActiveContextCount);
    }

    [TestMethod]
    public void ActiveContextCount_TracksNestedContextLifetimes()
    {
        var before = ShimContext.ActiveContextCount;

        using var outer = ShimContext.Create();
        Assert.AreEqual(before + 1, ShimContext.ActiveContextCount);

        using (ShimContext.Create())
        {
            Assert.AreEqual(before + 2, ShimContext.ActiveContextCount);
        }

        Assert.AreEqual(before + 1, ShimContext.ActiveContextCount);

        outer.Dispose();
        Assert.AreEqual(before, ShimContext.ActiveContextCount);
    }

    // -------------------------------------------------------------------------
    // Test fixture types
    // -------------------------------------------------------------------------

    public sealed class SampleTarget
    {
        public SampleTarget()
            : this("real")
        {
        }

        public SampleTarget(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
