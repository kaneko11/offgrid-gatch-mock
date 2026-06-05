namespace MiniMockito.Exceptions;

public class UnsupportedMockTargetException : MockException
{
    public UnsupportedMockTargetException()
    {
    }

    public UnsupportedMockTargetException(string message)
        : base(message)
    {
    }

    public UnsupportedMockTargetException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
