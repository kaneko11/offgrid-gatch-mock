using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Stubbing;
using MiniMockito.Proxy;
using MiniMockito.Proxy.ClassProxy;
using MiniMockito.Verification;
using System.Linq.Expressions;
using System.Reflection;

namespace MiniMockito;

/// <summary>
/// Provides the public mock, stubbing, matcher, verification, and captor API.
/// </summary>
public static class Mock
{
    /// <summary>
    /// Creates a lenient mock for the interface type <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>Use <c>Mock.Class&lt;T&gt;()</c> for public non-sealed class proxies.</remarks>
    /// <typeparam name="T">The interface type to mock.</typeparam>
    /// <returns>A proxy implementing <typeparamref name="T"/>.</returns>
    public static T Of<T>()
    {
        return Of<T>(MockBehavior.Lenient);
    }

    /// <summary>
    /// Creates a mock for the interface type <typeparamref name="T"/> with the specified behavior.
    /// </summary>
    /// <remarks>Only interfaces are supported by this API.</remarks>
    /// <typeparam name="T">The interface type to mock.</typeparam>
    /// <param name="behavior">The behavior to use for unstubbed invocations.</param>
    /// <returns>A proxy implementing <typeparamref name="T"/>.</returns>
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

    /// <summary>
    /// Creates a mock using the legacy <see cref="Core.MockBehavior"/> enum.
    /// </summary>
    /// <typeparam name="T">The interface type to mock.</typeparam>
    /// <param name="behavior">The legacy behavior value.</param>
    /// <returns>A proxy implementing <typeparamref name="T"/>.</returns>
    public static T Of<T>(Core.MockBehavior behavior)
    {
        return Of<T>(behavior == Core.MockBehavior.Strict ? MockBehavior.Strict : MockBehavior.Lenient);
    }

    /// <summary>
    /// Creates a lenient class mock for a public non-sealed class with a parameterless constructor.
    /// </summary>
    /// <remarks>
    /// Class proxies intercept public virtual methods only. Sealed classes, static methods,
    /// non-virtual methods, private methods, constructors, and direct <c>new</c> interception are not supported.
    /// </remarks>
    /// <typeparam name="T">The class type to mock.</typeparam>
    /// <returns>A generated class proxy instance.</returns>
    public static T Class<T>()
        where T : class
    {
        return Class<T>(MockBehavior.Lenient);
    }

    /// <summary>
    /// Creates a class mock for a public non-sealed class with a parameterless constructor.
    /// </summary>
    /// <remarks>
    /// Class proxies intercept public virtual methods only. Sealed classes, static methods,
    /// non-virtual methods, private methods, constructors, and direct <c>new</c> interception are not supported.
    /// </remarks>
    /// <typeparam name="T">The class type to mock.</typeparam>
    /// <param name="behavior">The behavior to use for unstubbed virtual method invocations.</param>
    /// <returns>A generated class proxy instance.</returns>
    public static T Class<T>(MockBehavior behavior)
        where T : class
    {
        return Class<T>(new ClassMockOptions(behavior));
    }

    /// <summary>
    /// Creates a class mock using class proxy options.
    /// </summary>
    /// <remarks>
    /// Use <see cref="ClassMockOptions.CallBase"/> to create a partial mock that calls base implementations
    /// for unstubbed public virtual method invocations.
    /// </remarks>
    /// <typeparam name="T">The class type to mock.</typeparam>
    /// <param name="options">The class proxy options.</param>
    /// <returns>A generated class proxy instance.</returns>
    public static T Class<T>(ClassMockOptions options)
        where T : class
    {
        return ClassProxyFactory.Default.Create<T>(options);
    }

    /// <summary>
    /// Creates a class mock using the legacy <see cref="Core.MockBehavior"/> enum.
    /// </summary>
    /// <typeparam name="T">The class type to mock.</typeparam>
    /// <param name="behavior">The legacy behavior value.</param>
    /// <returns>A generated class proxy instance.</returns>
    public static T Class<T>(Core.MockBehavior behavior)
        where T : class
    {
        return Class<T>(behavior == Core.MockBehavior.Strict ? MockBehavior.Strict : MockBehavior.Lenient);
    }

    /// <summary>
    /// Starts stubbing a method with a non-void return value.
    /// </summary>
    /// <typeparam name="TResult">The method return type.</typeparam>
    /// <param name="invocation">The method call expression to stub.</param>
    /// <returns>A builder used to configure the stub behavior.</returns>
    public static StubBuilder<TResult> When<TResult>(Expression<Func<TResult>> invocation)
    {
        ThrowHelper.ThrowIfNull(invocation);
        var setup = InvocationSetupFactory.Create(invocation.Body);
        return new StubBuilder<TResult>(setup.State, setup.Matcher, setup.ReturnType);
    }

    /// <summary>
    /// Starts stubbing a void method.
    /// </summary>
    /// <param name="invocation">The method call expression to stub.</param>
    /// <returns>A builder used to configure the stub behavior.</returns>
    public static StubBuilder When(Expression<Action> invocation)
    {
        ThrowHelper.ThrowIfNull(invocation);
        var setup = InvocationSetupFactory.Create(invocation.Body);
        return new StubBuilder(setup.State, setup.Matcher, setup.ReturnType);
    }

