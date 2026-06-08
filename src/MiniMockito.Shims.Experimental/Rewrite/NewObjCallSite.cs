namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes one detected <c>newobj</c> instruction.
/// </summary>
public sealed class NewObjCallSite
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NewObjCallSite"/> class.
    /// </summary>
    public NewObjCallSite(
        string assemblyPath,
        string targetTypeName,
        string targetConstructor,
        string callingTypeName,
        string callingMethodName,
        int ilOffset,
        bool isSupported,
        string? unsupportedReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTypeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetConstructor);
        ArgumentException.ThrowIfNullOrWhiteSpace(callingTypeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(callingMethodName);
        AssemblyPath = Path.GetFullPath(assemblyPath);
        TargetTypeName = targetTypeName;
        TargetConstructor = targetConstructor;
        CallingTypeName = callingTypeName;
        CallingMethodName = callingMethodName;
        ILOffset = ilOffset;
        IsSupported = isSupported;
        UnsupportedReason = unsupportedReason;
    }

    /// <summary>
    /// Gets the scanned assembly path.
    /// </summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// Gets the constructed target type name.
    /// </summary>
    public string TargetTypeName { get; }

    /// <summary>
    /// Gets the constructor signature.
    /// </summary>
    public string TargetConstructor { get; }

    /// <summary>
    /// Gets the type that contains the call site.
    /// </summary>
    public string CallingTypeName { get; }

    /// <summary>
    /// Gets the method that contains the call site.
    /// </summary>
    public string CallingMethodName { get; }

    /// <summary>
    /// Gets the IL offset of the <c>newobj</c> instruction.
    /// </summary>
    public int ILOffset { get; }

    /// <summary>
    /// Gets whether this call site satisfies the Phase 3 supported pattern.
    /// </summary>
    public bool IsSupported { get; }

    /// <summary>
    /// Gets the unsupported reason when <see cref="IsSupported"/> is <see langword="false"/>.
    /// </summary>
    public string? UnsupportedReason { get; }
}
