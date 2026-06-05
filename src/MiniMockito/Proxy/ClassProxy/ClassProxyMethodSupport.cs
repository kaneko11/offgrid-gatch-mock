using System.Reflection;

namespace MiniMockito.Proxy.ClassProxy;

internal sealed class ClassProxyMethodSupport
{
    internal ClassProxyMethodSupport(MethodInfo method, bool isSupported, ClassProxyUnsupportedReason? reason)
    {
        Method = method;
        IsSupported = isSupported;
        Reason = reason;
    }

    internal MethodInfo Method { get; }

    internal bool IsSupported { get; }

    internal ClassProxyUnsupportedReason? Reason { get; }

    internal string Describe()
    {
        var reason = Reason is null ? "supported" : Reason.ToString();
        return $"{Method.DeclaringType?.Name}.{Method.Name}({string.Join(", ", Method.GetParameters().Select(parameter => parameter.ParameterType.Name))}) - {reason}";
    }
}
