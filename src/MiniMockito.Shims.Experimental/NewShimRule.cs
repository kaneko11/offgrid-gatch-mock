namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes a registered replacement for a target constructor call.
/// </summary>
public sealed class NewShimRule
{
    internal NewShimRule(Type targetType, Func<object?> factory, Guid contextId, long registrationOrder)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(factory);
        TargetType = targetType;
        Factory = factory;
        ContextId = contextId;
        RegistrationOrder = registrationOrder;
    }

    internal NewShimRule(Type targetType, Func<object?[], object?> argsFactory, Guid contextId, long registrationOrder)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(argsFactory);
        TargetType = targetType;
        ArgsFactory = argsFactory;
        ContextId = contextId;
        RegistrationOrder = registrationOrder;
    }

    internal NewShimRule(Type targetType, Func<ShimConstructorContext, object?> contextFactory, Guid contextId, long registrationOrder)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(contextFactory);
        TargetType = targetType;
        ContextFactory = contextFactory;
        ContextId = contextId;
        RegistrationOrder = registrationOrder;
    }

    /// <summary>
    /// Gets the target type this rule replaces.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Gets the parameterless replacement factory, or <see langword="null"/> when an args/context factory is registered.
    /// </summary>
    public Func<object?>? Factory { get; }

    /// <summary>
    /// Gets the args-based replacement factory, or <see langword="null"/> when a parameterless or context factory is registered.
    /// </summary>
    public Func<object?[], object?>? ArgsFactory { get; }

    /// <summary>
    /// Gets the context-based replacement factory, or <see langword="null"/> when a parameterless or args factory is registered.
    /// </summary>
    public Func<ShimConstructorContext, object?>? ContextFactory { get; }

    /// <summary>
    /// Gets the context ID that owns this rule.
    /// </summary>
    public Guid ContextId { get; }

    /// <summary>
    /// Gets the registration order within the owning context.
    /// </summary>
    public long RegistrationOrder { get; }

    internal object? CreateInstance() => CreateInstanceWithArgs([]);

    internal object? CreateInstanceWithArgs(object?[] args)
    {
        if (ContextFactory is not null)
            return ContextFactory(new ShimConstructorContext(TargetType, args));
        if (ArgsFactory is not null)
            return ArgsFactory(args);
        return Factory?.Invoke();
    }
}
