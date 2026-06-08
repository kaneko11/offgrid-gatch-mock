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

    /// <summary>
    /// Gets the target type this rule replaces.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Gets the replacement factory.
    /// </summary>
    public Func<object?> Factory { get; }

    /// <summary>
    /// Gets the context ID that owns this rule.
    /// </summary>
    public Guid ContextId { get; }

    /// <summary>
    /// Gets the registration order within the owning context.
    /// </summary>
    public long RegistrationOrder { get; }

    internal object? CreateInstance()
    {
        return Factory();
    }
}
