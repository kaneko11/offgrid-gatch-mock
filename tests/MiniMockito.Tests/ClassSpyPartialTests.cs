using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Core;
using static MiniMockito.Mock;

namespace MiniMockito.Tests;

[TestClass]
public sealed class ClassSpyPartialTests
{
    [TestMethod]
    public void ClassSpy_CanBeCreated()
    {
        var spy = Spy.Class<ClassSpySampleService>();

        Assert.IsNotNull(spy);
        Assert.IsInstanceOfType<ClassSpySampleService>(spy);
        Assert.AreNotEqual(typeof(ClassSpySampleService), spy.GetType());
    }

    [TestMethod]
    public void ClassSpy_WhenNoStubMatches_CallsBaseImplementation()
    {
        var spy = Spy.Class<ClassSpySampleService>();

        var result = spy.GetName(1);

        Assert.AreEqual("real-1", result);
        Assert.AreEqual(1, spy.NameCallCount);
    }

    [TestMethod]
    public void ClassSpy_WhenStubMatches_UsesThenReturnInsteadOfBaseImplementation()
    {
        var spy = Spy.Class<ClassSpySampleService>();

        When(() => spy.GetName(2)).ThenReturn("mocked");

        Assert.AreEqual("mocked", spy.GetName(2));
        Assert.AreEqual(0, spy.NameCallCount);
        Assert.AreEqual("real-3", spy.GetName(3));
        Assert.AreEqual(1, spy.NameCallCount);
    }

    [TestMethod]
    public void ClassSpy_ThenThrow_Works()
    {
        var spy = Spy.Class<ClassSpySampleService>();
        var exception = new InvalidOperationException("configured");

        When(() => spy.GetName(2)).ThenThrow(exception);

        var actual = Assert.ThrowsException<InvalidOperationException>(() => spy.GetName(2));
        Assert.AreSame(exception, actual);
        Assert.AreEqual(0, spy.NameCallCount);
    }

    [TestMethod]
    public void ClassSpy_ThenAnswer_Works()
    {
        var spy = Spy.Class<ClassSpySampleService>();

        When(() => spy.GetName(Any<int>())).ThenAnswer(ctx => $"answer-{ctx.Arguments[0]}");

        Assert.AreEqual("answer-5", spy.GetName(5));
        Assert.AreEqual(0, spy.NameCallCount);
    }

    [TestMethod]
    public void ClassSpy_RecordsInvocationsForBaseAndStubbedCalls()
    {
        var spy = Spy.Class<ClassSpySampleService>();
        When(() => spy.GetName(2)).ThenReturn("mocked");

        spy.GetName(1);
        spy.GetName(2);

        var invocations = MockRepository.Default.GetState(spy).Invocations;

        Assert.AreEqual(2, invocations.Count);
        Assert.IsTrue(invocations.All(invocation => invocation.Method.Name == nameof(ClassSpySampleService.GetName)));
        Assert.AreEqual("real-1", invocations[0].ReturnValue);
        Assert.AreEqual("mocked", invocations[1].ReturnValue);
    }

    [TestMethod]
    public void ClassSpy_VerifyAndVerifyNoMoreInteractions_Work()
    {
        var spy = Spy.Class<ClassSpySampleService>();
        When(() => spy.GetName(2)).ThenReturn("mocked");

        spy.GetName(1);
        spy.GetName(2);

        Verify(() => spy.GetName(1), Times.Once());
        Verify(() => spy.GetName(2), Times.Once());
        VerifyNoMoreInteractions(spy);
    }

    [TestMethod]
    public void ClassPartialMock_WithCallBaseOption_CallsBaseWhenNoStubMatches()
    {
        var partial = Mock.Class<ClassSpySampleService>(ClassMockOptions.CallBase);

        When(() => partial.GetName(2)).ThenReturn("mocked");

        Assert.AreEqual("real-1", partial.GetName(1));
        Assert.AreEqual("mocked", partial.GetName(2));
        Assert.AreEqual(1, partial.NameCallCount);
    }

    [TestMethod]
    public void ExistingInterfaceSpy_StillWorks()
    {
        var real = new InterfaceSpyRealService();
        var spy = Spy.Of<IInterfaceSpyService>(real);

        When(() => spy.GetName(2)).ThenReturn("mocked");

        Assert.AreEqual("real-1", spy.GetName(1));
        Assert.AreEqual("mocked", spy.GetName(2));
        Assert.AreEqual(1, real.CallCount);
    }
}

public class ClassSpySampleService
{
    public int NameCallCount { get; private set; }

    public virtual string GetName(int id)
    {
        NameCallCount++;
        return $"real-{id}";
    }
}

public interface IInterfaceSpyService
{
    string GetName(int id);
}

public sealed class InterfaceSpyRealService : IInterfaceSpyService
{
    public int CallCount { get; private set; }

    public string GetName(int id)
    {
        CallCount++;
        return $"real-{id}";
    }
}
