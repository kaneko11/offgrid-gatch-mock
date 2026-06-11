using System;
using System.Linq;
using System.Reflection;
using MiniMockito.Exceptions;

namespace MiniMockito.Proxy;

/// <summary>
/// Interface proxy backend built on <see cref="DispatchProxy"/>.  Used on modern .NET
/// (net8.0) where DispatchProxy is fully supported.
/// </summary>
internal sealed class DispatchProxyInterfaceProxyFactory : IInterfaceProxyFactory
{
    private static readonly MethodInfo CreateMethod = typeof(DispatchProxy)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method =>
            method.Name == nameof(DispatchProxy.Create)
            && method.IsGenericMethodDefinition
            && method.GetGenericArguments().Length == 2
            && method.GetParameters().Length == 0);

    public string Name => "DispatchProxy";

    public object Create(Type interfaceType, IMiniMockitoInterceptor interceptor)
    {
        var proxy = CreateMethod
            .MakeGenericMethod(interfaceType, typeof(MiniMockitoDispatchProxy))
            .Invoke(null, null);

        if (proxy is null)
        {
            throw new MockException($"Failed to create a DispatchProxy for '{interfaceType.FullName}'.");
        }

        ((MiniMockitoDispatchProxy)proxy).SetInterceptor(interceptor);
        return proxy;
    }
}
