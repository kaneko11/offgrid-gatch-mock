namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Configures an experimental assembly rewrite operation.
/// </summary>
public sealed class RewriteOptions
{
    /// <summary>
    /// Gets the allowed target types whose parameterless <c>newobj</c> call sites may be rewritten.
    /// </summary>
    public IReadOnlyList<Type> TargetTypes { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the rewriter should copy nearby runtime files to the output directory.
    /// </summary>
    public bool CopyRuntimeFiles { get; init; } = true;

    internal NewObjScanOptions ToScanOptions()
    {
        return new NewObjScanOptions
        {
            TargetTypes = TargetTypes,
        };
    }
}
