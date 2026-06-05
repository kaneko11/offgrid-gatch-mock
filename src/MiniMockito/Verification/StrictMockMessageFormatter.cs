using System.Reflection;
using MiniMockito.Core;

namespace MiniMockito.Verification;

internal static class StrictMockMessageFormatter
{
    internal static string Format(MockState state, MethodInfo method, object?[]? arguments)
    {
        var argumentText = string.Join(", ", (arguments ?? []).Select(InvocationFormatter.FormatValue));
        var candidates = state.DescribeStubCandidates(method);
        var candidateText = candidates.Count == 0
            ? "  <none>"
            : string.Join(Environment.NewLine, candidates.Select(candidate => $"  {candidate}"));

        return string.Join(
            Environment.NewLine,
            "Strict mock received an unstubbed invocation.",
            $"Mock ID: {state.MockId}",
            $"Method: {method.Name}",
            $"Arguments: {argumentText}",
            "Existing stub candidates:",
            candidateText);
    }
}
