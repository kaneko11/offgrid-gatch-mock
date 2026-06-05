namespace MiniMockito.Matching;

public abstract class ArgumentMatcher
{
    public abstract bool Matches(object? argument);

    public virtual string Describe()
    {
        return GetType().Name;
    }
}
