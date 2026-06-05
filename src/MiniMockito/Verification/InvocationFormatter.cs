using MiniMockito.Core;

namespace MiniMockito.Verification;

internal static class InvocationFormatter
{
    internal static string FormatMany(IReadOnlyList<InvocationRecord> invocations)
    {
        if (invocations.Count == 0)
        {
            return "  <none>";
        }

        return string.Join(Environment.NewLine, invocations.Select(invocation => $"  {Format(invocation)}"));
    }

    internal static string Format(InvocationRecord invocation)
    {
        var arguments = string.Join(", ", invocation.Arguments.Select(FormatValue));
        var verified = invocation.IsVerified ? " verified" : string.Empty;
        return $"#{invocation.SequenceNumber} {invocation.Method.Name}({arguments}){verified}";
    }

    internal static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            string text => $"\"{text}\"",
            _ => value.ToString() ?? string.Empty
        };
    }
}
