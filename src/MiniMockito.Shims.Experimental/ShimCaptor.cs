namespace MiniMockito.Shims.Experimental;

/// <summary>
/// A capture matcher that records every constructor argument that passes its type check.
/// Implements <see cref="IShimArgumentMatcher"/> so it can be passed directly to
/// <see cref="NewShimBuilder{T}.WithArguments"/>.
/// </summary>
/// <remarks>
/// <b>Experimental.</b> This API may change in future phases.
///
/// <b>Capture rules:</b>
/// <list type="bullet">
///   <item>Only arguments that type-match <typeparamref name="T"/> are captured.</item>
///   <item>When <typeparamref name="T"/> is a reference type or <see cref="Nullable{T}"/>,
///         <see langword="null"/> is captured as <see langword="default"/>(<typeparamref name="T"/>).</item>
///   <item>When <typeparamref name="T"/> is a non-nullable value type, <see langword="null"/>
///         does not match and is not captured.</item>
///   <item>When a matcher later in the same <c>WithArguments</c> list fails, arguments that were
///         already captured by this instance remain captured (<em>partial capture</em>).
///         This behaviour is intentional for simplicity; see docs for details.</item>
/// </list>
/// </remarks>
/// <typeparam name="T">The type of constructor argument to capture.</typeparam>
public sealed class ShimCaptor<T> : IShimArgumentMatcher
{
    private readonly List<T?> _captured = [];

    /// <inheritdoc/>
    public Type? ExpectedType => typeof(T);

    /// <summary>
    /// Gets the last captured value.
    /// </summary>
    /// <exception cref="ShimException">Thrown when no value has been captured yet.</exception>
    public T? Value
    {
        get
        {
            if (_captured.Count == 0)
            {
                throw new ShimException(string.Join(
                    Environment.NewLine,
                    $"No value has been captured for ShimCaptor<{typeof(T).Name}>.",
                    $"Captured count: 0.",
                    "Hint: Ensure the captor is used in WithArguments(...) and the shim rule actually matches."));
            }

            return _captured[_captured.Count - 1];
        }
    }

    /// <summary>
    /// Gets all captured values in capture order.
    /// </summary>
    public IReadOnlyList<T?> Values => _captured.AsReadOnly();

    /// <summary>
    /// Gets a value indicating whether at least one value has been captured.
    /// </summary>
    public bool HasValue => _captured.Count > 0;

    /// <summary>
    /// Removes all captured values.
    /// </summary>
    public void Clear() => _captured.Clear();

    /// <inheritdoc/>
    public bool Matches(object? actual)
    {
        if (actual is null)
        {
            bool nullOk = !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null;
            if (nullOk)
            {
                _captured.Add(default);
                return true;
            }

            return false;
        }

        if (actual is T value)
        {
            _captured.Add(value);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public string Describe() => $"Capture<{typeof(T).Name}>()";
}

/// <summary>
/// Factory for <see cref="ShimCaptor{T}"/>.
/// </summary>
public static class ShimCaptor
{
    /// <summary>
    /// Creates a new <see cref="ShimCaptor{T}"/> that captures constructor arguments of type
    /// <typeparamref name="T"/>.
    /// </summary>
    public static ShimCaptor<T> For<T>() => new();
}
