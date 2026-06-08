using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Exceptions;
using static MiniMockito.Mock;

namespace MiniMockito.Tests;

[TestClass]
public sealed class ReleasePrepRegressionTests
{
    [TestMethod]
    public void ReadmeV2_InterfaceMockClassProxyAndClassSpyExamples_CompileAndRun()
    {
        var service = Mock.Of<IReadmeV2UserService>();
        var repository = Mock.Class<ReadmeV2Repository>();
        var calculator = Spy.Class<ReadmeV2Calculator>();

        When(() => service.GetName(Any<int>())).ThenReturn("abc");
        When(() => repository.FindName(1)).ThenReturn("mocked");
        When(() => calculator.GetRate("test")).ThenReturn(0.20m);

        Assert.AreEqual("abc", service.GetName(123));
        Assert.AreEqual("mocked", repository.FindName(1));
        Assert.AreEqual(0.20m, calculator.GetRate("test"));
        Assert.AreEqual(0.10m, calculator.GetRate("default"));

        Verify(() => service.GetName(123), Times.Once());
        Verify(() => repository.FindName(1), Times.Once());
        Verify(() => calculator.GetRate("test"), Times.Once());
        Verify(() => calculator.GetRate("default"), Times.Once());
        VerifyNoMoreInteractions(service);
        VerifyNoMoreInteractions(repository);
        VerifyNoMoreInteractions(calculator);
    }

    [TestMethod]
    public void ReadmeV2_MatchersCaptorAndInterfaceSpyExamples_CompileAndRun()
    {
        var service = Mock.Of<IReadmeV2UserService>();
        var captor = Capture<string>();

        When(() => service.Find(Null<string>())).ThenReturn("missing");
        When(() => service.Find(NotNull<string>())).ThenReturn("present");
        When(() => service.GetName(InRange(1, 5))).ThenReturn("range");

        Assert.AreEqual("missing", service.Find(null));
        Assert.AreEqual("present", service.Find("user"));
        Assert.AreEqual("range", service.GetName(3));

        service.Save("captured");
        Verify(() => service.Save(captor.Value));
        Assert.AreEqual("captured", captor.CapturedValue);

        var spy = Spy.Of<IReadmeV2UserService>(new ReadmeV2UserService());
        When(() => spy.GetName(0)).ThenReturn("stubbed");

        Assert.AreEqual("stubbed", spy.GetName(0));
        Assert.AreEqual("real-7", spy.GetName(7));
    }

    [TestMethod]
    public async Task ReadmeV2_AsyncExample_CompilesAndRuns()
    {
        var service = Mock.Of<IReadmeV2UserService>();

        When(() => service.GetNameAsync(Any<int>())).ThenReturn("async");

        Assert.AreEqual("async", await service.GetNameAsync(42));
        Verify(() => service.GetNameAsync(42), Times.Once());
        VerifyNoMoreInteractions(service);
    }

    [TestMethod]
    public void InterfaceMockAndClassProxy_CanBeVerifiedInOrderTogether()
    {
        var interfaceStep = Mock.Of<IReadmeV2WorkflowStep>();
        var classStep = Mock.Class<ReadmeV2WorkflowStep>();

        interfaceStep.Start();
        classStep.Save();
        interfaceStep.End();

        var order = InOrder(interfaceStep, classStep);
        order.Verify(() => interfaceStep.Start());
        order.Verify(() => classStep.Save());
        order.Verify(() => interfaceStep.End());

        VerifyNoMoreInteractions(interfaceStep);
        VerifyNoMoreInteractions(classStep);
    }

    [TestMethod]
    public void UnsupportedV2ShimScenarios_RemainUnsupported()
    {
        var sealedException = Assert.ThrowsException<ClassProxyException>(() => Mock.Class<ReadmeV2SealedService>());
        StringAssert.Contains(sealedException.Message, "Reason: SealedClass");

        var mixed = Mock.Class<ReadmeV2MixedService>();
        var nonVirtualException = Assert.ThrowsException<ClassProxyException>(
            () => When(() => mixed.NonVirtual()).ThenReturn("unsupported"));
        StringAssert.Contains(nonVirtualException.Message, "Reason: NonVirtualMethod");
    }

    private interface IReadmeV2UserService
    {
        string? GetName(int id);

        string? Find(string? key);

        void Save(string value);

        Task<string?> GetNameAsync(int id);
    }

    private sealed class ReadmeV2UserService : IReadmeV2UserService
    {
        public string GetName(int id)
        {
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

    public class ReadmeV2Repository
    {
        public virtual string? FindName(int id)
        {
            return $"real-{id}";
        }
    }

    public class ReadmeV2Calculator
    {
        public virtual decimal GetRate(string category)
        {
            return category == "default" ? 0.10m : 0.08m;
        }
    }

    private interface IReadmeV2WorkflowStep
    {
        void Start();

        void End();
    }

    public class ReadmeV2WorkflowStep
    {
        public virtual void Save()
        {
        }
    }

    public sealed class ReadmeV2SealedService
    {
        public string GetName()
        {
            return "sealed";
        }
    }

    public class ReadmeV2MixedService
    {
        public virtual string Virtual()
        {
            return "virtual";
        }

        public string NonVirtual()
        {
            return "non-virtual";
        }
    }
}
