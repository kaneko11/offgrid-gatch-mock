namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Factory for argument matchers used with <see cref="NewShimBuilder{T}.WithArguments"/>,
/// <see cref="StaticShimBuilder{TResult}.WithArguments"/>, and <see cref="StaticShimBuilder.WithArguments"/>.
/// </summary>
/// <remarks>
/// <b>Experimental.</b> All matchers in this class are part of the experimental shim API.
/// API may change in future phases.
/// <para>
/// <b>Static import:</b> Add <c>using static MiniMockito.Shims.Experimental.ShimArg;</c> to use
/// the factory methods without the class name prefix:
/// <code>
/// using static MiniMockito.Shims.Experimental.ShimArg;
///
/// // newobj interception
/// Shim.New&lt;UserRepository&gt;()
///     .WithArguments(Any&lt;string&gt;())
///     .Returns(fakeRepository);
///
/// // static method interception (Phase 14+)
/// Shim.Static&lt;string&gt;(typeof(Clock), "GetName", typeof(int))
///     .WithArguments(Eq(42))
///     .Returns("shimmed");
///
/// var captured = Captor&lt;string&gt;();
/// Shim.New&lt;UserRepository&gt;()
///     .WithArguments(captured)
///     .Returns(fakeRepository);
/// </code>
/// </para>
/// <para>
/// <b>Null matching:</b>
/// <see cref="Any{T}"/> matches null for reference types and <see cref="Nullable{T}"/>,
/// but not for non-nullable value types.
/// <see cref="Eq{T}"/> with a null expected value matches a null actual argument.
/// </para>
/// <para>
/// <b>Value type boxing:</b>
/// Matchers receive boxed <see langword="object?"/> values. The generated wrapper method boxes
/// value-type arguments before passing them to the dispatcher.
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

    /// <summary>
    /// Creates a new <see cref="ShimCaptor{T}"/> that captures constructor arguments of type
    /// <typeparamref name="T"/>.  Convenience alias for <see cref="ShimCaptor.For{T}()"/>.
    /// </summary>
    public static ShimCaptor<T> Captor<T>() => ShimCaptor.For<T>();
}
