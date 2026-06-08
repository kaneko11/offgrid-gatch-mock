using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

[TestClass]
public sealed class ShimProjectSkeletonTests
{
    [TestMethod]
    public void ShimContextCreate_CreatesContext()
    {
        using var context = ShimContext.Create();

        Assert.AreNotEqual(Guid.Empty, context.ContextId);
    }

    [TestMethod]
    public void ShimNewReturnsInstance_RegistersRule()
    {
        using var context = ShimContext.Create();
        var fake = new UserRepository("fake");

        var rule = Shim.New<UserRepository>().Returns(fake);

        Assert.AreEqual(typeof(UserRepository), rule.TargetType);
        Assert.AreEqual(context.ContextId, rule.ContextId);
    }

    [TestMethod]
    public void ShimDispatcherNew_WhenRuleExists_ReturnsFakeInstance()
    {
        using var context = ShimContext.Create();
        var fake = new UserRepository("fake");

        Shim.New<UserRepository>().Returns(fake);

        var actual = ShimDispatcher.New<UserRepository>();

        Assert.AreSame(fake, actual);
    }

    [TestMethod]
    public void ShimDispatcherNew_WhenFactoryRuleExists_ReturnsFactoryResult()
    {
        using var context = ShimContext.Create();
        var calls = 0;

        Shim.New<UserRepository>().Returns(() => new UserRepository($"fake-{++calls}"));

        var first = ShimDispatcher.New<UserRepository>();
        var second = ShimDispatcher.New<UserRepository>();

        Assert.AreEqual("fake-1", first.Name);
        Assert.AreEqual("fake-2", second.Name);
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public void ShimDispatcherNew_WhenNoRuleExists_CreatesRealInstanceWithParameterlessConstructor()
    {
        var actual = ShimDispatcher.New<UserRepository>();

        Assert.IsNotNull(actual);
        Assert.AreEqual("real", actual.Name);
    }

    [TestMethod]
    public void ShimContextDispose_CleansUpRules()
    {
        var fake = new UserRepository("fake");

        using (ShimContext.Create())
        {
            Shim.New<UserRepository>().Returns(fake);

            Assert.AreSame(fake, ShimDispatcher.New<UserRepository>());
        }

        var actual = ShimDispatcher.New<UserRepository>();

        Assert.AreNotSame(fake, actual);
        Assert.AreEqual("real", actual.Name);
    }

    [TestMethod]
    public void ShimNew_WhenCalledOutsideContext_ThrowsClearException()
    {
        var exception = Assert.ThrowsException<ShimException>(() => Shim.New<UserRepository>());

        StringAssert.Contains(exception.Message, "No active ShimContext.");
        StringAssert.Contains(exception.Message, "Shim.New<T>() requires an active shim context.");
        StringAssert.Contains(exception.Message, "Hint:");
    }

    [TestMethod]
    public void NestedShimContexts_DoNotMixRules()
    {
        var outerFake = new UserRepository("outer");
        var innerFake = new UserRepository("inner");

        using (ShimContext.Create())
        {
            Shim.New<UserRepository>().Returns(outerFake);

            Assert.AreSame(outerFake, ShimDispatcher.New<UserRepository>());

            using (ShimContext.Create())
            {
                Shim.New<UserRepository>().Returns(innerFake);

                Assert.AreSame(innerFake, ShimDispatcher.New<UserRepository>());
            }

            Assert.AreSame(outerFake, ShimDispatcher.New<UserRepository>());
        }
    }

    [TestMethod]
    public void ShimDispatcherNew_WhenFallbackHasNoParameterlessConstructor_ThrowsClearException()
    {
        var exception = Assert.ThrowsException<ShimUnsupportedException>(
            () => ShimDispatcher.New<NoParameterlessConstructor>());

        StringAssert.Contains(exception.Message, "Target type:");
        StringAssert.Contains(exception.Message, "Reason: PublicParameterlessConstructorNotFound");
        StringAssert.Contains(exception.Message, "Supported patterns:");
        StringAssert.Contains(exception.Message, "Unsupported patterns:");
        StringAssert.Contains(exception.Message, "Hint:");
    }

    public class UserRepository
    {
        public UserRepository()
            : this("real")
        {
        }

        public UserRepository(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public class NoParameterlessConstructor
    {
        public NoParameterlessConstructor(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
