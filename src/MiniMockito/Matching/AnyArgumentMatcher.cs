namespace MiniMockito.Matching;

internal sealed class AnyArgumentMatcher : ArgumentMatcher
{
    public override bool Matches(object? argument)
    {
        return true;
    }

    public override string Describe()
    {
        return "Any";
    }
}
