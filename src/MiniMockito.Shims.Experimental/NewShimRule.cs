namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes a registered replacement for a target constructor call.
/// </summary>
public sealed class NewShimRule
{
    internal NewShimRule(Type targetType, Func<object?> factory, Guid contextId, long registrationOrder, IReadOnlyList<IShimArgumentMatcher>? matchers = null, string? externalAssemblySimpleName = null)
    {
        ThrowHelper.ThrowIfNull(targetType);
        ThrowHelper.ThrowIfNull(factory);
        TargetType = targetType;
        Factory = factory;
        ContextId = contextId;
        RegistrationOrder = registrationOrder;
        ArgumentMatchers = matchers;
        ExternalAssemblySimpleName = externalAssemblySimpleName;
    }

    internal NewShimRule(Type targetType, Func<object?[], object?> argsFactory, Guid contextId, long registrationOrder, IReadOnlyList<IShimArgumentMatcher>? matchers = null)
    {
        ThrowHelper.ThrowIfNull(targetType);
        ThrowHelper.ThrowIfNull(argsFactory);
        TargetType = targetType;
        ArgsFactory = argsFactory;
        ContextId = contextId;
        RegistrationOrder = registrationOrder;
        ArgumentMatchers = matchers;
    }

    internal NewShimRule(Type targetType, Func<ShimConstructorContext, object?> contextFactory, Guid contextId, long registrationOrder, IReadOnlyList<IShimArgumentMatcher>? matchers = null)
    {
        ThrowHelper.ThrowIfNull(targetType);
        ThrowHelper.ThrowIfNull(contextFactory);
        TargetType = targetType;
        ContextFactory = contextFactory;
        ContextId = contextId;
        RegistrationOrder = registrationOrder;
        ArgumentMatchers = matchers;
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
    /// Gets the simple name of the assembly that defines the external target type, or
    /// <see langword="null"/> for an internal (same-assembly) target.  External rules are keyed
    /// by <see cref="Type.FullName"/> rather than by runtime <see cref="Type"/> identity.
    /// </summary>
    public string? ExternalAssemblySimpleName { get; }

    /// <summary>Gets a value indicating whether this rule targets a cross-assembly (external) type.</summary>
    public bool IsExternal => ExternalAssemblySimpleName is not null;

    /// <summary>
    /// Gets the context ID that owns this rule.
    /// </summary>
    public Guid ContextId { get; }

    /// <summary>
    /// Gets the registration order within the owning context.
    /// </summary>
    public long RegistrationOrder { get; }

    /// <summary>
    /// Gets the optional argument matchers, or <see langword="null"/> for a catch-all rule.
    /// A catch-all rule matches regardless of constructor arguments.
    /// An empty list matches only when the argument count is zero (parameterless constructor).
    /// </summary>
    public IReadOnlyList<IShimArgumentMatcher>? ArgumentMatchers { get; }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="args"/> satisfies this rule's argument matchers.
    /// A rule with no matchers (<see cref="ArgumentMatchers"/> is <see langword="null"/>) is a catch-all
    /// and matches any argument list.
    /// </summary>
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
