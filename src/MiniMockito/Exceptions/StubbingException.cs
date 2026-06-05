namespace MiniMockito.Exceptions;

public class StubbingException : MockException
{
    public StubbingException()
    {
    }

    public StubbingException(string message)
        : base(message)
    {
    }

    public StubbingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
