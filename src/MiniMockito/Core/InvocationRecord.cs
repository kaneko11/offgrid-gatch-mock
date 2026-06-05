using System.Reflection;

namespace MiniMockito.Core;

/// <summary>
/// Represents one invocation recorded by a mock or spy.
/// </summary>
public sealed class InvocationRecord
{
    internal InvocationRecord(
        Guid mockId,
        MethodInfo method,
        IReadOnlyList<object?> arguments,
        DateTimeOffset timestamp,
        long sequenceNumber,
        int threadId)
    {
        MockId = mockId;
        Method = method;
        Arguments = arguments;
        Timestamp = timestamp;
        SequenceNumber = sequenceNumber;
        ThreadId = threadId;
    }

    /// <summary>
    /// Gets the unique ID of the mock that recorded this invocation.
    /// </summary>
    public Guid MockId { get; }

    /// <summary>
    /// Gets the invoked method.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// Gets a snapshot of invocation arguments.
    /// </summary>
    public IReadOnlyList<object?> Arguments { get; }

    /// <summary>
    /// Gets the UTC timestamp when the invocation was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the global invocation sequence number.
    /// </summary>
    public long SequenceNumber { get; }

    /// <summary>
    /// Gets the returned value when the invocation completed successfully.
    /// </summary>
    public object? ReturnValue { get; internal set; }

    /// <summary>
    /// Gets the thrown exception when the invocation failed.
    /// </summary>
    public Exception? Exception { get; internal set; }

    /// <summary>
    /// Gets the managed thread ID that recorded the invocation.
    /// </summary>
    public int ThreadId { get; }

    /// <summary>
    /// Gets whether this invocation has been marked as verified.
    /// </summary>
    public bool IsVerified { get; internal set; }
}
