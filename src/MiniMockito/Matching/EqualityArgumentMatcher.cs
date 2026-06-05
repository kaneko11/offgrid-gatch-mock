namespace MiniMockito.Matching;

internal sealed class EqualityArgumentMatcher : ArgumentMatcher
{
    private readonly object? _expected;

    internal EqualityArgumentMatcher(object? expected)
    {
        _expected = expected;
    }

    public override bool Matches(object? argument)
    {
        return Equals(_expected, argument);
    }

    public override string Describe()
    {
        return _expected is null ? "Eq(null)" : $"Eq({_expected})";
    }
}
