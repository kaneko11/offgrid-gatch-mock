// Polyfill for ArgumentNullException.ThrowIfNull (NET6+),
// ArgumentException.ThrowIfNullOrWhiteSpace (NET7+),
// and ObjectDisposedException.ThrowIf (NET7+).
// Used by all TargetFrameworks so that source is compatible with net48.

using System.Runtime.CompilerServices;

namespace MiniMockito.Shims.Experimental;

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

    [System.Diagnostics.DebuggerStepThrough]
    internal static void ThrowIfDisposed(bool condition, object instance)
    {
        if (condition)
            throw new ObjectDisposedException(instance?.GetType().Name);
    }
}
