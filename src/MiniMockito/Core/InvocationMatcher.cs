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
        return Matches(invocation.Method, invocation.Arguments);
    }

    public bool Matches(MethodInfo method, IReadOnlyList<object?> arguments)
    {
        if (!Equals(Method, method) || ArgumentMatchers.Count != arguments.Count)
        {
            return false;
        }

        for (var index = 0; index < ArgumentMatchers.Count; index++)
        {
            if (!ArgumentMatchers[index].Matches(arguments[index]))
            {
                return false;
            }
        }

        return true;
    }

    internal void CaptureArguments(IReadOnlyList<object?> arguments)
    {
        for (var index = 0; index < ArgumentMatchers.Count && index < arguments.Count; index++)
        {
            if (ArgumentMatchers[index] is ICapturingArgumentMatcher capturingMatcher)
            {
                capturingMatcher.Capture(arguments[index]);
            }
        }
    }

    internal string Describe()
    {
        return $"{Method.Name}({string.Join(", ", ArgumentMatchers.Select(matcher => matcher.Describe()))})";
    }
}
