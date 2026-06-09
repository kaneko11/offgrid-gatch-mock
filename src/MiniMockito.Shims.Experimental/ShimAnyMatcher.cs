namespace MiniMockito.Shims.Experimental;

internal sealed class ShimAnyMatcher<T> : IShimArgumentMatcher
{
    public Type? ExpectedType => typeof(T);

    public bool Matches(object? actual)
    {
        if (actual is null)
        {
            // null matches reference types and Nullable<T>; not non-nullable value types.
            return !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null;
        }

        return actual is T;
    }

    public string Describe() => $"Any<{typeof(T).Name}>()";
}
