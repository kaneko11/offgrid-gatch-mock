namespace MiniMockito.Matching;

internal sealed class NotNullArgumentMatcher : ArgumentMatcher
{
    public override bool Matches(object? argument)
    {
        return argument is not null;
    }

    public override string Describe()
    {
        return "NotNull";
    }
}
