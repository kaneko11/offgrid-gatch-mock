using System;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Fluent builder for configuring a <c>new T()</c> shim through the high-level <see cref="Shims"/>
/// facade.  Obtained via <see cref="Shims.New{TTarget}"/>.
/// </summary>
/// <typeparam name="TTarget">The interception target type, as declared with
/// <see cref="Shims.WithNew{TTarget}"/>.</typeparam>
/// <remarks>
/// The replacement instance passed to <see cref="Returns(object)"/> must be a rewritten-identity
/// instance — create one with <see cref="Shims.CreateFake{TTarget}(object[])"/>.  A plain
/// default-context instance (or a proxy of the original type) cannot be returned by the rewritten
/// constructor call site because of the load-context type-identity boundary.
/// </remarks>
public sealed class ShimsNewBuilder<TTarget> where TTarget : class
{
    private readonly Shims _owner;
    private IShimArgumentMatcher[]? _matchers;

    internal ShimsNewBuilder(Shims owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Constrains this shim to constructor calls whose arguments satisfy the supplied matchers.
    /// Omitting this call registers a catch-all shim that matches any constructor argument list.
    /// </summary>
    public ShimsNewBuilder<TTarget> WithArguments(params IShimArgumentMatcher[] matchers)
    {
        if (matchers == null) throw new ArgumentNullException(nameof(matchers));
        _matchers = matchers;
        return this;
    }

    /// <summary>
    /// Registers the replacement instance returned for matching <c>new TTarget(...)</c> call sites.
    /// </summary>
    /// <param name="fakeInstance">
    /// A rewritten-identity instance, typically produced by
    /// <see cref="Shims.CreateFake{TTarget}(object[])"/>.
    /// </param>
    public void Returns(object fakeInstance)
    {
        if (fakeInstance == null) throw new ArgumentNullException(nameof(fakeInstance));

        if (_matchers == null || _matchers.Length == 0)
        {
            _owner.Harness.RegisterShim<TTarget>(fakeInstance);
        }
        else
        {
            _owner.Harness.RegisterShimWithMatchers<TTarget>(fakeInstance, _matchers);
        }
    }
}
