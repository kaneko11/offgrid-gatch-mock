namespace MiniMockito.Exceptions;

/// <summary>
/// Exception thrown when class proxy creation or invocation fails.
/// </summary>
public class ClassProxyException : MockException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClassProxyException"/> class.
    /// </summary>
    public ClassProxyException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassProxyException"/> class with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public ClassProxyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassProxyException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ClassProxyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
