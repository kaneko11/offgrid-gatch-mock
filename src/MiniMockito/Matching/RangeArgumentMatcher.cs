namespace MiniMockito.Matching;

internal sealed class RangeArgumentMatcher : ArgumentMatcher
{
    private readonly IComparable _minimum;
    private readonly IComparable _maximum;

    internal RangeArgumentMatcher(IComparable minimum, IComparable maximum)
    {
        _minimum = minimum;
        _maximum = maximum;
    }

    public override bool Matches(object? argument)
    {
        if (argument is not IComparable comparable)
        {
            return false;
        }

        return comparable.CompareTo(_minimum) >= 0 && comparable.CompareTo(_maximum) <= 0;
    }

    public override string Describe()
    {
        return $"InRange({_minimum}, {_maximum})";
    }
}
