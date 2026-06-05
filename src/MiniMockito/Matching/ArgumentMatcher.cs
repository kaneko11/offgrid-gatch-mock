namespace MiniMockito.Matching;

/// <summary>
/// Base type for argument matchers.
/// </summary>
public abstract class ArgumentMatcher
{
    /// <summary>
    /// Returns whether the supplied argument matches.
    /// </summary>
    /// <param name="argument">The actual argument value.</param>
    /// <returns><see langword="true"/> when the argument matches.</returns>
    public abstract bool Matches(object? argument);

    /// <summary>
    /// Describes this matcher for diagnostics.
    /// </summary>
    /// <returns>A human-readable matcher description.</returns>
    public virtual string Describe()
    {
        return GetType().Name;
    }
}
