using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using static MiniMockito.Mock;

namespace MiniMockito.Tests;

[TestClass]
public sealed class HardeningTests
{
    [TestMethod]
    public void RootMockBehavior_IsAcceptedByPublicApi()
    {
        var mock = Mock.Of<IHardenedService>(MiniMockito.MockBehavior.Strict);

        var exception = Assert.ThrowsException<MockException>(() => mock.GetName(1));

        StringAssert.Contains(exception.Message, "Method: GetName");
    }

    [TestMethod]
    public void LegacyCoreMockBehavior_RemainsAccepted()
    {
        var mock = Mock.Of<IHardenedService>(MiniMockito.Core.MockBehavior.Lenient);

        Assert.IsNull(mock.GetName(1));
    }

    [TestMethod]
    public void PublicApis_WhenNullExpressionsAreProvided_ThrowArgumentNullException()
    {
        var mock = Mock.Of<IHardenedService>();

        Assert.ThrowsException<ArgumentNullException>(() => When((System.Linq.Expressions.Expression<Action>)null!));
        Assert.ThrowsException<ArgumentNullException>(() => Verify((System.Linq.Expressions.Expression<Action>)null!));
        Assert.ThrowsException<ArgumentNullException>(() => Verify(() => mock.GetName(1), null!));
        Assert.ThrowsException<ArgumentNullException>(() => VerifyNoInteractions(null!));
        Assert.ThrowsException<ArgumentNullException>(() => VerifyNoMoreInteractions(null!));
    }

    [TestMethod]
    public void Times_WhenNegativeCountIsProvided_ThrowsArgumentOutOfRangeException()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Times.Exactly(-1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Times.AtLeast(-1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Times.AtMost(-1));
    }

    [TestMethod]
    public void ThenReturnSequence_WhenValuesArrayIsNull_ThrowsStubbingException()
    {
        var mock = Mock.Of<IHardenedService>();
        object?[]? values = null;

        Assert.ThrowsException<StubbingException>(() => When(() => mock.GetName(1)).ThenReturnSequence(values!));
    }

    [TestMethod]
    public void InOrder_WhenNoMocksAreProvided_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(() => InOrder());
    }

    [TestMethod]
    public void Spy_WhenTargetIsNotInterface_ThrowsUnsupportedMockTargetException()
    {
        var real = new HardenedService();

        Assert.ThrowsException<UnsupportedMockTargetException>(() => Spy.Of(real));
    }

    [TestMethod]
    public async Task InvocationRecording_IsThreadSafeForConcurrentCalls()
    {
        var mock = Mock.Of<IHardenedService>();
        const int taskCount = 8;
        const int callsPerTask = 50;

        var tasks = Enumerable
            .Range(0, taskCount)
            .Select(taskIndex => Task.Run(() =>
            {
                for (var callIndex = 0; callIndex < callsPerTask; callIndex++)
                {
                    mock.GetName(taskIndex * callsPerTask + callIndex);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        var invocations = MockRepository.Default.GetState(mock).Invocations;
        var sequenceNumbers = invocations.Select(invocation => invocation.SequenceNumber).ToArray();

        Assert.AreEqual(taskCount * callsPerTask, invocations.Count);
        Assert.AreEqual(sequenceNumbers.Length, sequenceNumbers.Distinct().Count());
        Assert.IsTrue(invocations.All(invocation => invocation.Method.Name == nameof(IHardenedService.GetName)));
    }

    private interface IHardenedService
    {
        string? GetName(int id);
    }

    private sealed class HardenedService
    {
        public string GetName(int id)
        {
            return id.ToString();
        }
    }
}
