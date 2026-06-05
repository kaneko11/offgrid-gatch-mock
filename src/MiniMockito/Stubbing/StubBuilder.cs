using MiniMockito.Core;
using MiniMockito.Exceptions;

namespace MiniMockito.Stubbing;

public class StubBuilder
{
    private readonly MockState _state;
    private readonly InvocationMatcher _matcher;

    internal StubBuilder(MockState state, InvocationMatcher matcher, Type returnType)
    {
        _state = state;
        _matcher = matcher;
    }

    public void ThenReturn()
    {
        AddRule(new ReturnBehavior((object?)null));
    }

    public void ThenThrow(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        AddRule(new ThrowBehavior(exception));
    }

    public void ThenAnswer(Func<StubContext, object?> answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        AddRule(new AnswerBehavior(answer));
    }

    private protected void AddRule(StubBehavior behavior)
    {
        _state.AddStubRule(new StubRule(_matcher, behavior));
    }
}

public sealed class StubBuilder<TResult> : StubBuilder
{
    internal StubBuilder(MockState state, InvocationMatcher matcher, Type returnType)
        : base(state, matcher, returnType)
    {
    }

    public void ThenReturn(object? value)
    {
        AddRule(new ReturnBehavior(value));
    }

    public void ThenReturnSequence(params object?[] values)
    {
        if (values.Length == 0)
        {
            throw new StubbingException("ThenReturnSequence requires at least one value.");
        }

        AddRule(new ReturnBehavior(values));
    }
}
