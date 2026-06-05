namespace MiniMockito.Stubbing;

internal sealed class AnswerBehavior : StubBehavior
{
    private readonly Func<StubContext, object?> _answer;

    internal AnswerBehavior(Func<StubContext, object?> answer)
    {
        _answer = answer;
    }

    internal override object? Invoke(StubContext context, Type returnType)
    {
        var value = _answer(context);
        return ReturnValueAdapter.ToReturnValue(value, returnType);
    }
}
