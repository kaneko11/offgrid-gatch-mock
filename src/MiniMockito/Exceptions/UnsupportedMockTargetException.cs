namespace MiniMockito.Exceptions;

/// <summary>
/// Exception thrown when a target cannot be mocked or spied by MiniMockito.
/// </summary>
public class UnsupportedMockTargetException : MockException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedMockTargetException"/> class.
    /// </summary>
    public UnsupportedMockTargetException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedMockTargetException"/> class with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public UnsupportedMockTargetException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedMockTargetException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public UnsupportedMockTargetException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
