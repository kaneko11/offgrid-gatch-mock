using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Proxy;
using System.Reflection;

namespace MiniMockito;

public static class Mock
{
    public static T Of<T>()
    {
        var targetType = typeof(T);
        if (!targetType.IsInterface)
        {
            throw new UnsupportedMockTargetException(
                $"MiniMockito can only mock interfaces in v1. Target type '{targetType.FullName}' is not an interface.");
        }

        var proxy = DispatchProxy.Create<T, MiniMockitoDispatchProxy>();
        if (proxy is null)
        {
            throw new MockException($"Failed to create a proxy for '{targetType.FullName}'.");
        }

        var state = MockRepository.Default.CreateState(targetType, MockBehavior.Lenient);
        ((IMockProxy)(object)proxy).Configure(state);
        MockRepository.Default.Register((object)proxy, state);

        return proxy;
    }
}
