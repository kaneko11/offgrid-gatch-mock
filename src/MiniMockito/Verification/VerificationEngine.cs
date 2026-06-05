using MiniMockito.Core;
using MiniMockito.Exceptions;

namespace MiniMockito.Verification;

internal static class VerificationEngine
{
    internal static void Verify(VerificationSetup setup, Times times)
    {
        var matchingInvocations = setup.State.FindInvocations(setup.Matcher);
        if (!times.IsSatisfiedBy(matchingInvocations.Count))
        {
            throw new VerificationException(
                VerificationFailureMessage.Format(setup.State, setup.Matcher, times, matchingInvocations));
        }

        setup.State.MarkVerified(matchingInvocations);
        foreach (var invocation in matchingInvocations)
        {
            setup.Matcher.CaptureArguments(invocation.Arguments);
        }
    }

    internal static void VerifyNoInteractions(MockState state)
    {
        var invocations = state.Invocations;
        if (invocations.Count != 0)
        {
            throw new VerificationException(
                $"Wanted: no interactions{Environment.NewLine}" +
                $"Actual invocations:{Environment.NewLine}{InvocationFormatter.FormatMany(invocations)}");
        }
    }

    internal static void VerifyNoMoreInteractions(MockState state)
    {
        var unverifiedInvocations = state.GetUnverifiedInvocations();
        if (unverifiedInvocations.Count != 0)
        {
            throw new VerificationException(
                $"Wanted: no more interactions{Environment.NewLine}" +
                $"Actual invocations:{Environment.NewLine}{InvocationFormatter.FormatMany(unverifiedInvocations)}");
        }
    }
}
