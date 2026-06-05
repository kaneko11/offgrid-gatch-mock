using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Stubbing;
using MiniMockito.Proxy;
using MiniMockito.Verification;
using System.Linq.Expressions;
using System.Reflection;

namespace MiniMockito;

public static class Mock
{
    public static T Of<T>()
    {
        return Of<T>(MockBehavior.Lenient);
    }

    public static T Of<T>(MockBehavior behavior)
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

        var state = MockRepository.Default.CreateState(targetType, behavior);
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

    public static ArgumentCaptor<T> Capture<T>()
    {
        return new ArgumentCaptor<T>();
    }

    public static void Verify(Expression<Action> invocation)
    {
        Verify(invocation, Times.Once());
    }

    public static void Verify(Expression<Action> invocation, Times times)
    {
        ArgumentNullException.ThrowIfNull(times);
        var setup = VerificationSetupFactory.Create(invocation.Body);
        VerificationEngine.Verify(setup, times);
    }

    public static void Verify<TResult>(Expression<Func<TResult>> invocation)
    {
        Verify(invocation, Times.Once());
    }

    public static void Verify<TResult>(Expression<Func<TResult>> invocation, Times times)
    {
        ArgumentNullException.ThrowIfNull(times);
        var setup = VerificationSetupFactory.Create(invocation.Body);
        VerificationEngine.Verify(setup, times);
    }

    public static void VerifyNoInteractions(object mock)
    {
        var state = MockRepository.Default.GetState(mock);
        VerificationEngine.VerifyNoInteractions(state);
    }

    public static void VerifyNoMoreInteractions(object mock)
    {
        var state = MockRepository.Default.GetState(mock);
        VerificationEngine.VerifyNoMoreInteractions(state);
    }
}
