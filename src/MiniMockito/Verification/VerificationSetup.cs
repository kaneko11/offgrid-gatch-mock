using MiniMockito.Core;

namespace MiniMockito.Verification;

internal sealed class VerificationSetup
{
    internal VerificationSetup(MockState state, InvocationMatcher matcher)
    {
        State = state;
        Matcher = matcher;
    }

    internal MockState State { get; }

    internal InvocationMatcher Matcher { get; }
}
