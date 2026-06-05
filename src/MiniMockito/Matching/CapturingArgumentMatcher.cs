namespace MiniMockito.Matching;

internal sealed class CapturingArgumentMatcher<T> : ArgumentMatcher, ICapturingArgumentMatcher
{
    private readonly ArgumentCaptor<T> _captor;

    public CapturingArgumentMatcher(ArgumentCaptor<T> captor)
    {
        _captor = captor;
    }

    public override bool Matches(object? argument)
    {
        if (argument is null)
        {
            return !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null;
        }

        return typeof(T).IsInstanceOfType(argument);
    }

    public void Capture(object? argument)
    {
        _captor.Capture(argument);
    }

    public override string Describe()
    {
        return $"Capture<{typeof(T).Name}>";
    }
}
