using System;

namespace MiniMockito.Proxy;

/// <summary>
/// Creates an interface proxy object that forwards intercepted method calls to an
/// <see cref="IMiniMockitoInterceptor"/>.  Different backends implement the actual
/// proxy generation (DispatchProxy on modern .NET; RealProxy on .NET Framework, which
/// avoids the DispatchProxy code path that fails under <c>PlatformTarget=x86</c>).
/// </summary>
internal interface IInterfaceProxyFactory
{
    /// <summary>A short backend name for diagnostics (e.g. <c>"DispatchProxy"</c>, <c>"RealProxy"</c>).</summary>
    string Name { get; }

    /// <summary>
    /// Creates a proxy implementing <paramref name="interfaceType"/> whose calls are routed to
    /// <paramref name="interceptor"/>.
    /// </summary>
    object Create(Type interfaceType, IMiniMockitoInterceptor interceptor);
}
