namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Entry point intended for rewritten constructor call sites.
/// </summary>
/// <remarks>
/// <b>Experimental.</b> This class is generated into rewritten assemblies by the build-time weaver.
/// <para>
/// Dispatch diagnostics are recorded in <see cref="ShimContext.LastDispatchDiagnostics"/> after
/// every call to <see cref="New{T}"/> and <see cref="NewWithArgs{T}"/>.
/// </para>
/// </remarks>
public static class ShimDispatcher
{
    /// <summary>
    /// Creates a new instance of <typeparamref name="T"/> through the active shim rule, or by using a public parameterless constructor.
    /// </summary>
    /// <remarks>
    /// Rules registered without <c>WithArguments</c> (catch-all rules) and rules registered with an empty
    /// <c>WithArguments()</c> call match parameterless constructor call sites.
    /// Rules that require specific arguments do not match parameterless calls.
    /// Dispatch diagnostics are written to <see cref="ShimContext.LastDispatchDiagnostics"/>.
    /// </remarks>
    public static T New<T>()
    {
        var targetType = typeof(T);
        var context = ShimContext.Current;

        if (context is { IsDisposed: false })
        {
            bool found = context.Registry.TryFindNewRuleWithArgsDiagnostics(
                targetType, [], out var rule, out var diag);
            context.LastDispatchDiagnostics = diag;
            if (found && rule is not null)
                return (T)rule.CreateInstance()!;
        }

        return CreateRealInstance<T>(targetType);
    }

    /// <summary>
    /// Creates a new instance of <typeparamref name="T"/> through the best matching active shim rule,
    /// or by using the matching public constructor as a fallback.
    /// </summary>
    /// <remarks>
    /// Value-type arguments must be boxed by the caller (the rewritten wrapper method handles this automatically).
    /// Rules are evaluated from most recently registered to least recently registered; the first rule
    /// whose argument matchers all pass is selected.  A catch-all rule (no <c>WithArguments</c>) always matches.
    /// When no rule matches, the real constructor is invoked via <see cref="Activator.CreateInstance(Type, object?[])"/>.
    /// Dispatch diagnostics (including tried rules and matcher results) are written to
    /// <see cref="ShimContext.LastDispatchDiagnostics"/>.
    /// </remarks>
    /// <param name="args">Boxed constructor arguments in declaration order.</param>
    public static T NewWithArgs<T>(object?[] args)
    {
        ThrowHelper.ThrowIfNull(args);
        var targetType = typeof(T);
        var context = ShimContext.Current;

        if (context is { IsDisposed: false })
        {
            bool found = context.Registry.TryFindNewRuleWithArgsDiagnostics(
                targetType, args, out var rule, out var diag);
            context.LastDispatchDiagnostics = diag;
            if (found && rule is not null)
                return (T)rule.CreateInstanceWithArgs(args)!;
        }

        return CreateRealInstanceWithArgs<T>(targetType, args);
    }

    /// <summary>
    /// Entry point for rewritten <b>instance method</b> call sites (Phase 25).  Looks up a registered
    /// method shim for <paramref name="methodKey"/> (<c>DeclaringTypeFullName::MethodName</c>) in the
    /// active <see cref="ShimContext"/>.  When found, the shim is invoked and its result returned via
    /// <paramref name="result"/>; the generated wrapper casts it to the call's (possibly substituted)
    /// return type.  When not found, returns <see langword="false"/> and the wrapper invokes the real method.
    /// </summary>
    /// <param name="methodKey">The method key (<c>DeclaringTypeFullName::MethodName</c>).</param>
    /// <param name="receiver">The call receiver (boxed for value types), or <see langword="null"/>.</param>
    /// <param name="args">The boxed call arguments in declaration order.</param>
    /// <param name="result">The shim's return value when a shim is found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a method shim handled the call.</returns>
    public static bool TryInvokeMethod(string methodKey, object? receiver, object?[] args, out object? result)
    {
        ThrowHelper.ThrowIfNull(methodKey);
        ThrowHelper.ThrowIfNull(args);

        var context = ShimContext.Current;
        if (context is { IsDisposed: false }
            && context.MethodRegistry.TryGet(methodKey, out var shim)
            && shim is not null)
        {
            result = shim(receiver, args);
            context.LastMethodShimResolved = true;
            return true;
        }

        if (context is { IsDisposed: false })
        {
            context.LastMethodShimResolved = false;
        }

        result = null;
        return false;
    }

    private static T CreateRealInstance<T>(Type targetType)
    {
        if (!targetType.IsClass)
        {
            throw CreateFallbackException(targetType, "TargetTypeIsNotAClass", "ShimDispatcher.New<T>() fallback supports reference types with public parameterless constructors.");
        }

        if (targetType.ContainsGenericParameters)
        {
            throw CreateFallbackException(targetType, "OpenGenericTypeNotSupported", "Use a closed non-generic class.");
        }

        if (targetType.IsAbstract)
        {
            throw CreateFallbackException(targetType, "AbstractTypeNotSupported", "Register a replacement instance or use a concrete class.");
        }

        try
        {
            var instance = Activator.CreateInstance(targetType);
            if (instance is null)
            {
                throw CreateFallbackException(targetType, "ConstructorReturnedNull", "Register a replacement instance with Shim.New<T>().Returns(...).");
            }

            return (T)instance;
        }
        catch (MissingMethodException exception)
        {
            throw CreateFallbackException(targetType, "PublicParameterlessConstructorNotFound", "Add a public parameterless constructor or register a replacement rule.", exception);
        }
    }

    private static T CreateRealInstanceWithArgs<T>(Type targetType, object?[] args)
    {
        if (!targetType.IsClass)
        {
            throw CreateFallbackException(targetType, "TargetTypeIsNotAClass", "ShimDispatcher.NewWithArgs<T>() fallback supports reference types.");
        }

        if (targetType.ContainsGenericParameters)
        {
            throw CreateFallbackException(targetType, "OpenGenericTypeNotSupported", "Use a closed non-generic class.");
        }

        if (targetType.IsAbstract)
        {
            throw CreateFallbackException(targetType, "AbstractTypeNotSupported", "Register a replacement instance or use a concrete class.");
        }

        try
        {
            var instance = Activator.CreateInstance(targetType, args);
            if (instance is null)
            {
                throw CreateFallbackException(targetType, "ConstructorReturnedNull", "Register a replacement instance with Shim.New<T>().Returns(...).");
            }

            return (T)instance;
        }
        catch (MissingMethodException exception)
        {
            throw CreateFallbackException(targetType, "PublicConstructorNotFound", "Ensure a matching public constructor exists or register a replacement rule.", exception);
        }
    }

    private static ShimUnsupportedException CreateFallbackException(
        Type targetType,
        string reason,
        string hint,
        Exception? innerException = null)
    {
        var message = string.Join(
            Environment.NewLine,
            "New shim fallback cannot create a real instance.",
            $"Target type: {targetType.FullName}",
            "Calling assembly: <manual dispatcher>",
            "Calling method: ShimDispatcher",
            "Rewrite mode: None",
            $"Reason: {reason}",
            "Supported patterns:",
            "  public non-generic class",
            "  public constructor",
            "Unsupported patterns:",
            "  value types",
            "  interfaces",
            "  abstract types",
            "  open generic types",
            $"Hint: {hint}");

        return innerException is null
            ? new ShimUnsupportedException(message)
            : new ShimUnsupportedException(message, innerException);
    }
}
