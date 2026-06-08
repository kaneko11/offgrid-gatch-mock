namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Entry point intended for rewritten constructor call sites.
/// </summary>
public static class ShimDispatcher
{
    /// <summary>
    /// Creates a new instance of <typeparamref name="T"/> through the active shim rule, or by using a public parameterless constructor.
    /// </summary>
    /// <typeparam name="T">The target type to create.</typeparam>
    /// <returns>The replacement instance when a rule exists; otherwise a real instance.</returns>
    public static T New<T>()
    {
        var targetType = typeof(T);
        var context = ShimContext.Current;

        if (context is { IsDisposed: false } &&
            context.Registry.TryFindNewRule(targetType, out var rule) &&
            rule is not null)
        {
            var result = rule.CreateInstance();
            return (T)result!;
        }

        return CreateRealInstance<T>(targetType);
    }

    private static T CreateRealInstance<T>(Type targetType)
    {
        if (!targetType.IsClass)
        {
            throw CreateFallbackException(targetType, "TargetTypeIsNotAClass", "ShimDispatcher.New<T>() fallback supports reference types with public parameterless constructors.");
        }

        if (targetType.ContainsGenericParameters)
        {
            throw CreateFallbackException(targetType, "OpenGenericTypeNotSupported", "Use a closed non-generic class.");
        }

        if (targetType.IsAbstract)
        {
            throw CreateFallbackException(targetType, "AbstractTypeNotSupported", "Register a replacement instance or use a concrete class.");
        }

        try
        {
            var instance = Activator.CreateInstance(targetType);
            if (instance is null)
            {
                throw CreateFallbackException(targetType, "ConstructorReturnedNull", "Register a replacement instance with Shim.New<T>().Returns(...).");
            }

            return (T)instance;
        }
        catch (MissingMethodException exception)
        {
            throw CreateFallbackException(targetType, "PublicParameterlessConstructorNotFound", "Add a public parameterless constructor or register a replacement rule.", exception);
        }
    }

    private static ShimUnsupportedException CreateFallbackException(
        Type targetType,
        string reason,
        string hint,
        Exception? innerException = null)
    {
        var message = string.Join(
            Environment.NewLine,
            "New shim fallback cannot create a real instance.",
            $"Target type: {targetType.FullName}",
            "Constructor: .ctor()",
            "Calling assembly: <manual dispatcher>",
            "Calling method: ShimDispatcher.New<T>()",
            "Rewrite mode: None",
            $"Reason: {reason}",
            "Supported patterns:",
            "  public non-generic class",
            "  public parameterless constructor",
            "Unsupported patterns:",
            "  value types",
            "  interfaces",
            "  abstract types",
            "  open generic types",
            "  constructor arguments",
            $"Hint: {hint}");

        return innerException is null
            ? new ShimUnsupportedException(message)
            : new ShimUnsupportedException(message, innerException);
    }
}
