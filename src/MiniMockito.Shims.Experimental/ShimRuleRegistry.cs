namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Stores shim rules for a single <see cref="ShimContext"/>.
/// Multiple rules may be registered for the same target type to support constructor overloads
/// and argument-specific matching.  When multiple rules match, the most recently registered
/// rule wins (Mockito-style: "last stub wins").
/// </summary>
public sealed class ShimRuleRegistry
{
    private readonly Dictionary<Type, List<NewShimRule>> _newRules = [];
    private readonly Dictionary<string, List<NewShimRule>> _newRulesByName = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();
    private long _nextRegistrationOrder;

    /// <summary>
    /// Gets the total number of registered new-shim rules across all target types,
    /// including both runtime-<see cref="Type"/>-keyed (internal) and FullName-keyed (external) rules.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                var total = 0;
                foreach (var list in _newRules.Values)
                    total += list.Count;
                foreach (var list in _newRulesByName.Values)
                    total += list.Count;
                return total;
            }
        }
    }

    /// <summary>
    /// Registers a parameterless replacement factory for <paramref name="targetType"/>.
    /// The rule is a catch-all and matches any constructor argument list.
    /// </summary>
    public NewShimRule RegisterNewRule(Type targetType, Func<object?> factory, Guid contextId)
        => RegisterNewRule(targetType, factory, contextId, matchers: null);

    /// <summary>
    /// Registers an args-based replacement factory for <paramref name="targetType"/>.
    /// The factory receives the boxed constructor arguments in declaration order.
    /// The rule is a catch-all and matches any constructor argument list.
    /// </summary>
    public NewShimRule RegisterNewRule(Type targetType, Func<object?[], object?> argsFactory, Guid contextId)
        => RegisterNewRule(targetType, argsFactory, contextId, matchers: null);

    /// <summary>
    /// Registers a <see cref="ShimConstructorContext"/>-based replacement factory for <paramref name="targetType"/>.
    /// The rule is a catch-all and matches any constructor argument list.
    /// </summary>
    public NewShimRule RegisterNewRuleWithContext(Type targetType, Func<ShimConstructorContext, object?> contextFactory, Guid contextId)
        => RegisterNewRuleWithContext(targetType, contextFactory, contextId, matchers: null);

    /// <summary>
    /// Registers a parameterless replacement factory with optional argument matchers.
    /// When <paramref name="matchers"/> is <see langword="null"/> the rule is a catch-all.
    /// </summary>
    internal NewShimRule RegisterNewRule(Type targetType, Func<object?> factory, Guid contextId, IReadOnlyList<IShimArgumentMatcher>? matchers)
    {
        ThrowHelper.ThrowIfNull(targetType);
        ThrowHelper.ThrowIfNull(factory);

        lock (_syncRoot)
        {
            var rule = new NewShimRule(targetType, factory, contextId, ++_nextRegistrationOrder, matchers);
            GetOrCreateList(targetType).Add(rule);
            return rule;
        }
    }

    /// <summary>
    /// Registers an args-based replacement factory with optional argument matchers.
    /// </summary>
    internal NewShimRule RegisterNewRule(Type targetType, Func<object?[], object?> argsFactory, Guid contextId, IReadOnlyList<IShimArgumentMatcher>? matchers)
    {
        ThrowHelper.ThrowIfNull(targetType);
        ThrowHelper.ThrowIfNull(argsFactory);

        lock (_syncRoot)
        {
            var rule = new NewShimRule(targetType, argsFactory, contextId, ++_nextRegistrationOrder, matchers);
            GetOrCreateList(targetType).Add(rule);
            return rule;
        }
    }

    /// <summary>
    /// Registers a <see cref="ShimConstructorContext"/>-based factory with optional argument matchers.
    /// </summary>
    internal NewShimRule RegisterNewRuleWithContext(Type targetType, Func<ShimConstructorContext, object?> contextFactory, Guid contextId, IReadOnlyList<IShimArgumentMatcher>? matchers)
    {
        ThrowHelper.ThrowIfNull(targetType);
        ThrowHelper.ThrowIfNull(contextFactory);

        lock (_syncRoot)
        {
            var rule = new NewShimRule(targetType, contextFactory, contextId, ++_nextRegistrationOrder, matchers);
            GetOrCreateList(targetType).Add(rule);
            return rule;
        }
    }

    /// <summary>
    /// Registers a parameterless replacement factory for an <b>external</b> (cross-assembly) target,
    /// keyed by <paramref name="typeFullName"/> rather than by runtime <see cref="Type"/> identity.
    /// This allows the rule to be found even when the <c>newobj</c> call site resolves the type
    /// through a different load context than the one that registered the shim.
    /// </summary>
    /// <param name="typeFullName">The external type's <see cref="Type.FullName"/> (the primary key).</param>
    /// <param name="assemblySimpleName">The simple name of the assembly that defines the external type.</param>
    /// <param name="originalType">The external type as seen by the registering load context.</param>
    /// <param name="factory">The replacement factory.</param>
    /// <param name="contextId">The owning context id.</param>
    /// <param name="matchers">Optional argument matchers; <see langword="null"/> means catch-all.</param>
    internal NewShimRule RegisterNewRuleByName(
        string typeFullName,
        string assemblySimpleName,
        Type originalType,
        Func<object?> factory,
        Guid contextId,
        IReadOnlyList<IShimArgumentMatcher>? matchers)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(typeFullName);
        ThrowHelper.ThrowIfNull(originalType);
        ThrowHelper.ThrowIfNull(factory);

        lock (_syncRoot)
        {
            var rule = new NewShimRule(
                originalType, factory, contextId, ++_nextRegistrationOrder, matchers, assemblySimpleName);
            GetOrCreateNameList(typeFullName).Add(rule);
            return rule;
        }
    }

    /// <summary>
    /// Attempts to find the most recently registered rule for a target type, regardless of
    /// argument matchers.  This is a backward-compatible lookup used when no argument context
    /// is available (e.g. existence checks in tests).
    /// </summary>
    public bool TryFindNewRule(Type targetType, out NewShimRule? rule)
    {
        ThrowHelper.ThrowIfNull(targetType);

        lock (_syncRoot)
        {
            if (_newRules.TryGetValue(targetType, out var list) && list.Count > 0)
            {
                rule = list[list.Count - 1];
                return true;
            }

            // External (FullName-keyed) fallback.
            var fullName = targetType.FullName;
            if (fullName is not null && _newRulesByName.TryGetValue(fullName, out var nameList) && nameList.Count > 0)
            {
                rule = nameList[nameList.Count - 1];
                return true;
            }

            rule = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to find the best matching rule for a target type given the actual constructor
    /// arguments.  Rules are evaluated from most recently registered to least recently registered.
    /// The first rule whose <see cref="NewShimRule.MatchesArgs"/> returns <see langword="true"/>
    /// is selected.
    /// </summary>
    /// <remarks>
    /// Rules without argument matchers (<see cref="NewShimRule.ArgumentMatchers"/> is
    /// <see langword="null"/>) are catch-all rules and match any argument list.
    /// Rules with an empty matcher list match only when <paramref name="args"/> is empty.
    /// </remarks>
    internal bool TryFindNewRuleWithArgs(Type targetType, object?[] args, out NewShimRule? rule)
    {
        ThrowHelper.ThrowIfNull(targetType);

        lock (_syncRoot)
        {
            if (_newRules.TryGetValue(targetType, out var list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].MatchesArgs(args))
                    {
                        rule = list[i];
                        return true;
                    }
                }
            }

            // External (FullName-keyed) fallback.
            var fullName = targetType.FullName;
            if (fullName is not null && _newRulesByName.TryGetValue(fullName, out var nameList))
            {
                for (int i = nameList.Count - 1; i >= 0; i--)
                {
                    if (nameList[i].MatchesArgs(args))
                    {
                        rule = nameList[i];
                        return true;
                    }
                }
            }

            rule = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to find the best matching rule and collects per-rule diagnostics.
    /// Rules are evaluated from most recently registered to least recently registered.
    /// Each matcher is called exactly once; for <see cref="ShimCaptor{T}"/> matchers this
    /// means capture side-effects occur during the single evaluation pass.
    /// </summary>
    internal bool TryFindNewRuleWithArgsDiagnostics(
        Type targetType,
        object?[] args,
        out NewShimRule? rule,
        out ShimDispatchDiagnostics diagnostics)
    {
        ThrowHelper.ThrowIfNull(targetType);

        lock (_syncRoot)
        {
            var tried = new List<ShimDispatchDiagnostics.TriedRuleInfo>();

            // 1. Exact runtime-Type match (internal targets).
            if (_newRules.TryGetValue(targetType, out var typeList) && typeList.Count > 0
                && TryMatchInList(typeList, args, tried, out var typeRule))
            {
                rule = typeRule;
                diagnostics = new ShimDispatchDiagnostics(targetType, args, tried.AsReadOnly(), matchFound: true);
                return true;
            }

            // 2. FullName fallback (external / cross-assembly targets).
            var fullName = targetType.FullName;
            if (fullName is not null && _newRulesByName.TryGetValue(fullName, out var nameList) && nameList.Count > 0
                && TryMatchInList(nameList, args, tried, out var nameRule))
            {
                rule = nameRule;
                diagnostics = new ShimDispatchDiagnostics(
                    targetType,
                    args,
                    tried.AsReadOnly(),
                    matchFound: true,
                    resolvedByFullNameFallback: true,
                    duplicateFullNameRisk: HasDuplicateAssemblyRisk(nameList));
                return true;
            }

            rule = null;
            diagnostics = new ShimDispatchDiagnostics(targetType, args, tried.AsReadOnly(), matchFound: false);
            return false;
        }
    }

    /// <summary>
    /// Evaluates the rules in <paramref name="list"/> from most recently registered to least,
    /// appending each evaluation to <paramref name="tried"/> and returning the first match.
    /// </summary>
    private static bool TryMatchInList(
        List<NewShimRule> list,
        object?[] args,
        List<ShimDispatchDiagnostics.TriedRuleInfo> tried,
        out NewShimRule? matched)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var r = list[i];
            var matchers = r.ArgumentMatchers;
            bool ruleMatched;
            string mismatchReason;
            List<string> descriptions;

            if (matchers is null)
            {
                ruleMatched = true;
                mismatchReason = string.Empty;
                descriptions = [];
            }
            else if (args.Length != matchers.Count)
            {
                ruleMatched = false;
                mismatchReason = $"Expected {matchers.Count} argument(s), got {args.Length}";
                descriptions = matchers.Select(m => m.Describe()).ToList();
            }
            else
            {
                ruleMatched = true;
                mismatchReason = string.Empty;
                descriptions = [];
                for (int j = 0; j < args.Length; j++)
                {
                    var desc = matchers[j].Describe();
                    descriptions.Add(desc);
                    if (!matchers[j].Matches(args[j]))
                    {
                        ruleMatched = false;
                        var valStr = args[j] is string sv ? $"\"{sv}\"" : (args[j]?.ToString() ?? "null");
                        mismatchReason = $"Matcher [{j}] ({desc}) did not match actual value: {valStr}";
                        for (int k = j + 1; k < matchers.Count; k++)
                            descriptions.Add(matchers[k].Describe());
                        break;
                    }
                }
            }

            tried.Add(new ShimDispatchDiagnostics.TriedRuleInfo(
                r.RegistrationOrder,
                descriptions.AsReadOnly(),
                ruleMatched,
                mismatchReason));

            if (ruleMatched)
            {
                matched = r;
                return true;
            }
        }

        matched = null;
        return false;
    }

    private static bool HasDuplicateAssemblyRisk(List<NewShimRule> nameList)
    {
        string? first = null;
        foreach (var r in nameList)
        {
            var name = r.ExternalAssemblySimpleName;
            if (name is null)
                continue;
            if (first is null)
                first = name;
            else if (!string.Equals(first, name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Removes all rules from this registry.
    /// </summary>
    public void Clear()
    {
        lock (_syncRoot)
        {
            _newRules.Clear();
            _newRulesByName.Clear();
        }
    }

    private List<NewShimRule> GetOrCreateList(Type targetType)
    {
        if (!_newRules.TryGetValue(targetType, out var list))
        {
            list = [];
            _newRules[targetType] = list;
        }

        return list;
    }

    private List<NewShimRule> GetOrCreateNameList(string typeFullName)
    {
        if (!_newRulesByName.TryGetValue(typeFullName, out var list))
        {
            list = [];
            _newRulesByName[typeFullName] = list;
        }

        return list;
    }
}
