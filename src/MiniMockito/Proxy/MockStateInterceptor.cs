using System.Reflection;
using System.Runtime.ExceptionServices;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Utilities;
using MiniMockito.Verification;

namespace MiniMockito.Proxy;

/// <summary>
/// The single, backend-agnostic implementation of the MiniMockito interface
/// invocation pipeline: record the invocation, resolve a stub rule, otherwise
/// delegate to the real instance (spy), throw on strict, or return a default value.
/// Shared by every proxy backend so behavior never diverges per backend.
/// </summary>
internal sealed class MockStateInterceptor : IMiniMockitoInterceptor
{
    private readonly MockState _state;

    internal MockStateInterceptor(MockState state)
    {
        _state = state;
    }

    public object? Invoke(MethodInfo method, object?[]? arguments)
    {
        var invocation = _state.RecordInvocation(method, arguments);

        try
        {
            var stubRule = _state.FindStubRule(method, arguments);
            object? returnValue;
            if (stubRule is not null)
            {
                returnValue = stubRule.Invoke(invocation, method.ReturnType);
            }
            else if (_state.RealInstance is not null)
            {
                returnValue = InvokeRealInstance(_state.RealInstance, method, arguments);
            }
            else if (_state.Behavior == global::MiniMockito.MockBehavior.Strict)
            {
                throw new MockException(StrictMockMessageFormatter.Format(_state, method, arguments));
            }
            else
            {
                returnValue = DefaultValueProvider.GetDefaultValue(method.ReturnType);
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
