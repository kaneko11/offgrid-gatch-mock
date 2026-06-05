using MiniMockito.Core;

namespace MiniMockito.Stubbing;

internal sealed class StubSetup
{
    internal StubSetup(MockState state, InvocationMatcher matcher, Type returnType)
    {
        State = state;
        Matcher = matcher;
        ReturnType = returnType;
    }

    internal MockState State { get; }

    internal InvocationMatcher Matcher { get; }

    internal Type ReturnType { get; }
}
