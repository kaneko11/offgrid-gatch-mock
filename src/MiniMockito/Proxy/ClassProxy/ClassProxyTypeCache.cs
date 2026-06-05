using System.Collections.Concurrent;

namespace MiniMockito.Proxy.ClassProxy;

internal sealed class ClassProxyTypeCache
{
    private readonly ConcurrentDictionary<Type, Type> _types = new();
    private readonly ClassProxyBuilder _builder = new();

    internal Type GetOrCreate(Type targetType)
    {
        return _types.GetOrAdd(targetType, _builder.Build);
    }
}
