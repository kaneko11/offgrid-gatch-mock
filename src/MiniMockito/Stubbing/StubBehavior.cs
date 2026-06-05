namespace MiniMockito.Stubbing;

internal abstract class StubBehavior
{
    internal abstract object? Invoke(StubContext context, Type returnType);
}
