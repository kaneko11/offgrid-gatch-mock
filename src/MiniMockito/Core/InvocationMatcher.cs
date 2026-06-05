using System.Reflection;
using MiniMockito.Matching;

namespace MiniMockito.Core;

public sealed class InvocationMatcher
{
    public InvocationMatcher(MethodInfo method, IReadOnlyList<ArgumentMatcher> argumentMatchers)
    {
        Method = method;
        ArgumentMatchers = argumentMatchers;
    }

    public MethodInfo Method { get; }

    public IReadOnlyList<ArgumentMatcher> ArgumentMatchers { get; }

    public bool Matches(InvocationRecord invocation)
    {
        if (!Equals(Method, invocation.Method) || ArgumentMatchers.Count != invocation.Arguments.Count)
        {
            return false;
        }

        for (var index = 0; index < ArgumentMatchers.Count; index++)
        {
            if (!ArgumentMatchers[index].Matches(invocation.Arguments[index]))
            {
                return false;
            }
        }

        return true;
    }
}
