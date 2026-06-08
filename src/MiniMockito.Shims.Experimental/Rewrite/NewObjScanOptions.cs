namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Configures a dry-run scan for <c>newobj</c> instructions.
/// </summary>
public sealed class NewObjScanOptions
{
    /// <summary>
    /// Gets or initializes the allowlisted target types.
    /// </summary>
    public IReadOnlyList<Type> TargetTypes { get; init; } = [];
}
