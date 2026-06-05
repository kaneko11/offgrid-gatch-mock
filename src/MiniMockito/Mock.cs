using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Stubbing;
using MiniMockito.Proxy;
using System.Linq.Expressions;
using System.Reflection;

namespace MiniMockito;

public static class Mock
{
    public static T Of<T>()
    {
        var targetType = typeof(T);
        if (!targetType.IsInterface)
        {
            throw new UnsupportedMockTargetException(
                $"MiniMockito can only mock interfaces in v1. Target type '{targetType.FullName}' is not an interface.");
        }

        var proxy = DispatchProxy.Create<T, MiniMockitoDispatchProxy>();
        if (proxy is null)
        {
            throw new MockException($"Failed to create a proxy for '{targetType.FullName}'.");
        }

        var state = MockRepository.Default.CreateState(targetType, MockBehavior.Lenient);
        ((IMockProxy)(object)proxy).Configure(state);
        MockRepository.Default.Register((object)proxy, state);

        return proxy;
    }

    public static StubBuilder<TResult> When<TResult>(Expression<Func<TResult>> invocation)
    {
        var setup = InvocationSetupFactory.Create(invocation.Body);
        return new StubBuilder<TResult>(setup.State, setup.Matcher, setup.ReturnType);
    }

    public static StubBuilder When(Expression<Action> invocation)
    {
        var setup = InvocationSetupFactory.Create(invocation.Body);
        return new StubBuilder(setup.State, setup.Matcher, setup.ReturnType);
    }

    public static T Any<T>()
    {
        return default!;
    }

    public static T Eq<T>(T value)
    {
        return default!;
    }

    public static T Is<T>(Expression<Func<T, bool>> predicate)
    {
        return default!;
    }

    public static T? Null<T>()
    {
        return default;
    }

    public static T NotNull<T>()
    {
        return default!;
    }

    public static T InRange<T>(T min, T max)
        where T : IComparable<T>
    {
        return default!;
    }
}
