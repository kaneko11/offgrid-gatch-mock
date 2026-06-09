namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Fluent builder for configuring a static method shim that returns <typeparamref name="TResult"/>.
/// Obtained via <see cref="Shim.Static{TResult}(string,string,Type[])"/>.
/// </summary>
public sealed class StaticShimBuilder<TResult>
{
    private readonly StaticMethodKey _key;
    private readonly ShimContext _context;
    private IReadOnlyList<IShimArgumentMatcher>? _matchers;

    internal StaticShimBuilder(StaticMethodKey key, ShimContext context)
    {
        _key = key;
        _context = context;
    }

    /// <summary>
    /// Restricts matching to calls whose arguments satisfy the given matchers.
    /// Omitting this call (or calling with no arguments) registers a catch-all rule.
    /// </summary>
    public StaticShimBuilder<TResult> WithArguments(params IShimArgumentMatcher[] matchers)
    {
        _matchers = matchers.Length == 0 ? null : [.. matchers];
        return this;
    }

    /// <summary>Registers a constant return value.</summary>
    public void Returns(TResult value)
    {
        _context.EnsureActive();
        _context.StaticRegistry.RegisterRule(_key, _ => value, _matchers);
    }

    /// <summary>Registers a factory that produces the return value on each call.</summary>
    public void Returns(Func<TResult> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _context.EnsureActive();
        _context.StaticRegistry.RegisterRule(_key, _ => factory(), _matchers);
    }

    /// <summary>Registers a factory that receives the boxed arguments and returns the shimmed value.</summary>
    public void Returns(Func<object?[], TResult> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _context.EnsureActive();
        _context.StaticRegistry.RegisterRule(_key, args => factory(args), _matchers);
    }

    /// <summary>Registers a rule that throws <paramref name="exception"/> when the method is called.</summary>
    public void Throws(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _context.EnsureActive();
        _context.StaticRegistry.RegisterThrowRule(_key, exception, _matchers);
    }

    /// <summary>Registers a rule that throws a new <typeparamref name="TException"/>.</summary>
    public void Throws<TException>() where TException : Exception, new()
        => Throws(new TException());
}

/// <summary>
/// Fluent builder for configuring a shim for a void static method.
/// Obtained via <see cref="Shim.Static(string,string,Type[])"/>.
/// </summary>
public sealed class StaticShimBuilder
{
    private readonly StaticMethodKey _key;
    private readonly ShimContext _context;
    private IReadOnlyList<IShimArgumentMatcher>? _matchers;

    internal StaticShimBuilder(StaticMethodKey key, ShimContext context)
    {
        _key = key;
        _context = context;
    }

    /// <summary>
    /// Restricts matching to calls whose arguments satisfy the given matchers.
    /// Omitting this call registers a catch-all rule.
    /// </summary>
    public StaticShimBuilder WithArguments(params IShimArgumentMatcher[] matchers)
    {
        _matchers = matchers.Length == 0 ? null : [.. matchers];
        return this;
    }

    /// <summary>Registers a no-op rule (intercept but do nothing).</summary>
    public void DoNothing()
    {
        _context.EnsureActive();
        _context.StaticRegistry.RegisterVoidRule(_key, null, _matchers);
    }

    /// <summary>Registers a callback that is invoked with the boxed arguments.</summary>
    public void Callback(Action<object?[]> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _context.EnsureActive();
        _context.StaticRegistry.RegisterVoidRule(_key, action, _matchers);
    }

    /// <summary>Registers a rule that throws <paramref name="exception"/> when the method is called.</summary>
    public void Throws(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _context.EnsureActive();
        _context.StaticRegistry.RegisterThrowRule(_key, exception, _matchers);
    }

    /// <summary>Registers a rule that throws a new <typeparamref name="TException"/>.</summary>
    public void Throws<TException>() where TException : Exception, new()
        => Throws(new TException());
}