    /// <summary>
    /// Matches any argument of type <typeparamref name="T"/> in a stubbing or verification expression.
    /// </summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <returns>A placeholder value for expression capture.</returns>
    public static T Any<T>()
    {
        return default!;
    }

    /// <summary>
    /// Matches an argument equal to <paramref name="value"/>.
    /// </summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="value">The expected value.</param>
    /// <returns>A placeholder value for expression capture.</returns>
    public static T Eq<T>(T value)
    {
        return default!;
    }

    /// <summary>
    /// Matches an argument that satisfies <paramref name="predicate"/>.
    /// </summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="predicate">The predicate to evaluate against actual arguments.</param>
    /// <returns>A placeholder value for expression capture.</returns>
    public static T Is<T>(Expression<Func<T, bool>> predicate)
    {
        return default!;
    }

    /// <summary>
    /// Matches a null argument.
    /// </summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <returns>A null placeholder value for expression capture.</returns>
    public static T? Null<T>()
    {
        return default;
    }

    /// <summary>
    /// Matches a non-null argument.
    /// </summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <returns>A placeholder value for expression capture.</returns>
    public static T NotNull<T>()
    {
        return default!;
    }

    /// <summary>
    /// Matches an argument in the inclusive range from <paramref name="min"/> to <paramref name="max"/>.
    /// </summary>
    /// <typeparam name="T">The comparable argument type.</typeparam>
    /// <param name="min">The inclusive minimum.</param>
    /// <param name="max">The inclusive maximum.</param>
    /// <returns>A placeholder value for expression capture.</returns>
    public static T InRange<T>(T min, T max)
        where T : IComparable<T>
    {
        return default!;
    }

    /// <summary>
    /// Creates an argument captor for use in verification expressions.
    /// </summary>
    /// <typeparam name="T">The captured argument type.</typeparam>
    /// <returns>A captor for arguments of type <typeparamref name="T"/>.</returns>
    public static ArgumentCaptor<T> Capture<T>()
    {
        return new ArgumentCaptor<T>();
    }

    /// <summary>
    /// Verifies that a void method was invoked once.
    /// </summary>
    /// <param name="invocation">The expected invocation expression.</param>
    public static void Verify(Expression<Action> invocation)
    {
        Verify(invocation, Times.Once());
    }

    /// <summary>
    /// Verifies that a void method was invoked according to <paramref name="times"/>.
    /// </summary>
    /// <param name="invocation">The expected invocation expression.</param>
    /// <param name="times">The expected invocation count rule.</param>
    public static void Verify(Expression<Action> invocation, Times times)
    {
        ThrowHelper.ThrowIfNull(invocation);
        ThrowHelper.ThrowIfNull(times);
        var setup = VerificationSetupFactory.Create(invocation.Body);
        VerificationEngine.Verify(setup, times);
    }

    /// <summary>
    /// Verifies that a non-void method was invoked once.
    /// </summary>
    /// <typeparam name="TResult">The method return type.</typeparam>
    /// <param name="invocation">The expected invocation expression.</param>
    public static void Verify<TResult>(Expression<Func<TResult>> invocation)
    {
        Verify(invocation, Times.Once());
    }

    /// <summary>
    /// Verifies that a non-void method was invoked according to <paramref name="times"/>.
    /// </summary>
    /// <typeparam name="TResult">The method return type.</typeparam>
    /// <param name="invocation">The expected invocation expression.</param>
    /// <param name="times">The expected invocation count rule.</param>
    public static void Verify<TResult>(Expression<Func<TResult>> invocation, Times times)
    {
        ThrowHelper.ThrowIfNull(invocation);
        ThrowHelper.ThrowIfNull(times);
        var setup = VerificationSetupFactory.Create(invocation.Body);
        VerificationEngine.Verify(setup, times);
    }

    /// <summary>
    /// Verifies that the mock has no recorded invocations.
    /// </summary>
    /// <param name="mock">The mock to inspect.</param>
    public static void VerifyNoInteractions(object mock)
    {
        var state = MockRepository.Default.GetState(mock);
        VerificationEngine.VerifyNoInteractions(state);
    }

    /// <summary>
    /// Verifies that all recorded invocations on the mock have already been verified.
    /// </summary>
    /// <param name="mock">The mock to inspect.</param>
    public static void VerifyNoMoreInteractions(object mock)
    {
        var state = MockRepository.Default.GetState(mock);
        VerificationEngine.VerifyNoMoreInteractions(state);
    }

    /// <summary>
    /// Creates an in-order verifier over one or more mocks or spies.
    /// </summary>
    /// <param name="mocks">The mocks or spies whose invocations should be ordered.</param>
    /// <returns>An in-order verification context.</returns>
    public static InOrderContext InOrder(params object[] mocks)
    {
        return new InOrderContext(mocks);
    }
}
