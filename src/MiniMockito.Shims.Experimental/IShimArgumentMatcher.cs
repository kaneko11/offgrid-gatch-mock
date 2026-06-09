namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Matches a single constructor argument in a shim rule.
/// </summary>
/// <remarks>
/// <b>Experimental.</b> This interface is part of the <c>MiniMockito.Shims.Experimental</c> package.
/// API may change in future phases.
/// <para>
/// Implement this interface to create custom argument matchers.  Built-in matchers are accessible
/// via the <see cref="ShimArg"/> factory class, or directly via
/// <c>using static MiniMockito.Shims.Experimental.ShimArg;</c>.
/// </para>
/// <para>
/// The <see cref="Matches"/> method may have side effects for capturing matchers such as
/// <see cref="ShimCaptor{T}"/>.  Each matcher is called exactly once per dispatch attempt.
/// </para>
/// </remarks>
public interface IShimArgumentMatcher
{
    /// <summary>
    /// Gets the expected type this matcher handles, or <see langword="null"/> if unconstrained.
    /// </summary>
    Type? ExpectedType { get; }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> satisfies this matcher.
    /// May have side effects for capturing matchers (e.g. <see cref="ShimCaptor{T}"/>).
    /// </summary>
    bool Matches(object? value);

    /// <summary>
    /// Returns a human-readable description of the expected value for diagnostics.
    /// Used by <see cref="ShimDispatchDiagnostics"/> when formatting mismatch reports.
    /// </summary>
    string Describe();
}
