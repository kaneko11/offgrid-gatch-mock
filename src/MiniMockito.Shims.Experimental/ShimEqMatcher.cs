namespace MiniMockito.Shims.Experimental;

internal sealed class ShimEqMatcher<T> : IShimArgumentMatcher
{
    private readonly T? _expected;

    internal ShimEqMatcher(T? expected)
    {
        _expected = expected;
    }

    public Type? ExpectedType => typeof(T);

    public bool Matches(object? actual)
    {
        if (actual is null)
            return EqualityComparer<T>.Default.Equals(_expected, default);
        if (actual is T t)
            return EqualityComparer<T>.Default.Equals(_expected, t);
        return false;
    }

    public string Describe()
    {
        var valueStr = _expected is null ? "null" : $"\"{_expected}\"";
        return $"Eq<{typeof(T).Name}>({valueStr})";
    }
}
