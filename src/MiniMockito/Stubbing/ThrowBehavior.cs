namespace MiniMockito.Stubbing;

internal sealed class ThrowBehavior : StubBehavior
{
    private readonly Exception _exception;

    internal ThrowBehavior(Exception exception)
    {
        _exception = exception;
    }

    internal override object? Invoke(StubContext context, Type returnType)
    {
        throw _exception;
    }
}
