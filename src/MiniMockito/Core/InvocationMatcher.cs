using System.Reflection;
using MiniMockito.Matching;

namespace MiniMockito.Core;

/// <summary>
/// Matches recorded invocations by method and argument matchers.
/// </summary>
public sealed class InvocationMatcher
{
    /// <summary>
    /// Creates a new invocation matcher.
    /// </summary>
    /// <param name="method">The expected method.</param>
    /// <param name="argumentMatchers">The argument matchers in parameter order.</param>
    public InvocationMatcher(MethodInfo method, IReadOnlyList<ArgumentMatcher> argumentMatchers)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(argumentMatchers);
        Method = method;
        ArgumentMatchers = argumentMatchers;
    }

    /// <summary>
    /// Gets the expected method.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// Gets the argument matchers in parameter order.
    /// </summary>
    public IReadOnlyList<ArgumentMatcher> ArgumentMatchers { get; }

    /// <summary>
    /// Returns whether the recorded invocation matches this matcher.
    /// </summary>
    /// <param name="invocation">The invocation to check.</param>
    /// <returns><see langword="true"/> when the invocation matches.</returns>
    public bool Matches(InvocationRecord invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return Matches(invocation.Method, invocation.Arguments);
    }

    /// <summary>
    /// Returns whether the method and arguments match this matcher.
    /// </summary>
    /// <param name="method">The invoked method.</param>
    /// <param name="arguments">The invocation arguments.</param>
    /// <returns><see langword="true"/> when the method and arguments match.</returns>
    public bool Matches(MethodInfo method, IReadOnlyList<object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(arguments);
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
