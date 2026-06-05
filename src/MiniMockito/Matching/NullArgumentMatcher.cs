namespace MiniMockito.Matching;

internal sealed class NullArgumentMatcher : ArgumentMatcher
{
    public override bool Matches(object? argument)
    {
        return argument is null;
    }

    public override string Describe()
    {
        return "Null";
    }
}
