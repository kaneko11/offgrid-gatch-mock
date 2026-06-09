namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes a static method call site that was rewritten (or skipped) by <see cref="StaticCallRewriter"/>.
/// </summary>
public sealed class StaticCallSite
{
    internal StaticCallSite(
        string callingTypeName,
        string callingMethodName,
        int ilOffset,
        string targetTypeFullName,
        string targetMethodName,
        string[] parameterTypeNames,
        string returnTypeName,
        bool isVoid,
        bool wasRewritten,
        string? skipReason = null)
    {
        CallingTypeName = callingTypeName;
        CallingMethodName = callingMethodName;
        ILOffset = ilOffset;
        TargetTypeFullName = targetTypeFullName;
        TargetMethodName = targetMethodName;
        ParameterTypeNames = parameterTypeNames;
        ReturnTypeName = returnTypeName;
        IsVoid = isVoid;
        WasRewritten = wasRewritten;
        SkipReason = skipReason;
    }

    /// <summary>Gets the full name of the type containing the call site.</summary>
    public string CallingTypeName { get; }

    /// <summary>Gets the method containing the call site.</summary>
    public string CallingMethodName { get; }

    /// <summary>Gets the IL byte offset of the rewritten call instruction.</summary>
    public int ILOffset { get; }

    /// <summary>Gets the full name of the called method's declaring type.</summary>
    public string TargetTypeFullName { get; }

    /// <summary>Gets the name of the called method.</summary>
    public string TargetMethodName { get; }

    /// <summary>Gets the full names of the parameter types.</summary>
    public string[] ParameterTypeNames { get; }

    /// <summary>Gets the full name of the return type.</summary>
    public string ReturnTypeName { get; }

    /// <summary>Gets whether the original return type is void.</summary>
    public bool IsVoid { get; }

    /// <summary>Gets whether this call site was rewritten.</summary>
    public bool WasRewritten { get; }

    /// <summary>Gets the reason this call site was skipped, or <see langword="null"/> when rewritten.</summary>
    public string? SkipReason { get; }
}
