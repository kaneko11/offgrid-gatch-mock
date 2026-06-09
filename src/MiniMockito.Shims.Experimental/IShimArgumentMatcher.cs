namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Matches a single constructor argument in a shim rule.
/// </summary>
public interface IShimArgumentMatcher
{
    /// <summary>
    /// Gets the expected type this matcher handles, or <see langword="null"/> if unconstrained.
    /// </summary>
    Type? ExpectedType { get; }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> satisfies this matcher.
    /// </summary>
    bool Matches(object? value);

    /// <summary>
    /// Returns a human-readable description of the expected value for diagnostics.
    /// </summary>
    string Describe();
}
