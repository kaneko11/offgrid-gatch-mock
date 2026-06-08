using System.Runtime.CompilerServices;
using MiniMockito.Exceptions;

namespace MiniMockito.Core;

internal sealed class MockRepository
{
    private readonly ConditionalWeakTable<object, MockState> _states = new();
    private long _sequenceNumber;

    public static MockRepository Default { get; } = new();

    internal MockState CreateState(Type mockedType, global::MiniMockito.MockBehavior behavior, object? realInstance = null, bool callsBase = false)
    {
        return new MockState(mockedType, behavior, realInstance, callsBase);
    }

    internal void Register(object proxy, MockState state)
    {
        _states.Add(proxy, state);
    }

    internal MockState GetState(object mock)
    {
        ArgumentNullException.ThrowIfNull(mock);

        if (_states.TryGetValue(mock, out var state))
        {
            return state;
        }

        throw new UnsupportedMockTargetException("The supplied object is not a MiniMockito mock.");
    }

    internal long NextSequenceNumber()
    {
        return Interlocked.Increment(ref _sequenceNumber);
    }
}
