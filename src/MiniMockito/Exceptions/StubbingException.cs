namespace MiniMockito.Exceptions;

/// <summary>
/// Exception thrown for invalid stubbing configuration.
/// </summary>
public class StubbingException : MockException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StubbingException"/> class.
    /// </summary>
    public StubbingException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StubbingException"/> class with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public StubbingException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StubbingException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public StubbingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
