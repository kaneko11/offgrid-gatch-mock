namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes the most recent rewritten instance-method dispatch, including signature, selected
/// rule, return validation, and fallback information.
/// </summary>
public sealed class MethodDispatchDiagnostics
{
    internal MethodDispatchDiagnostics(
        string targetType,
        string methodSignature,
        Type expectedReturnType,
        bool isVirtual,
        string callingAssembly,
        string callingMethod,
        bool replacementFound,
        string? selectedRule,
        string? registrationSource,
        Type? actualReturnType,
        bool nullReturnedForNonNullableValueType,
        IReadOnlyList<string> triedRules,
        Exception? callbackException = null)
    {
        TargetType = targetType;
        MethodSignature = methodSignature;
        ExpectedReturnType = expectedReturnType;
        IsVirtual = isVirtual;
        CallingAssembly = callingAssembly;
        CallingMethod = callingMethod;
        ReplacementFound = replacementFound;
        SelectedRule = selectedRule;
        RegistrationSource = registrationSource;
        ActualReturnType = actualReturnType;
        NullReturnedForNonNullableValueType = nullReturnedForNonNullableValueType;
        TriedRules = triedRules;
        CallbackException = callbackException;
    }

    /// <summary>Gets the declaring target type full name.</summary>
    public string TargetType { get; }

    /// <summary>Gets the exact reflected/call-site method signature.</summary>
    public string MethodSignature { get; }

    /// <summary>Gets the parameter portion of the exact signature for diagnostics.</summary>
    public string ParameterTypes
    {
        get
        {
            var open = MethodSignature.IndexOf('(');
            var close = MethodSignature.LastIndexOf(')');
            return open >= 0 && close > open
                ? MethodSignature.Substring(open, close - open + 1)
                : "<unknown>";
        }
    }

    /// <summary>Gets the wrapper's required return type.</summary>
    public Type ExpectedReturnType { get; }

    /// <summary>Gets whether reflection/metadata identified the target as virtual.</summary>
    public bool IsVirtual { get; }

    /// <summary>Gets the selected backend.</summary>
    public MethodInterceptionBackend SelectedBackend => MethodInterceptionBackend.InstanceCallSiteRewrite;

    /// <summary>Gets the rewritten calling assembly.</summary>
    public string CallingAssembly { get; }

    /// <summary>Gets the rewritten calling method.</summary>
    public string CallingMethod { get; }

    /// <summary>Gets whether a replacement rule handled the call.</summary>
    public bool ReplacementFound { get; }

    /// <summary>Gets whether the generated wrapper fell back to the original method.</summary>
    public bool FallbackToOriginal => !ReplacementFound;

    /// <summary>Gets the selected rule description.</summary>
    public string? SelectedRule { get; }

    /// <summary>Gets whether the rule came from the typed or legacy untyped API.</summary>
    public string? RegistrationSource { get; }

    /// <summary>Gets the actual replacement result type, or null when the result was null/not returned.</summary>
    public Type? ActualReturnType { get; }

    /// <summary>Gets whether null was returned for a non-nullable value type.</summary>
    public bool NullReturnedForNonNullableValueType { get; }

    /// <summary>Gets matcher/rule selection diagnostics.</summary>
    public IReadOnlyList<string> TriedRules { get; }

    /// <summary>Gets an exception thrown by the replacement callback, if any.</summary>
    public Exception? CallbackException { get; }

    /// <summary>Formats the diagnostics as labeled human-readable text.</summary>
    public string Format()
    {
        var lines = new List<string>
        {
            "Target type: " + TargetType,
            "Exact MethodInfo signature: " + MethodSignature,
            "Return type: " + MethodSignatureFormatter.FormatType(ExpectedReturnType),
            "Parameter types: " + ParameterTypes,
            "Instance / static: instance",
            "Virtual / non-virtual: " + (IsVirtual ? "virtual" : "non-virtual"),
            "Selected backend: " + SelectedBackend,
            "Expected return type: " + MethodSignatureFormatter.FormatType(ExpectedReturnType),
            "Actual replacement return type: " +
                (ActualReturnType is null ? "<null / not returned>" : MethodSignatureFormatter.FormatType(ActualReturnType)),
            "Null returned for non-nullable value type: " + NullReturnedForNonNullableValueType,
            "Registration source: " + (RegistrationSource ?? "<none>"),
            "Calling assembly: " + CallingAssembly,
            "Calling method: " + CallingMethod,
            "Selected rule: " + (SelectedRule ?? "<none>"),
            "Fallback to original: " + FallbackToOriginal,
        };

        if (TriedRules.Count > 0)
        {
            lines.Add("Tried rules:");
            lines.AddRange(TriedRules.Select(rule => "  " + rule));
        }

        if (CallbackException is not null)
            lines.Add("Callback exception: " + CallbackException.GetType().FullName + ": " + CallbackException.Message);

        return string.Join(Environment.NewLine, lines);
    }
}
