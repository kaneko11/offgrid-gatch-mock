using System.Reflection;

namespace MiniMockito.Proxy;

/// <summary>
/// Core invocation handler that a proxy backend forwards intercepted interface
/// method calls to.  Backends (DispatchProxy on .NET, RealProxy on .NET Framework x86)
/// only translate their native call representation into a
/// <see cref="MethodInfo"/> + argument array and delegate here; all recording,
/// stubbing, verification, default-value, and strict/lenient logic lives behind this
/// interface so it is implemented exactly once.
/// </summary>
internal interface IMiniMockitoInterceptor
{
    /// <summary>
    /// Handles an intercepted call and returns the value the proxy should return.
    /// </summary>
    /// <param name="method">The invoked interface method.</param>
    /// <param name="arguments">The call arguments, or <see langword="null"/> for parameterless calls.</param>
    object? Invoke(MethodInfo method, object?[]? arguments);
}
