namespace MiniMockito.Shims.Experimental;

internal sealed class ShimPredicateMatcher<T> : IShimArgumentMatcher
{
    private readonly Func<T?, bool> _predicate;

    internal ShimPredicateMatcher(Func<T?, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _predicate = predicate;
    }

    public Type? ExpectedType => typeof(T);

    public bool Matches(object? actual)
    {
        T? value = actual is T t ? t : default;
        try
        {
            return _predicate(value);
        }
        catch (Exception ex)
        {
            throw new ShimException(
                string.Join(Environment.NewLine,
                    $"ShimArg.Is<{typeof(T).Name}>() predicate threw an exception.",
                    $"Actual value: {actual ?? "null"}",
                    $"Actual type: {actual?.GetType().FullName ?? "null"}",
                    $"Predicate exception: {ex.Message}"),
                ex);
        }
    }

    public string Describe() => $"Is<{typeof(T).Name}>(predicate)";
}
