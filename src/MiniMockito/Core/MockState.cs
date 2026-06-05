using System.Reflection;
using MiniMockito.Stubbing;

namespace MiniMockito.Core;

internal sealed class MockState
{
    private readonly List<InvocationRecord> _invocations = [];
    private readonly List<StubRule> _stubRules = [];
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

    internal void AddStubRule(StubRule rule)
    {
        lock (_syncRoot)
        {
            _stubRules.Add(rule);
        }
    }

    internal StubRule? FindStubRule(MethodInfo method, object?[]? arguments)
    {
        var copiedArguments = Array.AsReadOnly((object?[])(arguments?.Clone() ?? Array.Empty<object?>()));

        lock (_syncRoot)
        {
            for (var index = _stubRules.Count - 1; index >= 0; index--)
            {
                var rule = _stubRules[index];
                if (rule.Matches(method, copiedArguments))
                {
                    return rule;
                }
            }
        }

        return null;
    }
}
