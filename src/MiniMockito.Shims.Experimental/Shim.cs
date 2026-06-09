namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Entry point for experimental shim rule setup.
/// </summary>
/// <remarks>
/// <b>Experimental.</b> This class is part of the <c>MiniMockito.Shims.Experimental</c> package.
/// API may change in future phases.
/// <para>
/// All shim rules must be registered inside an active <see cref="ShimContext"/>:
/// <code>
/// using (ShimContext.Create())
/// {
///     Shim.New&lt;UserRepository&gt;()
///         .WithArguments(ShimArg.Any&lt;string&gt;())
///         .Returns(fakeRepository);
/// }
/// </code>
/// </para>
/// <para>
/// Static method mocking and BCL type replacement are not supported.
/// Parallel test execution is not safe; annotate test assemblies with
/// <c>[assembly: DoNotParallelize]</c>.
/// </para>
/// </remarks>
public static class Shim
{
    /// <summary>
    /// Starts configuring a shim rule for <c>new T()</c> constructor interception.
    /// </summary>
    /// <typeparam name="T">
    /// The concrete, non-generic, non-abstract class whose constructor calls will be intercepted.
    /// </typeparam>
    /// <returns>A fluent builder for constraining and registering the replacement.</returns>
    /// <exception cref="ShimException">Thrown when there is no active <see cref="ShimContext"/>.</exception>
    /// <exception cref="ShimUnsupportedException">Thrown when <typeparamref name="T"/> is not a supported target (value type, interface, abstract, or open generic).</exception>
    /// <remarks>
    /// Call <see cref="NewShimBuilder{T}.WithArguments"/> before <c>Returns</c> to restrict matching
    /// to specific constructor argument values.  Omitting <c>WithArguments</c> registers a catch-all
    /// rule that matches any constructor argument list.
    /// </remarks>
    public static NewShimBuilder<T> New<T>()
    {
        var context = ShimContext.RequireCurrent();
        return new NewShimBuilder<T>(context);
    }
}
