namespace MiniMockito.Exceptions;

/// <summary>
/// Base exception type for MiniMockito failures.
/// </summary>
public class MockException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MockException"/> class.
    /// </summary>
    public MockException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockException"/> class with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public MockException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MockException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
