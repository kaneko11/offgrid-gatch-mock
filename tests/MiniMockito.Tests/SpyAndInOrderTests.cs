using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using static MiniMockito.Mock;

namespace MiniMockito.Tests;

[TestClass]
public sealed class SpyAndInOrderTests
{
    [TestMethod]
    public void Spy_WhenNoStubMatches_CallsRealImplementation()
    {
        var real = new RealCalculator();
        var spy = Spy.Of<ICalculator>(real);

        var result = spy.Add(2, 3);

        Assert.AreEqual(5, result);
        Assert.AreEqual(1, real.AddCallCount);
    }

    [TestMethod]
    public void Spy_WhenStubMatches_UsesStubInsteadOfRealImplementation()
    {
        var real = new RealCalculator();
        var spy = Spy.Of<ICalculator>(real);

        When(() => spy.Add(2, 3)).ThenReturn(99);

        Assert.AreEqual(99, spy.Add(2, 3));
        Assert.AreEqual(0, real.AddCallCount);
        Assert.AreEqual(9, spy.Add(4, 5));
        Assert.AreEqual(1, real.AddCallCount);
    }

    [TestMethod]
    public void Spy_RecordsInvocations()
    {
        var spy = Spy.Of<ICalculator>(new RealCalculator());

        spy.Add(1, 2);

        var invocation = MockRepository.Default.GetState(spy).Invocations.Single();
        Assert.AreEqual(nameof(ICalculator.Add), invocation.Method.Name);
        Assert.AreEqual(3, invocation.ReturnValue);
    }

    [TestMethod]
    public void InOrder_VerifiesInvocationsAcrossMultipleMocks()
    {
        var first = Of<IWorkflowStep>();
        var second = Of<IWorkflowStep>();

        first.Start();
        second.Save();
        first.End();

        var order = InOrder(first, second);

        order.Verify(() => first.Start());
        order.Verify(() => second.Save());
        order.Verify(() => first.End());

        VerifyNoMoreInteractions(first);
        VerifyNoMoreInteractions(second);
    }

    [TestMethod]
    public void InOrder_WhenOrderDoesNotMatch_FailureMessageIncludesExpectedAndActualOrder()
    {
        var first = Of<IWorkflowStep>();
        var second = Of<IWorkflowStep>();

        second.Save();
        first.Start();

        var order = InOrder(first, second);

        order.Verify(() => first.Start());
        var exception = Assert.ThrowsException<VerificationException>(() => order.Verify(() => second.Save()));

        StringAssert.Contains(exception.Message, "Expected order");
        StringAssert.Contains(exception.Message, "Actual order");
        StringAssert.Contains(exception.Message, nameof(IWorkflowStep.Save));
    }

    [TestMethod]
    public void ReadmeStyleExamples_CompileAndRun()
    {
        var service = Of<IReadmeService>();

        When(() => service.GetName(Any<int>()))
            .ThenReturn("abc");

        Assert.AreEqual("abc", service.GetName(123));
        Verify(() => service.GetName(123), Times.Once());

        var captor = Capture<string>();
        service.Save("captured");
        Verify(() => service.Save(captor.Value));
        Assert.AreEqual("captured", captor.CapturedValue);

        var spy = Spy.Of<IReadmeService>(new ReadmeService());
        When(() => spy.GetName(0)).ThenReturn("stubbed");

        Assert.AreEqual("stubbed", spy.GetName(0));
        Assert.AreEqual("real-7", spy.GetName(7));
    }

    private interface ICalculator
    {
        int Add(int left, int right);
    }

    private sealed class RealCalculator : ICalculator
    {
        public int AddCallCount { get; private set; }

        public int Add(int left, int right)
        {
            AddCallCount++;
            return left + right;
        }
    }

    private interface IWorkflowStep
    {
        void Start();

        void Save();

        void End();
    }

    private interface IReadmeService
    {
        string? GetName(int id);

        void Save(string value);
    }

    private sealed class ReadmeService : IReadmeService
    {
        public string GetName(int id)
        {
            return $"real-{id}";
        }

        public void Save(string value)
        {
        }
    }
}
