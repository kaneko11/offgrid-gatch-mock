namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Exception reserved for future assembly rewrite failures.
/// </summary>
public class ShimRewriteException : ShimException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShimRewriteException"/> class.
    /// </summary>
    public ShimRewriteException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShimRewriteException"/> class with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public ShimRewriteException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShimRewriteException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ShimRewriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
