namespace MiniMockito.Stubbing;

internal sealed class ReturnBehavior : StubBehavior
{
    private readonly object?[] _values;
    private readonly object _syncRoot = new();
    private int _nextIndex;

    internal ReturnBehavior(IEnumerable<object?> values)
    {
        _values = values.ToArray();
        if (_values.Length == 0)
        {
            throw new ArgumentException("At least one return value is required.", nameof(values));
        }
    }

    internal ReturnBehavior(object? value)
        : this([value])
    {
    }

    internal override object? Invoke(StubContext context, Type returnType)
    {
        object? value;
        lock (_syncRoot)
        {
            value = _nextIndex < _values.Length
                ? _values[_nextIndex++]
                : _values[^1];
        }

        return ReturnValueAdapter.ToReturnValue(value, returnType);
    }
}
