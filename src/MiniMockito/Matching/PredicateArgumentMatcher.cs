namespace MiniMockito.Matching;

internal sealed class PredicateArgumentMatcher : ArgumentMatcher
{
    private readonly Type _argumentType;
    private readonly Delegate _predicate;

    internal PredicateArgumentMatcher(Type argumentType, Delegate predicate)
    {
        _argumentType = argumentType;
        _predicate = predicate;
    }

    public override bool Matches(object? argument)
    {
        if (argument is null)
        {
            return !_argumentType.IsValueType && (bool)_predicate.DynamicInvoke(argument)!;
        }

        if (!_argumentType.IsInstanceOfType(argument))
        {
            return false;
        }

        return (bool)_predicate.DynamicInvoke(argument)!;
    }

    public override string Describe()
    {
        return "Is(predicate)";
    }
}
