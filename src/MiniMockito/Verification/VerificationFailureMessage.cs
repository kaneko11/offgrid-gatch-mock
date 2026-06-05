using MiniMockito.Core;

namespace MiniMockito.Verification;

internal static class VerificationFailureMessage
{
    internal static string Format(
        MockState state,
        InvocationMatcher matcher,
        Times times,
        IReadOnlyList<InvocationRecord> matchingInvocations)
    {
        var actualInvocations = state.Invocations;
        var closestCalls = actualInvocations
            .Where(invocation => Equals(invocation.Method, matcher.Method))
            .ToArray();

        return string.Join(
            Environment.NewLine,
            "Wanted:",
            $"  {matcher.Describe()} {times.Description}",
            "Method:",
            $"  {matcher.Method.Name}",
            "Arguments:",
            $"  {string.Join(", ", matcher.ArgumentMatchers.Select(argument => argument.Describe()))}",
            "Expected count:",
            $"  {times.Description}",
            "Actual count:",
            $"  {matchingInvocations.Count}",
            "Matching invocations:",
            InvocationFormatter.FormatMany(matchingInvocations),
            "Actual invocations:",
            InvocationFormatter.FormatMany(actualInvocations),
            "Closest recorded calls:",
            InvocationFormatter.FormatMany(closestCalls));
    }
}
