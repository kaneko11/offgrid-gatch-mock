using System.Reflection;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Stores instance-method replacement rules for one <see cref="ShimContext"/>.
/// Exact typed rules are keyed by declaring type, method name, and parameter types. Legacy
/// name-only rules retain their historical <c>DeclaringType::Method</c> key.
/// </summary>
public sealed class MethodShimRegistry
{
    private readonly Dictionary<string, List<MethodShimRule>> _rules =
        new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();
    private long _nextRegistrationOrder;

    /// <summary>Gets the number of registered method replacement rules.</summary>
    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _rules.Values.Sum(rules => rules.Count);
            }
        }
    }

    /// <summary>Builds the backward-compatible name-only registry key.</summary>
    public static string MakeKey(string declaringTypeFullName, string methodName)
        => declaringTypeFullName + "::" + methodName;

    /// <summary>Builds an overload-safe key from exact parameter type names.</summary>
    public static string MakeSignatureKey(
        string declaringTypeFullName,
        string methodName,
        IEnumerable<string> parameterTypeNames)
        => MakeKey(declaringTypeFullName, methodName) +
           "(" + string.Join(",", parameterTypeNames) + ")";

    internal void Register(
        string declaringTypeFullName,
        string methodName,
        Func<object?, object?[], object?> shim)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(declaringTypeFullName);
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodName);
        ThrowHelper.ThrowIfNull(shim);

        RegisterCore(new MethodShimRule(
            MakeKey(declaringTypeFullName, methodName),
            method: null,
            methodSignature: declaringTypeFullName + "." + methodName + "(<legacy name-only>)",
            expectedReturnType: null,
            registrationSource: "legacy untyped API",
            shim,
            matchers: null,
            registrationOrder: NextRegistrationOrder()));
    }

    internal void Register(
        MethodInfo method,
        Func<object?, object?[], object?> shim,
        IReadOnlyList<IShimArgumentMatcher>? matchers,
        string registrationSource)
    {
        ThrowHelper.ThrowIfNull(method);
        ThrowHelper.ThrowIfNull(shim);
        ThrowHelper.ThrowIfNullOrWhiteSpace(registrationSource);

        RegisterCore(new MethodShimRule(
            MethodSignatureFormatter.MakeRegistryKey(method),
            method,
            MethodSignatureFormatter.Format(method),
            method.ReturnType,
            registrationSource,
            shim,
            matchers,
            NextRegistrationOrder()));
    }

    internal bool TryResolve(
        string methodKey,
        object?[] arguments,
        out MethodShimRule? rule,
        out IReadOnlyList<string> triedRules)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodKey);
        ThrowHelper.ThrowIfNull(arguments);

        lock (_syncRoot)
        {
            var tried = new List<string>();

            if (_rules.TryGetValue(methodKey, out var exactRules))
            {
                if (TryMatch(exactRules, arguments, tried, out rule))
                {
                    triedRules = tried.AsReadOnly();
                    return true;
                }

                // Exact typed rules existed but their matchers did not match. Do not unexpectedly
                // fall through to a broader legacy rule; the wrapper must call the real method.
                triedRules = tried.AsReadOnly();
                return false;
            }

            var legacyKey = GetLegacyKey(methodKey);
            if (!string.Equals(legacyKey, methodKey, StringComparison.Ordinal) &&
                _rules.TryGetValue(legacyKey, out var legacyRules) &&
                TryMatch(legacyRules, arguments, tried, out rule))
            {
                triedRules = tried.AsReadOnly();
                return true;
            }

            rule = null;
            triedRules = tried.AsReadOnly();
            return false;
        }
    }

    /// <summary>Removes all registered method replacement rules.</summary>
    public void Clear()
    {
        lock (_syncRoot)
        {
            _rules.Clear();
        }
    }

    private void RegisterCore(MethodShimRule rule)
    {
        lock (_syncRoot)
        {
            if (!_rules.TryGetValue(rule.Key, out var rules))
            {
                rules = new List<MethodShimRule>();
                _rules[rule.Key] = rules;
            }

            rules.Add(rule);
        }
    }

    private long NextRegistrationOrder()
    {
        lock (_syncRoot)
        {
            return ++_nextRegistrationOrder;
        }
    }

    private static bool TryMatch(
        List<MethodShimRule> rules,
        object?[] arguments,
        List<string> tried,
        out MethodShimRule? matched)
    {
        for (var i = rules.Count - 1; i >= 0; i--)
        {
            var rule = rules[i];
            if (rule.Matchers is null)
            {
                tried.Add("Rule #" + rule.RegistrationOrder + ": catch-all -> matched");
                matched = rule;
                return true;
            }

            if (rule.Matchers.Count != arguments.Length)
            {
                tried.Add(
                    "Rule #" + rule.RegistrationOrder + ": expected " +
                    rule.Matchers.Count + " argument(s), got " + arguments.Length);
                continue;
            }

            var ruleMatched = true;
            for (var argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
            {
                var matcher = rule.Matchers[argumentIndex];
                if (matcher.Matches(arguments[argumentIndex]))
                    continue;

                tried.Add(
                    "Rule #" + rule.RegistrationOrder + ": matcher [" + argumentIndex +
                    "] " + matcher.Describe() + " did not match " +
                    FormatArgument(arguments[argumentIndex]));
                ruleMatched = false;
                break;
            }

            if (!ruleMatched)
                continue;

            tried.Add("Rule #" + rule.RegistrationOrder + ": all matchers -> matched");
            matched = rule;
            return true;
        }

        matched = null;
        return false;
    }

    private static string GetLegacyKey(string methodKey)
    {
        var openParen = methodKey.IndexOf('(');
        return openParen < 0 ? methodKey : methodKey.Substring(0, openParen);
    }

    private static string FormatArgument(object? argument)
        => argument is null
            ? "null"
            : argument + " (" + (argument.GetType().FullName ?? argument.GetType().Name) + ")";
}

internal sealed class MethodShimRule
{
    internal MethodShimRule(
        string key,
        MethodInfo? method,
        string methodSignature,
        Type? expectedReturnType,
        string registrationSource,
        Func<object?, object?[], object?> shim,
        IReadOnlyList<IShimArgumentMatcher>? matchers,
        long registrationOrder)
    {
        Key = key;
        Method = method;
        MethodSignature = methodSignature;
        ExpectedReturnType = expectedReturnType;
        RegistrationSource = registrationSource;
        Shim = shim;
        Matchers = matchers;
        RegistrationOrder = registrationOrder;
    }

    internal string Key { get; }
    internal MethodInfo? Method { get; }
    internal string MethodSignature { get; }
    internal Type? ExpectedReturnType { get; }
    internal string RegistrationSource { get; }
    internal Func<object?, object?[], object?> Shim { get; }
    internal IReadOnlyList<IShimArgumentMatcher>? Matchers { get; }
    internal long RegistrationOrder { get; }
}
