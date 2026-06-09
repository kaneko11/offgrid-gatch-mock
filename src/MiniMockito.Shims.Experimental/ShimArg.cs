namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Factory for argument matchers used with <see cref="NewShimBuilder{T}.WithArguments"/>.
/// </summary>
/// <remarks>
/// All matchers in this class are experimental. API may change in future phases.
/// <para>
/// <b>Null matching:</b>
/// <see cref="Any{T}"/> matches null for reference types and <see cref="Nullable{T}"/>,
/// but not for non-nullable value types.
/// <see cref="Eq{T}"/> with a null expected value matches a null actual argument.
/// </para>
/// <para>
/// <b>Value type boxing:</b>
/// Matchers receive boxed <see langword="object?"/> values. The generated wrapper method boxes
/// value-type constructor arguments before passing them to <see cref="ShimDispatcher.NewWithArgs{T}"/>.
/// Matchers use <c>actual is T</c> / <c>EqualityComparer&lt;T&gt;.Default</c> to unbox correctly.
/// </para>
/// </remarks>
public static class ShimArg
{
    /// <summary>
    /// Creates a matcher that accepts any value assignable to <typeparamref name="T"/>.
    /// Null is accepted for reference types and <see cref="Nullable{T}"/>;
    /// null is rejected for non-nullable value types.
    /// </summary>
    public static IShimArgumentMatcher Any<T>() => new ShimAnyMatcher<T>();

    /// <summary>
    /// Creates a matcher that matches a specific value using <see cref="EqualityComparer{T}.Default"/>.
    /// Null is supported: <c>Eq&lt;string?&gt;(null)</c> matches a null argument.
    /// </summary>
    public static IShimArgumentMatcher Eq<T>(T? value) => new ShimEqMatcher<T>(value);

    /// <summary>
    /// Creates a matcher that matches when the predicate returns <see langword="true"/>.
    /// If the predicate throws, a <see cref="ShimException"/> is raised with the original
    /// exception as the inner exception.
    /// Null is passed to the predicate as <see langword="default"/>(<typeparamref name="T"/>)
    /// when the actual argument cannot be cast to <typeparamref name="T"/>.
    /// </summary>
    public static IShimArgumentMatcher Is<T>(Func<T?, bool> predicate) => new ShimPredicateMatcher<T>(predicate);
}
