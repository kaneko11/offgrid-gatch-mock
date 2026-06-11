using System.Reflection;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Proxy;
using MiniMockito.Proxy.ClassProxy;

namespace MiniMockito;

/// <summary>
/// Provides the public API for creating spies.
/// </summary>
public static class Spy
{
    /// <summary>
    /// Creates a class spy that calls base implementations for unstubbed public virtual method invocations.
    /// </summary>
    /// <remarks>
    /// Class spies use the same class proxy constraints as <c>Mock.Class&lt;T&gt;()</c>: the target must be a public
    /// non-sealed class with a public or protected parameterless constructor, and only public virtual methods are intercepted.
    /// </remarks>
    /// <typeparam name="T">The public non-sealed class type to spy.</typeparam>
    /// <returns>A generated class proxy instance.</returns>
    public static T Class<T>()
        where T : class
    {
        return ClassProxyFactory.Default.Create<T>(ClassMockOptions.CallBase);
    }

    /// <summary>
    /// Creates an interface spy that delegates unstubbed invocations to <paramref name="realInstance"/>.
    /// </summary>
    /// <remarks>Interface spies remain interface proxies. Class proxying is available through <c>Spy.Class&lt;T&gt;()</c>.</remarks>
    /// <typeparam name="T">The interface type to spy.</typeparam>
    /// <param name="realInstance">The real instance implementing <typeparamref name="T"/>.</param>
    /// <returns>A proxy implementing <typeparamref name="T"/>.</returns>
    public static T Of<T>(T realInstance)
    {
        var targetType = typeof(T);
        if (!targetType.IsInterface)
        {
            throw new UnsupportedMockTargetException(
                $"MiniMockito can only create interface spies in v1. Target type '{targetType.FullName}' is not an interface.");
        }

        ThrowHelper.ThrowIfNull(realInstance);

        if (!targetType.IsInstanceOfType(realInstance))
        {
            throw new UnsupportedMockTargetException(
                $"The real instance must implement '{targetType.FullName}'.");
        }

        var state = MockRepository.Default.CreateState(targetType, MockBehavior.Lenient, realInstance);
        var interceptor = new MockStateInterceptor(state);
        var proxy = InterfaceProxyFactorySelector.Resolve().Create(targetType, interceptor);
        MockRepository.Default.Register(proxy, state);

        return (T)proxy;
    }
}
