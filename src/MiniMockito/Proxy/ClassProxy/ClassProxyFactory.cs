using MiniMockito.Core;
using MiniMockito;

namespace MiniMockito.Proxy.ClassProxy;

internal sealed class ClassProxyFactory
{
    private readonly ClassProxyTypeCache _typeCache = new();

    internal static ClassProxyFactory Default { get; } = new();

    internal T Create<T>(ClassMockOptions options)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(options);

        var targetType = typeof(T);
        ClassProxyValidation.ValidateTarget(targetType);
        var proxyType = _typeCache.GetOrCreate(targetType);
        var proxy = (T)(Activator.CreateInstance(proxyType)
            ?? throw ClassProxyValidation.CreateException(
                targetType,
                null,
                ClassProxyUnsupportedReason.NoParameterlessConstructor,
                hint: "The generated proxy could not be instantiated."));

        var state = MockRepository.Default.CreateState(targetType, options.Behavior, callsBase: options.CallsBase);
        MockRepository.Default.Register(proxy, state);

        return proxy;
    }
}
