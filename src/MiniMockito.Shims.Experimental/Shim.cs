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

    // ─────────────────────────────────────────────────────────────────────────
    // Static method shims (Phase 14)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts configuring a shim rule for a non-void static method identified by full type name.
    /// </summary>
    /// <typeparam name="TResult">The return type of the static method.</typeparam>
    /// <param name="declaringTypeFullName">
    /// The full name of the type that declares the method (e.g. <c>"My.Namespace.Clock"</c>).
    /// </param>
    /// <param name="methodName">The method name.</param>
    /// <param name="parameterTypes">Parameter types in declaration order; omit for parameterless methods.</param>
    public static StaticShimBuilder<TResult> Static<TResult>(
        string declaringTypeFullName,
        string methodName,
        params Type[] parameterTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(declaringTypeFullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        var context = ShimContext.RequireCurrent();
        var key = new StaticMethodKey(
            declaringTypeFullName,
            methodName,
            (parameterTypes ?? []).Select(t => t.FullName ?? t.Name).ToArray());
        return new StaticShimBuilder<TResult>(key, context);
    }

    /// <summary>
    /// Starts configuring a shim rule for a non-void static method identified by <see cref="Type"/>.
    /// </summary>
    /// <typeparam name="TResult">The return type of the static method.</typeparam>
    /// <param name="declaringType">The type that declares the method.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="parameterTypes">Parameter types in declaration order.</param>
    public static StaticShimBuilder<TResult> Static<TResult>(
        Type declaringType,
        string methodName,
        params Type[] parameterTypes)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        return Static<TResult>(declaringType.FullName ?? declaringType.Name, methodName, parameterTypes);
    }

    /// <summary>
    /// Starts configuring a shim rule for a void static method identified by full type name.
    /// </summary>
    /// <param name="declaringTypeFullName">
    /// The full name of the type that declares the method.
    /// </param>
    /// <param name="methodName">The method name.</param>
    /// <param name="parameterTypes">Parameter types in declaration order.</param>
    public static StaticShimBuilder Static(
        string declaringTypeFullName,
        string methodName,
        params Type[] parameterTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(declaringTypeFullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        var context = ShimContext.RequireCurrent();
        var key = new StaticMethodKey(
            declaringTypeFullName,
            methodName,
            (parameterTypes ?? []).Select(t => t.FullName ?? t.Name).ToArray());
        return new StaticShimBuilder(key, context);
    }

    /// <summary>
    /// Starts configuring a shim rule for a void static method identified by <see cref="Type"/>.
    /// </summary>
    /// <param name="declaringType">The type that declares the method.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="parameterTypes">Parameter types in declaration order.</param>
    public static StaticShimBuilder Static(
        Type declaringType,
        string methodName,
        params Type[] parameterTypes)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        return Static(declaringType.FullName ?? declaringType.Name, methodName, parameterTypes);
    }
}
