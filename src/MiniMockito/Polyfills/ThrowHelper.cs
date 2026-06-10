using System.Runtime.CompilerServices;

namespace MiniMockito;

internal static class ThrowHelper
{
    [System.Diagnostics.DebuggerStepThrough]
    internal static void ThrowIfNull(
        object? argument,
        [CallerArgumentExpression("argument")] string? paramName = null)
    {
        if (argument is null)
            throw new ArgumentNullException(paramName);
    }

    [System.Diagnostics.DebuggerStepThrough]
    internal static void ThrowIfNullOrWhiteSpace(
        string? argument,
        [CallerArgumentExpression("argument")] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(argument))
            throw new ArgumentException("The value cannot be null or whitespace.", paramName);
    }
}
