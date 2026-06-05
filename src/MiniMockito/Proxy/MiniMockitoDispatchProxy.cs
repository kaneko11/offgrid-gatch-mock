using System.Reflection;
using System.Runtime.ExceptionServices;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Utilities;
using MiniMockito.Verification;

namespace MiniMockito.Proxy;

internal class MiniMockitoDispatchProxy : DispatchProxy, IMockProxy
{
    private MockState? _state;

    public void Configure(MockState state)
    {
        _state = state;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (_state is null)
        {
            throw new MockException("The mock proxy has not been configured.");
        }

        if (targetMethod is null)
        {
            throw new MockException("The invoked method could not be resolved.");
        }

        var invocation = _state.RecordInvocation(targetMethod, args);

        try
        {
            var stubRule = _state.FindStubRule(targetMethod, args);
            object? returnValue;
            if (stubRule is not null)
            {
                returnValue = stubRule.Invoke(invocation, targetMethod.ReturnType);
            }
            else if (_state.RealInstance is not null)
            {
                returnValue = InvokeRealInstance(_state.RealInstance, targetMethod, args);
            }
            else if (_state.Behavior == global::MiniMockito.MockBehavior.Strict)
            {
                throw new MockException(StrictMockMessageFormatter.Format(_state, targetMethod, args));
            }
            else
            {
                returnValue = DefaultValueProvider.GetDefaultValue(targetMethod.ReturnType);
            }

            invocation.ReturnValue = returnValue;
            return returnValue;
        }
        catch (Exception exception)
        {
            invocation.Exception = exception;
            throw;
        }
    }

    private static object? InvokeRealInstance(object realInstance, MethodInfo targetMethod, object?[]? args)
    {
        try
        {
            return targetMethod.Invoke(realInstance, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
