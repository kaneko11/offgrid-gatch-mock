namespace MiniMockito.Exceptions;

public class VerificationException : MockException
{
    public VerificationException()
    {
    }

    public VerificationException(string message)
        : base(message)
    {
    }

    public VerificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
