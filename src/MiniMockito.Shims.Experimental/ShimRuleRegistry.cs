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
    /// Registers a new-shim rule.
    /// </summary>
    /// <param name="targetType">The target type.</param>
    /// <param name="factory">The replacement factory.</param>
    /// <param name="contextId">The owning context ID.</param>
    /// <returns>The registered rule.</returns>
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
    /// Attempts to find a registered new-shim rule for a target type.
    /// </summary>
    /// <param name="targetType">The target type.</param>
    /// <param name="rule">The matching rule when found.</param>
    /// <returns><see langword="true"/> when a matching rule exists.</returns>
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
