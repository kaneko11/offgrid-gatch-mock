namespace MiniMockito;

public sealed class ArgumentCaptor<T>
{
    private readonly List<T?> _values = [];
    private readonly object _syncRoot = new();

    public T Value => default!;

    public T? CapturedValue
    {
        get
        {
            lock (_syncRoot)
            {
                return _values.Count == 0 ? default : _values[^1];
            }
        }
    }

    public IReadOnlyList<T?> CapturedValues
    {
        get
        {
            lock (_syncRoot)
            {
                return _values.ToArray();
            }
        }
    }

    internal void Capture(object? value)
    {
        lock (_syncRoot)
        {
            _values.Add(value is null ? default : (T)value);
        }
    }
}
