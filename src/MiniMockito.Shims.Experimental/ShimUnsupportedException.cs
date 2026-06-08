namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Exception thrown when an experimental shim target or pattern is unsupported.
/// </summary>
public class ShimUnsupportedException : ShimException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShimUnsupportedException"/> class.
    /// </summary>
    public ShimUnsupportedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShimUnsupportedException"/> class with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public ShimUnsupportedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShimUnsupportedException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ShimUnsupportedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
