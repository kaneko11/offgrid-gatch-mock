namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Registers replacement behavior for <c>new T()</c> shim calls.
/// </summary>
/// <typeparam name="T">The target type.</typeparam>
public sealed class NewShimBuilder<T>
{
    private readonly ShimContext _context;
    private IReadOnlyList<IShimArgumentMatcher>? _matchers;

    internal NewShimBuilder(ShimContext context)
    {
        ThrowHelper.ThrowIfNull(context);
        ValidateTargetType(typeof(T));
        _context = context;
    }

    /// <summary>
    /// Constrains this rule to constructor calls whose arguments all satisfy the supplied matchers.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>
    ///     Calling <see cref="WithArguments"/> without argument matchers matches only parameterless
    ///     constructors (argument count is zero).
    ///   </item>
    ///   <item>
    ///     Calling <see cref="Returns(T)"/> (or any overload) <em>without</em> calling
    ///     <see cref="WithArguments"/> first registers a <em>catch-all</em> rule that matches
    ///     any constructor argument list.
    ///   </item>
    ///   <item>
    ///     When multiple rules match the same call site the most recently registered rule wins
    ///     (Mockito-style: "last stub wins").
    ///   </item>
    ///   <item>
    ///     Calling <see cref="WithArguments"/> more than once on the same builder overwrites
    ///     the previous matchers.
    ///   </item>
    /// </list>
    /// </remarks>
    public NewShimBuilder<T> WithArguments(params IShimArgumentMatcher[] matchers)
    {
        ThrowHelper.ThrowIfNull(matchers);
        _context.EnsureActive();
        _matchers = matchers;
        return this;
    }

    /// <summary>
    /// Registers a fixed replacement instance for the target type.
    /// </summary>
    public NewShimRule Returns(T instance)
    {
        _context.EnsureActive();
        return _context.Registry.RegisterNewRule(typeof(T), () => instance, _context.ContextId, _matchers);
    }

    /// <summary>
    /// Registers a parameterless replacement factory for the target type.
    /// </summary>
    public NewShimRule Returns(Func<T> factory)
    {
        ThrowHelper.ThrowIfNull(factory);
        _context.EnsureActive();
        return _context.Registry.RegisterNewRule(typeof(T), () => factory(), _context.ContextId, _matchers);
    }

    /// <summary>
    /// Registers an args-based replacement factory for the target type.
    /// The factory receives the boxed constructor arguments in declaration order.
    /// </summary>
    public NewShimRule Returns(Func<object?[], T> argsFactory)
    {
        ThrowHelper.ThrowIfNull(argsFactory);
        _context.EnsureActive();
        return _context.Registry.RegisterNewRule(typeof(T), args => argsFactory(args), _context.ContextId, _matchers);
    }

    /// <summary>
    /// Registers a <see cref="ShimConstructorContext"/>-based replacement factory for the target type.
    /// </summary>
    public NewShimRule Returns(Func<ShimConstructorContext, T> contextFactory)
    {
        ThrowHelper.ThrowIfNull(contextFactory);
        _context.EnsureActive();
        return _context.Registry.RegisterNewRuleWithContext(typeof(T), ctx => contextFactory(ctx), _context.ContextId, _matchers);
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
