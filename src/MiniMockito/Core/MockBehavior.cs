namespace MiniMockito.Core;

/// <summary>
/// Compatibility alias for the mock behavior enum.
/// </summary>
/// <remarks>Use <see cref="MiniMockito.MockBehavior"/> for new code.</remarks>
public enum MockBehavior
{
    /// <summary>
    /// Unstubbed invocations return default values.
    /// </summary>
    Lenient,

    /// <summary>
    /// Unstubbed invocations throw a <see cref="MiniMockito.Exceptions.MockException"/>.
    /// </summary>
    Strict
}
