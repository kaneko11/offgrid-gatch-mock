using System.Reflection;

namespace MiniMockito.Core;

internal sealed class MockState
{
    private readonly List<InvocationRecord> _invocations = [];
    private readonly object _syncRoot = new();

    internal MockState(Type mockedType, MockBehavior behavior)
    {
        MockedType = mockedType;
        Behavior = behavior;
    }

    public Guid MockId { get; } = Guid.NewGuid();

    public Type MockedType { get; }

    public MockBehavior Behavior { get; }

    public IReadOnlyList<InvocationRecord> Invocations
    {
        get
        {
            lock (_syncRoot)
            {
                return _invocations.ToArray();
            }
        }
    }

    internal InvocationRecord RecordInvocation(MethodInfo method, object?[]? arguments)
    {
        var copiedArguments = Array.AsReadOnly((object?[])(arguments?.Clone() ?? Array.Empty<object?>()));
        var record = new InvocationRecord(
            MockId,
            method,
            copiedArguments,
            DateTimeOffset.UtcNow,
            MockRepository.Default.NextSequenceNumber(),
            Environment.CurrentManagedThreadId);

        lock (_syncRoot)
        {
            _invocations.Add(record);
        }

        return record;
    }
}
