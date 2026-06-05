using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using static MiniMockito.Mock;

namespace MiniMockito.Tests;

[TestClass]
public sealed class VerificationTests
{
    [TestMethod]
    public void Verify_WithOneCall_SucceedsAndMarksInvocationVerified()
    {
        var mock = Of<IVerifiedService>();

        mock.Save("abc");

        Verify(() => mock.Save("abc"));

        var invocation = MockRepository.Default.GetState(mock).Invocations.Single();
        Assert.IsTrue(invocation.IsVerified);
    }

    [TestMethod]
    public void Verify_DoesNotRecordVerifyExpressionAsInvocation()
    {
        var mock = Of<IVerifiedService>();
        mock.Save("abc");
        var state = MockRepository.Default.GetState(mock);
        var countBeforeVerify = state.Invocations.Count;

        Verify(() => mock.Save("abc"));

        Assert.AreEqual(countBeforeVerify, state.Invocations.Count);
    }

    [TestMethod]
    public void Verify_WithTimesExactly_Succeeds()
    {
        var mock = Of<IVerifiedService>();

        mock.Save("a");
        mock.Save("a");

        Verify(() => mock.Save("a"), Times.Exactly(2));
    }

    [TestMethod]
    public void Verify_WithNever_SucceedsWhenNoMatchingCallExists()
    {
        var mock = Of<IVerifiedService>();

        mock.Save("a");

        Verify(() => mock.Save("b"), Times.Never());
    }

    [TestMethod]
    public void Verify_WithAtLeast_Succeeds()
    {
        var mock = Of<IVerifiedService>();

        mock.Save("a");
        mock.Save("a");
        mock.Save("a");

        Verify(() => mock.Save("a"), Times.AtLeast(2));
    }

    [TestMethod]
    public void Verify_WithAtMost_Succeeds()
    {
        var mock = Of<IVerifiedService>();

        mock.Save("a");
        mock.Save("a");

        Verify(() => mock.Save("a"), Times.AtMost(2));
    }

    [TestMethod]
    public void VerifyNoInteractions_SucceedsWhenMockHasNoInvocations()
    {
        var mock = Of<IVerifiedService>();

        VerifyNoInteractions(mock);
    }

    [TestMethod]
    public void VerifyNoInteractions_FailsWhenMockHasInvocation()
    {
        var mock = Of<IVerifiedService>();
        mock.Save("abc");

        var exception = Assert.ThrowsException<VerificationException>(() => VerifyNoInteractions(mock));

        StringAssert.Contains(exception.Message, "Wanted:");
        StringAssert.Contains(exception.Message, "Actual invocations:");
    }

    [TestMethod]
    public void VerifyNoMoreInteractions_FailsForUnverifiedInvocationAndSucceedsAfterVerification()
    {
        var mock = Of<IVerifiedService>();

        mock.Save("a");
        mock.Save("b");

        Verify(() => mock.Save("a"));

        Assert.ThrowsException<VerificationException>(() => VerifyNoMoreInteractions(mock));

        Verify(() => mock.Save("b"));
        VerifyNoMoreInteractions(mock);
    }

    [TestMethod]
    public void Captor_CapturesVerifiedArgument()
    {
        var mock = Of<IVerifiedService>();
        var captor = Capture<string>();

        mock.Save("abc");

        Verify(() => mock.Save(captor.Value));

        Assert.AreEqual("abc", captor.CapturedValue);
    }

    [TestMethod]
    public void Captor_CapturesMultipleVerifiedArguments()
    {
        var mock = Of<IVerifiedService>();
        var captor = Capture<string>();

        mock.Save("a");
        mock.Save("b");

        Verify(() => mock.Save(captor.Value), Times.Exactly(2));

        CollectionAssert.AreEqual(new[] { "a", "b" }, captor.CapturedValues.ToArray());
    }

    [TestMethod]
    public void StrictMock_WhenUnstubbedInvocation_ThrowsMockException()
    {
        var mock = Of<IVerifiedService>(MockBehavior.Strict);

        When(() => mock.GetName(1)).ThenReturn("one");

        Assert.AreEqual("one", mock.GetName(1));

        var exception = Assert.ThrowsException<MockException>(() => mock.GetName(2));

        StringAssert.Contains(exception.Message, "Mock ID:");
        StringAssert.Contains(exception.Message, "Method: GetName");
        StringAssert.Contains(exception.Message, "Arguments: 2");
        StringAssert.Contains(exception.Message, "Existing stub candidates:");
    }

    [TestMethod]
    public void LenientMock_WhenUnstubbedInvocation_ReturnsDefaultValue()
    {
        var mock = Of<IVerifiedService>();

        Assert.IsNull(mock.GetName(1));
    }

    [TestMethod]
    public void VerifyFailureMessage_ContainsExpectedLabels()
    {
        var mock = Of<IVerifiedService>();

        mock.Save("actual");

        var exception = Assert.ThrowsException<VerificationException>(
            () => Verify(() => mock.Save("expected"), Times.Once()));

        StringAssert.Contains(exception.Message, "Wanted:");
        StringAssert.Contains(exception.Message, "Actual invocations:");
        StringAssert.Contains(exception.Message, "Matching invocations:");
        StringAssert.Contains(exception.Message, "Method:");
        StringAssert.Contains(exception.Message, "Expected count:");
        StringAssert.Contains(exception.Message, "Actual count:");
        StringAssert.Contains(exception.Message, "Arguments:");
        StringAssert.Contains(exception.Message, "Closest recorded calls:");
    }

    [TestMethod]
    public void Verify_WithMatcher_Succeeds()
    {
        var mock = Of<IVerifiedService>();

        mock.Save("abc");

        Verify(() => mock.Save(Is<string>(value => value.StartsWith("a"))));
    }

    private interface IVerifiedService
    {
        void Save(string value);

        string? GetName(int id);
    }
}
