using System.Reflection;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Proxy;

namespace MiniMockito;

public static class Spy
{
    public static T Of<T>(T realInstance)
    {
        var targetType = typeof(T);
        if (!targetType.IsInterface)
        {
            throw new UnsupportedMockTargetException(
                $"MiniMockito can only create interface spies in v1. Target type '{targetType.FullName}' is not an interface.");
        }

        ArgumentNullException.ThrowIfNull(realInstance);

        if (!targetType.IsInstanceOfType(realInstance))
        {
            throw new UnsupportedMockTargetException(
                $"The real instance must implement '{targetType.FullName}'.");
        }

        var proxy = DispatchProxy.Create<T, MiniMockitoDispatchProxy>();
        if (proxy is null)
        {
            throw new MockException($"Failed to create a spy proxy for '{targetType.FullName}'.");
        }

        var state = MockRepository.Default.CreateState(targetType, MockBehavior.Lenient, realInstance);
        ((IMockProxy)(object)proxy).Configure(state);
        MockRepository.Default.Register((object)proxy, state);

        return proxy;
    }
}
