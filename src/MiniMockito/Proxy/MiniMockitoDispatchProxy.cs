using System.Reflection;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Utilities;

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
            var returnValue = stubRule is not null
                ? stubRule.Invoke(invocation, targetMethod.ReturnType)
                : DefaultValueProvider.GetDefaultValue(targetMethod.ReturnType);

            invocation.ReturnValue = returnValue;
            return returnValue;
        }
        catch (Exception exception)
        {
            invocation.Exception = exception;
            throw;
        }
    }
}
