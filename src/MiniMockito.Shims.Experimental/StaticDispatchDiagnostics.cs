using System.Text;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Captures diagnostics for a single <see cref="StaticShimDispatcher"/> dispatch attempt.
/// Accessible via <see cref="ShimContext.LastStaticDispatchDiagnostics"/> after each static dispatch call.
/// </summary>
public sealed class StaticDispatchDiagnostics
{
    internal StaticDispatchDiagnostics(
        StaticMethodKey key,
        IReadOnlyList<object?> actualArguments,
        IReadOnlyList<TriedRuleInfo> triedRules,
        bool matchFound)
    {
        Key = key;
        ActualArguments = actualArguments;
        TriedRules = triedRules;
        MatchFound = matchFound;
    }

    /// <summary>Gets the key that identifies the dispatched static method.</summary>
    public StaticMethodKey Key { get; }

    /// <summary>Gets the actual boxed arguments passed to the dispatcher.</summary>
    public IReadOnlyList<object?> ActualArguments { get; }

    /// <summary>
    /// Gets information about each rule evaluated during matching, in evaluation order
    /// (most recently registered first).
    /// </summary>
    public IReadOnlyList<TriedRuleInfo> TriedRules { get; }

    /// <summary>Gets whether a matching rule was found.</summary>
    public bool MatchFound { get; }

    /// <summary>Gets whether the real static method was used as fallback (i.e. no rule matched).</summary>
    public bool FalledBack => !MatchFound;

    /// <summary>Returns a human-readable formatted diagnostics string.</summary>
    public string Format()
    {
        var sb = new StringBuilder();

        if (MatchFound)
            sb.AppendLine("Matching static shim rule found.");
        else
            sb.AppendLine("No matching static shim rule was found.");

        sb.AppendLine();
        sb.AppendLine($"Target: {Key.ToKeyString()}");

        if (ActualArguments.Count > 0)
        {
            sb.AppendLine("Actual arguments:");
            for (int i = 0; i < ActualArguments.Count; i++)
            {
                var arg = ActualArguments[i];
                var typeName = arg?.GetType().FullName ?? "null";
                var valueStr = arg is string s ? $"\"{s}\"" : (arg?.ToString() ?? "null");
                sb.AppendLine($"  [{i}] {valueStr} ({typeName})");
            }
        }
        else
        {
            sb.AppendLine("Actual arguments: (none)");
        }

        if (TriedRules.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Tried rules:");
            foreach (var r in TriedRules)
            {
                sb.AppendLine($"  Rule #{r.RegistrationOrder}:");
                if (r.MatcherDescriptions.Count == 0)
                    sb.AppendLine("    (catch-all)");
                else
                    for (int i = 0; i < r.MatcherDescriptions.Count; i++)
                        sb.AppendLine($"    [{i}] expected: {r.MatcherDescriptions[i]}");
                sb.AppendLine($"    result: {(r.Matched ? "matched" : "mismatch")}");
                if (!r.Matched && !string.IsNullOrEmpty(r.MismatchReason))
                    sb.AppendLine($"    reason: {r.MismatchReason}");
            }
        }

        if (FalledBack)
        {
            sb.AppendLine();
            sb.Append("Fallback: real static method call");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Information about a single rule evaluated during a dispatch attempt.</summary>
    public sealed class TriedRuleInfo
    {
        internal TriedRuleInfo(
            long registrationOrder,
            IReadOnlyList<string> matcherDescriptions,
            bool matched,
            string mismatchReason)
        {
            RegistrationOrder = registrationOrder;
            MatcherDescriptions = matcherDescriptions;
            Matched = matched;
            MismatchReason = mismatchReason;
        }

        /// <summary>Gets the rule's registration order.</summary>
        public long RegistrationOrder { get; }

        /// <summary>Gets the <see cref="IShimArgumentMatcher.Describe"/> output for each matcher.</summary>
        public IReadOnlyList<string> MatcherDescriptions { get; }

        /// <summary>Gets whether this rule matched.</summary>
        public bool Matched { get; }

        /// <summary>Gets the mismatch reason, or empty string when matched.</summary>
        public string MismatchReason { get; }
    }
}
