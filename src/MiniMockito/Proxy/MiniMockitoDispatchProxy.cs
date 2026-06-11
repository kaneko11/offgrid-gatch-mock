using System.Reflection;
using MiniMockito.Exceptions;

namespace MiniMockito.Proxy;

/// <summary>
/// <see cref="DispatchProxy"/>-based interface proxy backend.  Translates the DispatchProxy
/// callback into a call on the shared <see cref="IMiniMockitoInterceptor"/>; it holds no
/// mock logic of its own.
/// </summary>
internal class MiniMockitoDispatchProxy : DispatchProxy
{
    private IMiniMockitoInterceptor? _interceptor;

    internal void SetInterceptor(IMiniMockitoInterceptor interceptor)
    {
        _interceptor = interceptor;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (_interceptor is null)
        {
            throw new MockException("The mock proxy has not been configured.");
        }

        if (targetMethod is null)
        {
            throw new MockException("The invoked method could not be resolved.");
        }

        return _interceptor.Invoke(targetMethod, args);
    }
}
