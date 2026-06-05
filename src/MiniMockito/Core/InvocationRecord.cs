using System.Reflection;

namespace MiniMockito.Core;

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

    public Guid MockId { get; }

    public MethodInfo Method { get; }

    public IReadOnlyList<object?> Arguments { get; }

    public DateTimeOffset Timestamp { get; }

    public long SequenceNumber { get; }

    public object? ReturnValue { get; internal set; }

    public Exception? Exception { get; internal set; }

    public int ThreadId { get; }

    public bool IsVerified { get; internal set; }
}
