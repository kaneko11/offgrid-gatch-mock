namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Thrown before a rewritten wrapper casts or unboxes an incompatible replacement result.
/// </summary>
public sealed class ShimReturnTypeMismatchException : ShimException
{
    /// <summary>Creates a replacement return-type mismatch exception.</summary>
    public ShimReturnTypeMismatchException(string message)
        : base(message)
    {
    }
}
