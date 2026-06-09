namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes a registered replacement for a target static method call.
/// </summary>
public sealed class StaticShimRule
{
    private enum RuleKind { ReturnValue, Void, Throw }

    private readonly RuleKind _kind;
    private readonly Func<object?[], object?>? _factory;
    private readonly Action<object?[]>? _callback;
    private readonly Exception? _exception;

    // Value-returning rule.
    internal StaticShimRule(
        StaticMethodKey key,
        long registrationOrder,
        Func<object?[], object?> factory,
        IReadOnlyList<IShimArgumentMatcher>? matchers)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Key = key;
        RegistrationOrder = registrationOrder;
        _factory = factory;
        _kind = RuleKind.ReturnValue;
        ArgumentMatchers = matchers;
    }

    // Void rule (callback may be null → no-op).
    // The extra bool parameter disambiguates from the factory constructor at call sites.
    internal StaticShimRule(
        StaticMethodKey key,
        long registrationOrder,
        Action<object?[]>? voidCallback,
        IReadOnlyList<IShimArgumentMatcher>? matchers,
        bool isVoid)
    {
        _ = isVoid; // marker only
        Key = key;
        RegistrationOrder = registrationOrder;
        _callback = voidCallback;
        _kind = RuleKind.Void;
        ArgumentMatchers = matchers;
    }

    // Throw rule.
    internal StaticShimRule(
        StaticMethodKey key,
        long registrationOrder,
        Exception thrownException,
        IReadOnlyList<IShimArgumentMatcher>? matchers)
    {
        ArgumentNullException.ThrowIfNull(thrownException);
        Key = key;
        RegistrationOrder = registrationOrder;
        _exception = thrownException;
        _kind = RuleKind.Throw;
        ArgumentMatchers = matchers;
    }

    /// <summary>Gets the key that identifies the shimmed static method.</summary>
    public StaticMethodKey Key { get; }

    /// <summary>Gets the registration order within the owning context (monotonically increasing from 1).</summary>
    public long RegistrationOrder { get; }

    /// <summary>
    /// Gets the optional argument matchers, or <see langword="null"/> for a catch-all rule.
    /// </summary>
    public IReadOnlyList<IShimArgumentMatcher>? ArgumentMatchers { get; }

    /// <summary>Gets whether this rule replaces a void-return method.</summary>
    public bool IsVoid => _kind == RuleKind.Void;

    /// <summary>Returns <see langword="true"/> if the given boxed arguments satisfy this rule's matchers.</summary>
    internal bool MatchesArgs(object?[] args)
    {
        if (ArgumentMatchers is null) return true;
        if (args.Length != ArgumentMatchers.Count) return false;
        for (int i = 0; i < args.Length; i++)
        {
            if (!ArgumentMatchers[i].Matches(args[i])) return false;
        }
        return true;
    }

    /// <summary>
    /// Executes the rule.  Returns <see langword="true"/> when executed;
    /// throws when the rule is a throw-rule.
    /// For void rules, <paramref name="result"/> is always <see langword="null"/>.
    /// </summary>
    internal bool TryExecute(object?[] args, out object? result)
    {
        if (_kind == RuleKind.Throw)
            throw _exception!;

        if (_kind == RuleKind.Void)
        {
            _callback?.Invoke(args);
            result = null;
            return true;
        }

        result = _factory!(args);
        return true;
    }
}
