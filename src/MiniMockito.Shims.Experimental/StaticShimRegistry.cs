namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Stores static shim rules for a single <see cref="ShimContext"/>.
/// Multiple rules may be registered for the same method.
/// When multiple rules match, the most recently registered rule wins.
/// </summary>
public sealed class StaticShimRegistry
{
    private readonly Dictionary<string, List<StaticShimRule>> _rules =
        new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();
    private long _nextOrder;

    /// <summary>Gets the total number of registered rules across all methods.</summary>
    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                int total = 0;
                foreach (var list in _rules.Values) total += list.Count;
                return total;
            }
        }
    }

    /// <summary>Registers a value-returning rule.</summary>
    internal StaticShimRule RegisterRule(
        StaticMethodKey key,
        Func<object?[], object?> factory,
        IReadOnlyList<IShimArgumentMatcher>? matchers)
    {
        lock (_syncRoot)
        {
            var rule = new StaticShimRule(key, ++_nextOrder, factory, matchers);
            GetOrCreate(key.ToKeyString()).Add(rule);
            return rule;
        }
    }

    /// <summary>Registers a void rule (no-op or callback).</summary>
    internal StaticShimRule RegisterVoidRule(
        StaticMethodKey key,
        Action<object?[]>? callback,
        IReadOnlyList<IShimArgumentMatcher>? matchers)
    {
        lock (_syncRoot)
        {
            var rule = new StaticShimRule(key, ++_nextOrder, callback, matchers, isVoid: true);
            GetOrCreate(key.ToKeyString()).Add(rule);
            return rule;
        }
    }

    /// <summary>Registers a throw rule.</summary>
    internal StaticShimRule RegisterThrowRule(
        StaticMethodKey key,
        Exception exception,
        IReadOnlyList<IShimArgumentMatcher>? matchers)
    {
        lock (_syncRoot)
        {
            var rule = new StaticShimRule(key, ++_nextOrder, exception, matchers);
            GetOrCreate(key.ToKeyString()).Add(rule);
            return rule;
        }
    }

    /// <summary>
    /// Finds the best matching rule (last-registered-wins) and collects per-rule diagnostics.
    /// </summary>
    internal bool TryFindRuleWithDiagnostics(
        StaticMethodKey key,
        object?[] args,
        out StaticShimRule? rule,
        out StaticDispatchDiagnostics diagnostics)
    {
        lock (_syncRoot)
        {
            if (!_rules.TryGetValue(key.ToKeyString(), out var list) || list.Count == 0)
            {
                rule = null;
                diagnostics = new StaticDispatchDiagnostics(key, args, [], matchFound: false);
                return false;
            }

            var tried = new List<StaticDispatchDiagnostics.TriedRuleInfo>();

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var r = list[i];
                var matchers = r.ArgumentMatchers;
                bool matched;
                string mismatchReason;
                List<string> descriptions;

                if (matchers is null)
                {
                    matched = true;
                    mismatchReason = string.Empty;
                    descriptions = [];
                }
                else if (args.Length != matchers.Count)
                {
                    matched = false;
                    mismatchReason = $"Expected {matchers.Count} argument(s), got {args.Length}";
                    descriptions = matchers.Select(m => m.Describe()).ToList();
                }
                else
                {
                    matched = true;
                    mismatchReason = string.Empty;
                    descriptions = [];
                    for (int j = 0; j < args.Length; j++)
                    {
                        var desc = matchers[j].Describe();
                        descriptions.Add(desc);
                        if (!matchers[j].Matches(args[j]))
                        {
                            matched = false;
                            var valStr = args[j] is string sv ? $"\"{sv}\"" : (args[j]?.ToString() ?? "null");
                            mismatchReason = $"Matcher [{j}] ({desc}) did not match actual value: {valStr}";
                            for (int k = j + 1; k < matchers.Count; k++)
                                descriptions.Add(matchers[k].Describe());
                            break;
                        }
                    }
                }

                tried.Add(new StaticDispatchDiagnostics.TriedRuleInfo(
                    r.RegistrationOrder, descriptions.AsReadOnly(), matched, mismatchReason));

                if (matched)
                {
                    rule = r;
                    diagnostics = new StaticDispatchDiagnostics(key, args, tried.AsReadOnly(), matchFound: true);
                    return true;
                }
            }

            rule = null;
            diagnostics = new StaticDispatchDiagnostics(key, args, tried.AsReadOnly(), matchFound: false);
            return false;
        }
    }

    /// <summary>Removes all rules.</summary>
    public void Clear()
    {
        lock (_syncRoot) { _rules.Clear(); }
    }

    private List<StaticShimRule> GetOrCreate(string keyStr)
    {
        if (!_rules.TryGetValue(keyStr, out var list))
        {
            list = [];
            _rules[keyStr] = list;
        }
        return list;
    }
}
