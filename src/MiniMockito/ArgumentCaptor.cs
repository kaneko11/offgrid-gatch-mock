namespace MiniMockito;

/// <summary>
/// Captures arguments that matched a successful verification.
/// </summary>
/// <typeparam name="T">The captured argument type.</typeparam>
public sealed class ArgumentCaptor<T>
{
    private readonly List<T?> _values = [];
    private readonly object _syncRoot = new();

    /// <summary>
    /// Gets a placeholder value for use in a verification expression.
    /// </summary>
    public T Value => default!;

    /// <summary>
    /// Gets the most recently captured value, or the default value when nothing has been captured.
    /// </summary>
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

    /// <summary>
    /// Gets all captured values in capture order.
    /// </summary>
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
