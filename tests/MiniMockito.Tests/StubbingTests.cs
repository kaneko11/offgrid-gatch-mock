using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Exceptions;
using static MiniMockito.Mock;

namespace MiniMockito.Tests;

[TestClass]
public sealed class StubbingTests
{
    [TestMethod]
    public void ThenReturn_ReturnsConfiguredValue()
    {
        var mock = Of<IStubbedService>();

        When(() => mock.GetName(1)).ThenReturn("abc");

        Assert.AreEqual("abc", mock.GetName(1));
        Assert.IsNull(mock.GetName(2));
    }

    [TestMethod]
    public void ThenThrow_ThrowsConfiguredException()
    {
        var mock = Of<IStubbedService>();
        var exception = new InvalidOperationException("configured");

        When(() => mock.GetName(1)).ThenThrow(exception);

        var actual = Assert.ThrowsException<InvalidOperationException>(() => mock.GetName(1));
        Assert.AreSame(exception, actual);
        Assert.IsNull(mock.GetName(2));
    }

    [TestMethod]
    public void ThenAnswer_UsesInvocationContext()
    {
        var mock = Of<IStubbedService>();

        When(() => mock.GetName(Any<int>())).ThenAnswer(ctx => $"id={ctx.Arguments[0]}");

        Assert.AreEqual("id=123", mock.GetName(123));
    }

    [TestMethod]
    public void ThenReturnSequence_ReturnsValuesInOrderAndRepeatsLastValue()
    {
        var mock = Of<IStubbedService>();

        When(() => mock.GetName(1)).ThenReturnSequence("a", "b", "c");

        Assert.AreEqual("a", mock.GetName(1));
        Assert.AreEqual("b", mock.GetName(1));
        Assert.AreEqual("c", mock.GetName(1));
        Assert.AreEqual("c", mock.GetName(1));
    }

    [TestMethod]
    public void ThenReturnSequence_WhenNoValues_ThrowsStubbingException()
    {
        var mock = Of<IStubbedService>();

        Assert.ThrowsException<StubbingException>(() => When(() => mock.GetName(1)).ThenReturnSequence());
    }

    [TestMethod]
    public void AnyMatcher_MatchesAnyArgument()
    {
        var mock = Of<IStubbedService>();

        When(() => mock.GetName(Any<int>())).ThenReturn("any");

        Assert.AreEqual("any", mock.GetName(1));
        Assert.AreEqual("any", mock.GetName(999));
    }

    [TestMethod]
    public void EqMatcher_MatchesEqualArgument()
    {
        var mock = Of<IStubbedService>();

        When(() => mock.GetName(Eq(10))).ThenReturn("ten");

        Assert.AreEqual("ten", mock.GetName(10));
        Assert.IsNull(mock.GetName(11));
    }

    [TestMethod]
    public void IsMatcher_MatchesPredicate()
    {
        var mock = Of<IStubbedService>();

        When(() => mock.Echo(Is<string>(value => value.StartsWith("a")))).ThenReturn("match");

        Assert.AreEqual("match", mock.Echo("abc"));
        Assert.IsNull(mock.Echo("xyz"));
    }

    [TestMethod]
    public void NullAndNotNullMatchers_MatchNullability()
    {
        var mock = Of<IStubbedService>();

        When(() => mock.Echo(Null<string>())).ThenReturn("null");
        When(() => mock.Echo(NotNull<string>())).ThenReturn("not-null");

        Assert.AreEqual("null", mock.Echo(null));
        Assert.AreEqual("not-null", mock.Echo("abc"));
    }

    [TestMethod]
    public void InRangeMatcher_MatchesInclusiveRange()
    {
        var mock = Of<IStubbedService>();

        When(() => mock.GetName(InRange(1, 3))).ThenReturn("range");

        Assert.AreEqual("range", mock.GetName(1));
        Assert.AreEqual("range", mock.GetName(2));
        Assert.AreEqual("range", mock.GetName(3));
        Assert.IsNull(mock.GetName(4));
    }

    [TestMethod]
    public async Task TaskStubbing_ReturnsConfiguredTasks()
    {
        var mock = Of<IStubbedService>();

        When(() => mock.SaveAsync()).ThenReturn();
        When(() => mock.GetNameAsync(Any<int>())).ThenReturn("async");

        var saveTask = mock.SaveAsync();

        Assert.IsTrue(saveTask.IsCompletedSuccessfully);
        await saveTask;
        Assert.AreEqual("async", await mock.GetNameAsync(42));
    }

    [TestMethod]
    public async Task ValueTaskStubbing_ReturnsConfiguredValueTasks()
    {
        var mock = Of<IStubbedService>();

        When(() => mock.SaveValueAsync()).ThenReturn();
        When(() => mock.GetValueAsync()).ThenReturn(42);

        var saveValueTask = mock.SaveValueAsync();

        Assert.IsTrue(saveValueTask.IsCompletedSuccessfully);
        await saveValueTask;
        Assert.AreEqual(42, await mock.GetValueAsync());
    }

    private interface IStubbedService
    {
        string? GetName(int id);

        string? Echo(string? value);

        Task SaveAsync();

        Task<string?> GetNameAsync(int id);

        ValueTask SaveValueAsync();

        ValueTask<int> GetValueAsync();
    }
}
