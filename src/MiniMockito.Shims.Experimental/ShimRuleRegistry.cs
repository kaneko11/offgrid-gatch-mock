namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Stores shim rules for a single <see cref="ShimContext"/>.
/// </summary>
public sealed class ShimRuleRegistry
{
    private readonly Dictionary<Type, NewShimRule> _newRules = [];
    private readonly object _syncRoot = new();
    private long _nextRegistrationOrder;

    /// <summary>
    /// Gets the number of registered new-shim rules.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _newRules.Count;
            }
        }
    }

    /// <summary>
    /// Registers a parameterless replacement factory for <paramref name="targetType"/>.
    /// </summary>
    public NewShimRule RegisterNewRule(Type targetType, Func<object?> factory, Guid contextId)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(factory);

        lock (_syncRoot)
        {
            var rule = new NewShimRule(targetType, factory, contextId, ++_nextRegistrationOrder);
            _newRules[targetType] = rule;
            return rule;
        }
    }

    /// <summary>
    /// Registers an args-based replacement factory for <paramref name="targetType"/>.
    /// The factory receives the boxed constructor arguments in declaration order.
    /// </summary>
    public NewShimRule RegisterNewRule(Type targetType, Func<object?[], object?> argsFactory, Guid contextId)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(argsFactory);

        lock (_syncRoot)
        {
            var rule = new NewShimRule(targetType, argsFactory, contextId, ++_nextRegistrationOrder);
            _newRules[targetType] = rule;
            return rule;
        }
    }

    /// <summary>
    /// Registers a <see cref="ShimConstructorContext"/>-based replacement factory for <paramref name="targetType"/>.
    /// </summary>
    public NewShimRule RegisterNewRuleWithContext(Type targetType, Func<ShimConstructorContext, object?> contextFactory, Guid contextId)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(contextFactory);

        lock (_syncRoot)
        {
            var rule = new NewShimRule(targetType, contextFactory, contextId, ++_nextRegistrationOrder);
            _newRules[targetType] = rule;
            return rule;
        }
    }

    /// <summary>
    /// Attempts to find a registered new-shim rule for a target type.
    /// </summary>
    public bool TryFindNewRule(Type targetType, out NewShimRule? rule)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        lock (_syncRoot)
        {
            return _newRules.TryGetValue(targetType, out rule);
        }
    }

    /// <summary>
    /// Removes all rules from this registry.
    /// </summary>
    public void Clear()
    {
        lock (_syncRoot)
        {
            _newRules.Clear();
        }
    }
}
