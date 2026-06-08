namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Entry point for experimental shim rule setup.
/// </summary>
public static class Shim
{
    /// <summary>
    /// Starts configuring a rule for <c>new T()</c> interception.
    /// </summary>
    /// <typeparam name="T">The target type to construct through the shim dispatcher.</typeparam>
    /// <returns>A builder for registering a replacement instance or factory.</returns>
    public static NewShimBuilder<T> New<T>()
    {
        var context = ShimContext.RequireCurrent();
        return new NewShimBuilder<T>(context);
    }
}
