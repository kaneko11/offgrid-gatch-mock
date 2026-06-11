namespace MiniMockito.Proxy;

/// <summary>
/// Selects the interface proxy backend for the current runtime.
///
/// <para>On modern .NET (net8.0) the <see cref="DispatchProxyInterfaceProxyFactory"/> is used.
/// On .NET Framework the <c>NetFrameworkRealProxyInterfaceProxyFactory</c> is used so that
/// interface mocks work even under <c>PlatformTarget=x86</c>, where DispatchProxy throws a
/// <c>TypeLoadException</c> while building its dynamic proxy type.</para>
/// </summary>
internal static class InterfaceProxyFactorySelector
{
    /// <summary>Resolves the proxy backend for the current target framework.</summary>
    internal static IInterfaceProxyFactory Resolve()
    {
#if NETFRAMEWORK
        return new NetFrameworkRealProxyInterfaceProxyFactory();
#else
        return new DispatchProxyInterfaceProxyFactory();
#endif
    }
}
