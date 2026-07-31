namespace MiniMockito.Shims.Experimental;

/// <summary>Configures a type-safe replacement for a void instance method.</summary>
public sealed class VoidMethodReplacementBuilder
{
    private readonly Shims _owner;
    private readonly MethodReplacementDescriptor _descriptor;
    private IShimArgumentMatcher[]? _matchers;

    internal VoidMethodReplacementBuilder(Shims owner, MethodReplacementDescriptor descriptor)
    {
        _owner = owner;
        _descriptor = descriptor;
    }

    /// <summary>Gets the exact reflected void method selected for replacement.</summary>
    public System.Reflection.MethodInfo Method => _descriptor.Method;

    /// <summary>Gets the interception backend selected from the reflected method.</summary>
    public MethodInterceptionBackend Backend => _descriptor.Backend;

    /// <summary>Constrains this rule with one matcher per declared method parameter.</summary>
    public VoidMethodReplacementBuilder WithArguments(params IShimArgumentMatcher[] matchers)
    {
        if (matchers is null)
            throw new ArgumentNullException(nameof(matchers));
        MethodReplacementValidator.ValidateMatchers(_descriptor.Method, matchers);
        _matchers = matchers;
        return this;
    }

    /// <summary>Suppresses the real void method and performs no callback.</summary>
    public void DoNothing()
        => _owner.RegisterTypedMethodReplacement(
            _descriptor,
            _matchers,
            (_, _) => null);

    /// <summary>Runs a callback and suppresses the real void method.</summary>
    public void Callback(Action<MethodReplacementContext> callback)
    {
        if (callback is null)
            throw new ArgumentNullException(nameof(callback));
        _owner.RegisterTypedMethodReplacement(
            _descriptor,
            _matchers,
            (receiver, args) =>
            {
                callback(new MethodReplacementContext(_descriptor.Method, receiver, args));
                return null;
            });
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
