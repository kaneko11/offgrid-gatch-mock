using System.Linq.Expressions;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Verification;

namespace MiniMockito;

/// <summary>
/// Verifies invocation order across one or more mocks or spies.
/// </summary>
public sealed class InOrderContext
{
    private readonly IReadOnlyList<MockState> _states;
    private long _lastVerifiedSequenceNumber;

    internal InOrderContext(IEnumerable<object> mocks)
    {
        ArgumentNullException.ThrowIfNull(mocks);

        _states = mocks
            .Select(MockRepository.Default.GetState)
            .GroupBy(state => state.MockId)
            .Select(group => group.First())
            .ToArray();

        if (_states.Count == 0)
        {
            throw new ArgumentException("InOrder requires at least one mock.", nameof(mocks));
        }
    }

    /// <summary>
    /// Verifies that a void invocation happened after the previously verified in-order invocation.
    /// </summary>
    /// <param name="invocation">The expected invocation expression.</param>
    public void Verify(Expression<Action> invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var setup = VerificationSetupFactory.Create(invocation.Body);
        Verify(setup);
    }

    /// <summary>
    /// Verifies that a non-void invocation happened after the previously verified in-order invocation.
    /// </summary>
    /// <typeparam name="TResult">The invocation return type.</typeparam>
    /// <param name="invocation">The expected invocation expression.</param>
    public void Verify<TResult>(Expression<Func<TResult>> invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var setup = VerificationSetupFactory.Create(invocation.Body);
        Verify(setup);
    }

    private void Verify(VerificationSetup setup)
    {
        if (!_states.Any(state => state.MockId == setup.State.MockId))
        {
            throw new VerificationException(
                $"Expected order:{Environment.NewLine}  {setup.Matcher.Describe()}{Environment.NewLine}" +
                $"Actual order:{Environment.NewLine}{FormatActualOrder()}{Environment.NewLine}" +
                "The verified mock was not included in this InOrder context.");
        }

        var matchingInvocation = setup.State
            .FindInvocations(setup.Matcher)
            .Where(invocation => invocation.SequenceNumber > _lastVerifiedSequenceNumber)
            .OrderBy(invocation => invocation.SequenceNumber)
            .FirstOrDefault();

        if (matchingInvocation is null)
        {
            throw new VerificationException(
                $"Expected order:{Environment.NewLine}" +
                $"  after sequence {_lastVerifiedSequenceNumber}: {setup.Matcher.Describe()}{Environment.NewLine}" +
                $"Actual order:{Environment.NewLine}{FormatActualOrder()}");
        }

        setup.State.MarkVerified([matchingInvocation]);
        setup.Matcher.CaptureArguments(matchingInvocation.Arguments);
        _lastVerifiedSequenceNumber = matchingInvocation.SequenceNumber;
    }

    private string FormatActualOrder()
    {
        var invocations = _states
            .SelectMany(state => state.Invocations)
            .OrderBy(invocation => invocation.SequenceNumber)
            .ToArray();

        return InvocationFormatter.FormatMany(invocations);
    }
}
