namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Base exception type for MiniMockito shim failures.
/// </summary>
public class ShimException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShimException"/> class.
    /// </summary>
    public ShimException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShimException"/> class with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public ShimException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShimException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ShimException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
