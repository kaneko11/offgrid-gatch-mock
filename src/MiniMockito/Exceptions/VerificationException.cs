namespace MiniMockito.Exceptions;

/// <summary>
/// Exception thrown when verification fails.
/// </summary>
public class VerificationException : MockException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VerificationException"/> class.
    /// </summary>
    public VerificationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VerificationException"/> class with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public VerificationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VerificationException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public VerificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
