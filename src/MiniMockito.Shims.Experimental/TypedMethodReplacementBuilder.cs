namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Configures a non-void instance-method replacement whose result is constrained by
/// <typeparamref name="TResult"/>.
/// </summary>
public sealed class TypedMethodReplacementBuilder<TResult>
{
    private readonly Shims _owner;
    private readonly MethodReplacementDescriptor _descriptor;
    private IShimArgumentMatcher[]? _matchers;

    internal TypedMethodReplacementBuilder(Shims owner, MethodReplacementDescriptor descriptor)
    {
        _owner = owner;
        _descriptor = descriptor;
    }

    /// <summary>Gets the exact reflected method selected for replacement.</summary>
    public System.Reflection.MethodInfo Method => _descriptor.Method;

    /// <summary>Gets the interception backend selected from the reflected method.</summary>
    public MethodInterceptionBackend Backend => _descriptor.Backend;

    /// <summary>Constrains this rule with one matcher per declared method parameter.</summary>
    public TypedMethodReplacementBuilder<TResult> WithArguments(params IShimArgumentMatcher[] matchers)
    {
        if (matchers is null)
            throw new ArgumentNullException(nameof(matchers));
        MethodReplacementValidator.ValidateMatchers(_descriptor.Method, matchers);
        _matchers = matchers;
        return this;
    }

    /// <summary>Returns a constant value of the exact compile-time result type.</summary>
    public void Returns(TResult value)
        => _owner.RegisterTypedMethodReplacement(
            _descriptor,
            _matchers,
            (_, _) => value);

    /// <summary>Computes a replacement value from the receiver and boxed arguments.</summary>
    public void Returns(Func<MethodReplacementContext, TResult> callback)
    {
        if (callback is null)
            throw new ArgumentNullException(nameof(callback));
        _owner.RegisterTypedMethodReplacement(
            _descriptor,
            _matchers,
            (receiver, args) => callback(
                new MethodReplacementContext(_descriptor.Method, receiver, args)));
    }

    /// <summary>Throws <paramref name="exception"/> when this rule matches.</summary>
    public void Throws(Exception exception)
    {
        if (exception is null)
            throw new ArgumentNullException(nameof(exception));
        _owner.RegisterTypedMethodReplacement(
            _descriptor,
            _matchers,
            (_, _) => throw exception);
    }
}
