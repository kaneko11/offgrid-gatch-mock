namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Registers replacement behavior for <c>new T()</c> shim calls.
/// </summary>
/// <typeparam name="T">The target type.</typeparam>
public sealed class NewShimBuilder<T>
{
    private readonly ShimContext _context;

    internal NewShimBuilder(ShimContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateTargetType(typeof(T));
        _context = context;
    }

    /// <summary>
    /// Registers a fixed replacement instance for the target type.
    /// </summary>
    /// <param name="instance">The instance returned by <see cref="ShimDispatcher.New{T}"/> while this context is active.</param>
    /// <returns>The registered rule.</returns>
    public NewShimRule Returns(T instance)
    {
        _context.EnsureActive();
        return _context.Registry.RegisterNewRule(typeof(T), () => instance, _context.ContextId);
    }

    /// <summary>
    /// Registers a replacement factory for the target type.
    /// </summary>
    /// <param name="factory">The factory called by <see cref="ShimDispatcher.New{T}"/> while this context is active.</param>
    /// <returns>The registered rule.</returns>
    public NewShimRule Returns(Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _context.EnsureActive();
        return _context.Registry.RegisterNewRule(typeof(T), () => factory(), _context.ContextId);
    }

    private static void ValidateTargetType(Type targetType)
    {
        if (!targetType.IsClass)
        {
            throw CreateUnsupportedTargetException(targetType, "TargetTypeIsNotAClass", "Use a public non-generic class for new interception.");
        }

        if (targetType.ContainsGenericParameters)
        {
            throw CreateUnsupportedTargetException(targetType, "OpenGenericTypeNotSupported", "Use a closed non-generic class for the Phase 2 skeleton.");
        }

        if (targetType.IsAbstract)
        {
            throw CreateUnsupportedTargetException(targetType, "AbstractTypeNotSupported", "Use a concrete class for new interception.");
        }
    }

    private static ShimUnsupportedException CreateUnsupportedTargetException(Type targetType, string reason, string hint)
    {
        return new ShimUnsupportedException(string.Join(
            Environment.NewLine,
            "New shim target is not supported.",
            $"Target type: {targetType.FullName}",
            "Constructor: <not inspected>",
            "Calling assembly: <manual dispatcher>",
            "Calling method: <manual dispatcher>",
            "Rewrite mode: None",
            $"Reason: {reason}",
            "Supported patterns:",
            "  public non-generic class",
            "  parameterless constructor for dispatcher fallback",
            "Unsupported patterns:",
            "  value types",
            "  interfaces",
            "  open generic types",
            "  abstract types",
            $"Hint: {hint}"));
    }
}
