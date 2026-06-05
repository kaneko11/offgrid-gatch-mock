using System.Reflection;
using MiniMockito.Core;

namespace MiniMockito.Stubbing;

internal sealed class StubRule
{
    internal StubRule(InvocationMatcher matcher, StubBehavior behavior)
    {
        Matcher = matcher;
        Behavior = behavior;
    }

    internal InvocationMatcher Matcher { get; }

    internal StubBehavior Behavior { get; }

    internal bool Matches(MethodInfo method, IReadOnlyList<object?> arguments)
    {
        return Matcher.Matches(method, arguments);
    }

    internal object? Invoke(InvocationRecord invocation, Type returnType)
    {
        var context = new StubContext(invocation.MockId, invocation.Method, invocation.Arguments);
        return Behavior.Invoke(context, returnType);
    }

    internal string Describe()
    {
        return Matcher.Describe();
    }
}
