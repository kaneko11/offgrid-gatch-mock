namespace MiniMockito;

/// <summary>
/// Defines how a mock behaves when no stub matches an invocation.
/// </summary>
public enum MockBehavior
{
    /// <summary>
    /// Unstubbed invocations return default values.
    /// </summary>
    Lenient,

    /// <summary>
    /// Unstubbed invocations throw a <see cref="Exceptions.MockException"/>.
    /// </summary>
    Strict
}
