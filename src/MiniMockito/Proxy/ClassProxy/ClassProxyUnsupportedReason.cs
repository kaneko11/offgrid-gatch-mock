namespace MiniMockito.Proxy.ClassProxy;

internal enum ClassProxyUnsupportedReason
{
    NotAClass,
    NotPublic,
    SealedClass,
    AbstractClass,
    OpenGenericType,
    NoParameterlessConstructor,
    NoSupportedVirtualMethods,
    StaticMethod,
    NonVirtualMethod,
    FinalMethod,
    PrivateMethod,
    NonPublicMethod,
    GenericMethod,
    RefOrOutParameter,
    ObjectMethod
}
