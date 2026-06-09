namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Provides constructor call information to args-aware shim factories.
/// </summary>
public sealed class ShimConstructorContext
{
    internal ShimConstructorContext(Type targetType, object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(arguments);
        TargetType = targetType;
        Arguments = arguments;
    }

    /// <summary>
    /// Gets the type being constructed.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Gets the constructor arguments, in declaration order.
    /// Value type arguments are boxed.
    /// </summary>
    public IReadOnlyList<object?> Arguments { get; }

    /// <summary>
    /// Returns the argument at <paramref name="index"/> cast to <typeparamref name="T"/>.
    /// </summary>
    public T? GetArgument<T>(int index) => (T?)Arguments[index];
}
