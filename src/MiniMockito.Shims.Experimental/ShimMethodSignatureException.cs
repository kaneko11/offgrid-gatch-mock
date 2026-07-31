namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Thrown when a method replacement cannot resolve or validate an exact method signature.
/// </summary>
public sealed class ShimMethodSignatureException : ShimException
{
    /// <summary>Creates a signature validation exception.</summary>
    public ShimMethodSignatureException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a signature validation exception with an inner exception.</summary>
    public ShimMethodSignatureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
