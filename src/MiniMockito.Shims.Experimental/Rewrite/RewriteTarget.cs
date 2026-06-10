namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes a target type that may be considered by a rewrite dry-run scan.
/// </summary>
public sealed class RewriteTarget
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RewriteTarget"/> class.
    /// </summary>
    /// <param name="targetType">The allowlisted target type.</param>
    public RewriteTarget(Type targetType)
    {
        ThrowHelper.ThrowIfNull(targetType);
        TargetType = targetType;
    }

    /// <summary>
    /// Gets the allowlisted target type.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Gets the target type full name.
    /// </summary>
    public string TargetTypeName => TargetType.FullName ?? TargetType.Name;

    internal bool Matches(Type candidateType)
    {
        ThrowHelper.ThrowIfNull(candidateType);
        return string.Equals(TargetType.AssemblyQualifiedName, candidateType.AssemblyQualifiedName, StringComparison.Ordinal)
            || string.Equals(TargetType.FullName, candidateType.FullName, StringComparison.Ordinal);
    }
}
