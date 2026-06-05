using System.Reflection;

namespace MiniMockito.Stubbing;

/// <summary>
/// Provides invocation data to a <c>ThenAnswer</c> callback.
/// </summary>
public sealed class StubContext
{
    internal StubContext(Guid mockId, MethodInfo method, IReadOnlyList<object?> arguments)
    {
        MockId = mockId;
        Method = method;
        Arguments = arguments;
    }

    /// <summary>
    /// Gets the unique ID of the mock that received the invocation.
    /// </summary>
    public Guid MockId { get; }

    /// <summary>
    /// Gets the invoked method.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// Gets the invocation arguments.
    /// </summary>
    public IReadOnlyList<object?> Arguments { get; }
}
