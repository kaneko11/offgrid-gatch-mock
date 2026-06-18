using System.Text;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Captures diagnostics for a single <see cref="ShimDispatcher"/> dispatch attempt.
/// Accessible via <see cref="ShimContext.LastDispatchDiagnostics"/> after each dispatch call.
/// </summary>
/// <remarks>
/// <b>Experimental.</b> This API may change in future phases.
/// <para>
/// Diagnostics are recorded after every dispatch within an active <see cref="ShimContext"/>,
/// whether or not a matching rule was found.
/// Use <see cref="MatchFound"/> and <see cref="FalledBack"/> to distinguish outcomes.
/// </para>
/// <para>
/// <see cref="Format"/> returns a human-readable multi-line string suitable for debug assertions.
/// </para>
/// </remarks>
public sealed class ShimDispatchDiagnostics
{
    internal ShimDispatchDiagnostics(
        Type targetType,
        IReadOnlyList<object?> actualArguments,
        IReadOnlyList<TriedRuleInfo> triedRules,
        bool matchFound,
        bool resolvedByFullNameFallback = false,
        bool duplicateFullNameRisk = false)
    {
        TargetType = targetType;
        ActualArguments = actualArguments;
        TriedRules = triedRules;
        MatchFound = matchFound;
        ResolvedByFullNameFallback = resolvedByFullNameFallback;
        DuplicateFullNameRisk = duplicateFullNameRisk;
    }

    /// <summary>Gets the type that was being constructed.</summary>
    public Type TargetType { get; }

    /// <summary>Gets the actual constructor arguments that were passed, in declaration order. Value types are boxed.</summary>
    public IReadOnlyList<object?> ActualArguments { get; }

    /// <summary>
    /// Gets information about each rule that was evaluated during matching,
    /// in evaluation order (most recently registered first).
    /// Only rules evaluated before the first match (inclusive) are included.
    /// </summary>
    public IReadOnlyList<TriedRuleInfo> TriedRules { get; }

    /// <summary>Gets a value indicating whether a matching rule was found.</summary>
    public bool MatchFound { get; }

    /// <summary>Gets a value indicating whether the real constructor was used as a fallback (i.e. no rule matched).</summary>
    public bool FalledBack => !MatchFound;

    /// <summary>
    /// Gets a value indicating whether the matching rule was resolved through the cross-assembly
    /// <see cref="Type.FullName"/> fallback lookup (used for external targets) rather than an exact
    /// runtime <see cref="Type"/> match.
    /// </summary>
    public bool ResolvedByFullNameFallback { get; }

    /// <summary>
    /// Gets a value indicating whether more than one external rule shares the matched
    /// <see cref="Type.FullName"/> but originates from differently named assemblies.  When
    /// <see langword="true"/> the FullName-based lookup may be ambiguous.
    /// </summary>
    public bool DuplicateFullNameRisk { get; }

    /// <summary>
    /// Returns a human-readable formatted diagnostics string suitable for debug output and test assertions.
    /// </summary>
    /// <returns>
    /// A multi-line string containing the target type, actual arguments, tried rules with matcher
    /// descriptions, and whether fallback to the real constructor occurred.
    /// </returns>
    public string Format()
    {
        var sb = new StringBuilder();

        if (MatchFound)
            sb.AppendLine("Matching new shim rule found.");
        else
            sb.AppendLine("No matching new shim rule was found.");

        sb.AppendLine();
        sb.AppendLine($"Target type: {TargetType.Name}");

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
                {
                    sb.AppendLine("    (catch-all)");
                }
                else
                {
                    for (int i = 0; i < r.MatcherDescriptions.Count; i++)
                        sb.AppendLine($"    [{i}] expected: {r.MatcherDescriptions[i]}");
                }
                sb.AppendLine($"    result: {(r.Matched ? "matched" : "mismatch")}");
                if (!r.Matched && !string.IsNullOrEmpty(r.MismatchReason))
                    sb.AppendLine($"    reason: {r.MismatchReason}");
            }
        }

        if (ResolvedByFullNameFallback)
        {
            sb.AppendLine();
            sb.AppendLine("Shim lookup: resolved by FullName fallback (external target).");
            if (DuplicateFullNameRisk)
                sb.AppendLine("Warning: duplicate FullName risk — multiple external rules share this FullName across assemblies.");
        }

        if (FalledBack)
        {
            sb.AppendLine();
            sb.Append("Fallback: real constructor");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Information about a single rule that was evaluated during a dispatch attempt.
    /// </summary>
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

        /// <summary>Gets the registration order of the rule (monotonically increasing from 1).</summary>
        public long RegistrationOrder { get; }

        /// <summary>
        /// Gets the <see cref="IShimArgumentMatcher.Describe"/> output for each matcher in declaration order,
        /// or an empty list for a catch-all rule.
        /// </summary>
        public IReadOnlyList<string> MatcherDescriptions { get; }

        /// <summary>Gets a value indicating whether this rule matched the actual arguments.</summary>
        public bool Matched { get; }

        /// <summary>Gets a description of why the rule did not match, or an empty string when it matched.</summary>
        public string MismatchReason { get; }
    }
}
