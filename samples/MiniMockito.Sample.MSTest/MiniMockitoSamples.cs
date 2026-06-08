using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MiniMockito.Mock;

namespace MiniMockito.Sample.MSTest;

[TestClass]
public sealed class MiniMockitoSamples
{
    [TestMethod]
    public void InterfaceMock_WhenThenReturnAndVerify()
    {
        var service = Mock.Of<IUserService>();

        When(() => service.GetName(Any<int>()))
            .ThenReturn("abc");

        Assert.AreEqual("abc", service.GetName(123));
        Verify(() => service.GetName(123), Times.Once());
    }

    [TestMethod]
    public void MatcherAndCaptor_CanBeUsedInTheSameTest()
    {
        var service = Mock.Of<IUserService>();
        var captor = Capture<string>();

        When(() => service.Find(Is<string>(value => value.StartsWith("user-", StringComparison.Ordinal))))
            .ThenReturn("matched");

        Assert.AreEqual("matched", service.Find("user-123"));
        service.Save("captured");

        Verify(() => service.Save(captor.Value));
        Assert.AreEqual("captured", captor.CapturedValue);
    }

    [TestMethod]
    public void InterfaceSpy_DelegatesWhenNoStubMatches()
    {
        var real = new RealUserService();
        var spy = Spy.Of<IUserService>(real);

        When(() => spy.GetName(0))
            .ThenReturn("stubbed");

        Assert.AreEqual("stubbed", spy.GetName(0));
        Assert.AreEqual("real-7", spy.GetName(7));
        Assert.AreEqual(1, real.GetNameCallCount);
    }

    [TestMethod]
    public void ClassProxy_StubsPublicVirtualMethod()
    {
        var repository = Mock.Class<UserRepository>();

        When(() => repository.FindName(1))
            .ThenReturn("mocked");

        Assert.AreEqual("mocked", repository.FindName(1));
        Verify(() => repository.FindName(1), Times.Once());
    }

    [TestMethod]
    public void ClassSpyPartialMock_CallsBaseWhenNoStubMatches()
    {
        var calculator = Spy.Class<TaxCalculator>();

        When(() => calculator.GetRate("test"))
            .ThenReturn(0.20m);

        Assert.AreEqual(0.20m, calculator.GetRate("test"));
        Assert.AreEqual(0.10m, calculator.GetRate("default"));
        Assert.AreEqual(1, calculator.BaseCallCount);
    }

    [TestMethod]
    public async Task AsyncMethod_StubsLogicalResult()
    {
        var service = Mock.Of<IUserService>();

        When(() => service.GetNameAsync(Any<int>()))
            .ThenReturn("async");

        Assert.AreEqual("async", await service.GetNameAsync(42));
        Verify(() => service.GetNameAsync(42), Times.Once());
    }

    public interface IUserService
    {
        string? GetName(int id);

        string? Find(string? key);

        void Save(string value);

        Task<string?> GetNameAsync(int id);
    }

    public sealed class RealUserService : IUserService
    {
        public int GetNameCallCount { get; private set; }

        public string GetName(int id)
        {
            GetNameCallCount++;
            return $"real-{id}";
        }

        public string? Find(string? key)
        {
            return key;
        }

        public void Save(string value)
        {
        }

        public Task<string?> GetNameAsync(int id)
        {
            return Task.FromResult<string?>($"real-async-{id}");
        }
    }

    public class UserRepository
    {
        public virtual string? FindName(int id)
        {
            return $"real-{id}";
        }
    }

    public class TaxCalculator
    {
        public int BaseCallCount { get; private set; }

        public virtual decimal GetRate(string category)
        {
            BaseCallCount++;
            return category == "default" ? 0.10m : 0.08m;
        }
    }
}
