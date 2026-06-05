using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Core;
using MiniMockito.Exceptions;

namespace MiniMockito.Tests;

[TestClass]
public sealed class MockCreationTests
{
    [TestMethod]
    public void Of_WhenTargetIsInterface_CreatesMock()
    {
        var mock = Mock.Of<ITestService>();

        Assert.IsNotNull(mock);
        Assert.IsInstanceOfType<ITestService>(mock);
    }

    [TestMethod]
    public void Of_WhenTargetIsNotInterface_ThrowsUnsupportedMockTargetException()
    {
        Assert.ThrowsException<UnsupportedMockTargetException>(() => Mock.Of<NotAnInterface>());
    }

    [TestMethod]
    public void MethodCall_IsRecordedInternally()
    {
        var mock = Mock.Of<ITestService>();

        var value = mock.GetValue("abc");

        Assert.AreEqual(0, value);

        var state = MockRepository.Default.GetState(mock);
        var invocation = state.Invocations.Single();
        var expectedMethod = typeof(ITestService).GetMethod(nameof(ITestService.GetValue));

        Assert.AreEqual(expectedMethod, invocation.Method);
        Assert.AreEqual(state.MockId, invocation.MockId);
        Assert.AreEqual(1, invocation.Arguments.Count);
        Assert.AreEqual("abc", invocation.Arguments[0]);
        Assert.AreEqual(0, invocation.ReturnValue);
        Assert.IsNull(invocation.Exception);
        Assert.IsTrue(invocation.SequenceNumber > 0);
        Assert.AreEqual(Environment.CurrentManagedThreadId, invocation.ThreadId);
    }

    [TestMethod]
    public void LenientUnstubbedReferenceReturn_ReturnsNull()
    {
        var mock = Mock.Of<ITestService>();

        var value = mock.GetName();

        Assert.IsNull(value);
    }

    [TestMethod]
    public void LenientUnstubbedValueReturn_ReturnsDefaultValue()
    {
        var mock = Mock.Of<ITestService>();

        var value = mock.GetValue("abc");

        Assert.AreEqual(0, value);
    }

    [TestMethod]
    public async Task LenientUnstubbedTaskReturn_ReturnsCompletedTask()
    {
        var mock = Mock.Of<ITestService>();

        var task = mock.SaveAsync();

        Assert.IsNotNull(task);
        Assert.IsTrue(task.IsCompletedSuccessfully);
        await task;
    }

    [TestMethod]
    public async Task LenientUnstubbedGenericTaskReturn_ReturnsCompletedTaskWithDefaultValue()
    {
        var mock = Mock.Of<ITestService>();

        var task = mock.GetNameAsync();

        Assert.IsNotNull(task);
        Assert.IsTrue(task.IsCompletedSuccessfully);
        Assert.IsNull(await task);
    }

    [TestMethod]
    public async Task LenientUnstubbedValueTaskReturn_ReturnsCompletedValueTask()
    {
        var mock = Mock.Of<ITestService>();

        var valueTask = mock.SaveValueAsync();

        Assert.IsTrue(valueTask.IsCompletedSuccessfully);
        await valueTask;
    }

    [TestMethod]
    public async Task LenientUnstubbedGenericValueTaskReturn_ReturnsCompletedValueTaskWithDefaultValue()
    {
        var mock = Mock.Of<ITestService>();

        var valueTask = mock.GetValueAsync();

        Assert.IsTrue(valueTask.IsCompletedSuccessfully);
        Assert.AreEqual(0, await valueTask);
    }

    private interface ITestService
    {
        string? GetName();

        int GetValue(string key);

        Task SaveAsync();

        Task<string?> GetNameAsync();

        ValueTask SaveValueAsync();

        ValueTask<int> GetValueAsync();
    }

    private sealed class NotAnInterface
    {
    }
}
