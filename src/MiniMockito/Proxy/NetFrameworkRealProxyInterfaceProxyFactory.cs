#if NETFRAMEWORK
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;

namespace MiniMockito.Proxy;

/// <summary>
/// Interface proxy backend built on <see cref="RealProxy"/>, used on .NET Framework.
///
/// <para>On .NET Framework 4.8 with <c>PlatformTarget=x86</c>, <see cref="DispatchProxy"/>
/// fails to build its dynamic proxy type (<c>TypeLoadException: access is denied:
/// 'MiniMockito.Proxy.MiniMockitoDispatchProxy'</c>).  <see cref="RealProxy"/> is a
/// classic remoting transparent-proxy mechanism that does not hit that code path, so it
/// works under x86.  The public MiniMockito API is unchanged; only the proxy generation
/// differs.</para>
/// </summary>
internal sealed class NetFrameworkRealProxyInterfaceProxyFactory : IInterfaceProxyFactory
{
    public string Name => "RealProxy";

    public object Create(Type interfaceType, IMiniMockitoInterceptor interceptor)
    {
        var realProxy = new MiniMockitoRealProxy(interfaceType, interceptor);
        return realProxy.GetTransparentProxy();
    }

    private sealed class MiniMockitoRealProxy : RealProxy
    {
        private readonly Type _interfaceType;
        private readonly IMiniMockitoInterceptor _interceptor;

        public MiniMockitoRealProxy(Type interfaceType, IMiniMockitoInterceptor interceptor)
            : base(interfaceType)
        {
            _interfaceType = interfaceType;
            _interceptor = interceptor;
        }

        public override IMessage Invoke(IMessage msg)
        {
            var call = (IMethodCallMessage)msg;
            var method = call.MethodBase as MethodInfo;

            // System.Object methods (ToString/Equals/GetHashCode/GetType) are not part of the
            // mocked interface contract; handle them here so they are not recorded as
            // invocations and do not trip strict-mode (mirrors DispatchProxy, which only
            // intercepts interface methods).
            if (method is null || method.DeclaringType == typeof(object))
            {
                return HandleObjectMethod(call, method);
            }

            try
            {
                var result = _interceptor.Invoke(method, call.Args);
                return new ReturnMessage(result, call.Args, call.ArgCount, call.LogicalCallContext, call);
            }
            catch (Exception exception)
            {
                return new ReturnMessage(exception, call);
            }
        }

        private IMessage HandleObjectMethod(IMethodCallMessage call, MethodInfo? method)
        {
            object? result;
            switch (method?.Name)
            {
                case "GetHashCode":
                    result = RuntimeHelpers.GetHashCode(GetTransparentProxy());
                    break;
                case "Equals":
                    result = ReferenceEquals(call.Args[0], GetTransparentProxy());
                    break;
                case "ToString":
                    result = _interfaceType.FullName + " (MiniMockito RealProxy)";
                    break;
                case "GetType":
                    result = _interfaceType;
                    break;
                default:
                    result = null;
                    break;
            }

            return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
        }
    }
}
#endif
