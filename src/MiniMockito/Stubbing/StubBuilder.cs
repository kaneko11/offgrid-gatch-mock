using MiniMockito.Core;
using MiniMockito.Exceptions;

namespace MiniMockito.Stubbing;

/// <summary>
/// Configures stub behavior for a void invocation.
/// </summary>
public class StubBuilder
{
    private readonly MockState _state;
    private readonly InvocationMatcher _matcher;

    internal StubBuilder(MockState state, InvocationMatcher matcher, Type returnType)
    {
        _state = state;
        _matcher = matcher;
    }

    /// <summary>
    /// Configures the void invocation to complete normally.
    /// </summary>
    public void ThenReturn()
    {
        AddRule(new ReturnBehavior((object?)null));
    }

    /// <summary>
    /// Configures the invocation to throw an exception.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    public void ThenThrow(Exception exception)
    {
        ThrowHelper.ThrowIfNull(exception);
        AddRule(new ThrowBehavior(exception));
    }

    /// <summary>
    /// Configures the invocation to answer dynamically.
    /// </summary>
    /// <param name="answer">The callback used to produce the return value or side effect.</param>
    public void ThenAnswer(Func<StubContext, object?> answer)
    {
        ThrowHelper.ThrowIfNull(answer);
        AddRule(new AnswerBehavior(answer));
    }

    private protected void AddRule(StubBehavior behavior)
    {
        _state.AddStubRule(new StubRule(_matcher, behavior));
    }
}

/// <summary>
/// Configures stub behavior for an invocation with a return value.
/// </summary>
/// <typeparam name="TResult">The invocation return type.</typeparam>
public sealed class StubBuilder<TResult> : StubBuilder
{
    internal StubBuilder(MockState state, InvocationMatcher matcher, Type returnType)
        : base(state, matcher, returnType)
    {
    }

    /// <summary>
    /// Configures the invocation to return a value.
    /// </summary>
    /// <param name="value">The configured return value, or the logical result for async return types.</param>
    public void ThenReturn(object? value)
    {
        AddRule(new ReturnBehavior(value));
    }

    /// <summary>
    /// Configures the invocation to return values in sequence, repeating the final value after the sequence is exhausted.
    /// </summary>
    /// <param name="values">The return values in order.</param>
    public void ThenReturnSequence(params object?[] values)
    {
        if (values is null)
        {
            throw new StubbingException("ThenReturnSequence requires a non-null values array.");
        }

        if (values.Length == 0)
        {
            throw new StubbingException("ThenReturnSequence requires at least one value.");
        }

        AddRule(new ReturnBehavior(values));
    }
}
