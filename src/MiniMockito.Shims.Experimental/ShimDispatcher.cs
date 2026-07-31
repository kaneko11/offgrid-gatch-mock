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
            && context.MethodRegistry.TryResolve(methodKey, args, out var rule, out _)
            && rule is not null)
        {
            result = rule.Shim(receiver, args);
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

    /// <summary>
    /// Entry point used by generated method-call wrappers. It resolves the exact overload (or a
    /// backward-compatible legacy rule), invokes the replacement, and validates the result before
    /// generated IL casts or unboxes it.
    /// </summary>
    public static bool TryInvokeMethodValidated(
        string methodKey,
        string methodSignature,
        Type expectedReturnType,
        bool isVirtual,
        object? receiver,
        object?[] args,
        string callingAssembly,
        string callingMethod,
        out object? result)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodKey);
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodSignature);
        ThrowHelper.ThrowIfNull(expectedReturnType);
        ThrowHelper.ThrowIfNull(args);
        ThrowHelper.ThrowIfNullOrWhiteSpace(callingAssembly);
        ThrowHelper.ThrowIfNullOrWhiteSpace(callingMethod);

        var context = ShimContext.Current;
        var targetType = GetTargetType(methodKey);
        if (context is not { IsDisposed: false })
        {
            result = null;
            return false;
        }

        if (!context.MethodRegistry.TryResolve(
                methodKey,
                args,
                out var rule,
                out var triedRules) ||
            rule is null)
        {
            context.LastMethodShimResolved = false;
            context.LastMethodDispatchDiagnostics = new MethodDispatchDiagnostics(
                targetType,
                methodSignature,
                expectedReturnType,
                isVirtual,
                callingAssembly,
                callingMethod,
                replacementFound: false,
                selectedRule: null,
                registrationSource: null,
                actualReturnType: null,
                nullReturnedForNonNullableValueType: false,
                triedRules);
            result = null;
            return false;
        }

        try
        {
            result = rule.Shim(receiver, args);
        }
        catch (Exception exception)
        {
            context.LastMethodShimResolved = true;
            context.LastMethodDispatchDiagnostics = CreateMethodDiagnostics(
                targetType,
                methodSignature,
                expectedReturnType,
                isVirtual,
                callingAssembly,
                callingMethod,
                rule,
                triedRules,
                actualReturnType: null,
                nullReturnedForNonNullableValueType: false,
                callbackException: exception);
            throw;
        }

        var actualReturnType = result?.GetType();
        var nullForNonNullableValueType =
            result is null &&
            expectedReturnType != typeof(void) &&
            expectedReturnType.IsValueType &&
            Nullable.GetUnderlyingType(expectedReturnType) is null;

        var returnTypeMatches = ReturnTypeMatches(expectedReturnType, result);
        context.LastMethodShimResolved = true;
        context.LastMethodDispatchDiagnostics = CreateMethodDiagnostics(
            targetType,
            methodSignature,
            expectedReturnType,
            isVirtual,
            callingAssembly,
            callingMethod,
            rule,
            triedRules,
            actualReturnType,
            nullForNonNullableValueType,
            callbackException: null);

        if (!returnTypeMatches)
        {
            throw CreateReturnTypeMismatchException(
                targetType,
                methodSignature,
                expectedReturnType,
                result,
                rule,
                callingAssembly,
                callingMethod,
                nullForNonNullableValueType);
        }

        return true;
    }

    private static MethodDispatchDiagnostics CreateMethodDiagnostics(
        string targetType,
        string methodSignature,
        Type expectedReturnType,
        bool isVirtual,
        string callingAssembly,
        string callingMethod,
        MethodShimRule rule,
        IReadOnlyList<string> triedRules,
        Type? actualReturnType,
        bool nullReturnedForNonNullableValueType,
        Exception? callbackException)
        => new(
            targetType,
            methodSignature,
            expectedReturnType,
            isVirtual,
            callingAssembly,
            callingMethod,
            replacementFound: true,
            selectedRule: "Rule #" + rule.RegistrationOrder + ": " + rule.MethodSignature,
            registrationSource: rule.RegistrationSource,
            actualReturnType,
            nullReturnedForNonNullableValueType,
            triedRules,
            callbackException);

    private static bool ReturnTypeMatches(Type expectedReturnType, object? result)
    {
        if (expectedReturnType == typeof(void))
            return true;

        if (result is null)
        {
            return !expectedReturnType.IsValueType ||
                   Nullable.GetUnderlyingType(expectedReturnType) is not null;
        }

        var actualType = result.GetType();
        var nullableUnderlying = Nullable.GetUnderlyingType(expectedReturnType);
        if (nullableUnderlying is not null)
            return actualType == nullableUnderlying;
        if (expectedReturnType.IsValueType)
            return actualType == expectedReturnType;
        return expectedReturnType.IsInstanceOfType(result);
    }

    private static ShimReturnTypeMismatchException CreateReturnTypeMismatchException(
        string targetType,
        string methodSignature,
        Type expectedReturnType,
        object? result,
        MethodShimRule rule,
        string callingAssembly,
        string callingMethod,
        bool nullForNonNullableValueType)
    {
        var headline = nullForNonNullableValueType
            ? "Replacement callback returned null for a non-nullable value type."
            : "Replacement callback returned a value that is incompatible with the method return type.";
        var actualType = result is null
            ? "null"
            : MethodSignatureFormatter.FormatType(result.GetType());
        var lines = new List<string>
        {
            headline,
            string.Empty,
            "Target type: " + targetType,
            "Method: " + methodSignature,
            "Expected return type: " + MethodSignatureFormatter.FormatType(expectedReturnType),
            "Actual value: " + (result is null ? "null" : result.ToString()),
            "Actual replacement return type: " + actualType,
            "Registration source: " + rule.RegistrationSource,
            "Calling assembly: " + callingAssembly,
            "Calling method: " + callingMethod,
            "Selected rule: Rule #" + rule.RegistrationOrder + ": " + rule.MethodSignature,
        };

        if (nullForNonNullableValueType)
        {
            lines.Add(string.Empty);
            lines.Add(
                "Return a boxed " + MethodSignatureFormatter.FormatType(expectedReturnType) +
                " value, for example:");
            lines.Add("(recv, args) => (object)" + GetExampleValue(expectedReturnType));
        }

        lines.Add(string.Empty);
        lines.Add("Prefer the type-safe API:");
        lines.Add(
            "ReplaceMethod<" + GetFriendlyTypeName(expectedReturnType) +
            ">(methodInfo).Returns(" + GetExampleValue(expectedReturnType) + ")");
        return new ShimReturnTypeMismatchException(string.Join(Environment.NewLine, lines));
    }

    private static string GetTargetType(string methodKey)
    {
        var delimiter = methodKey.IndexOf("::", StringComparison.Ordinal);
        return delimiter < 0 ? "<unknown>" : methodKey.Substring(0, delimiter);
    }

    private static string GetFriendlyTypeName(Type type)
        => type == typeof(int) ? "int"
            : type == typeof(bool) ? "bool"
            : type == typeof(string) ? "string"
            : MethodSignatureFormatter.FormatType(type);

    private static string GetExampleValue(Type type)
    {
        if (type == typeof(int) || type == typeof(short) || type == typeof(long) ||
            type == typeof(byte) || type == typeof(uint) || type == typeof(ushort) ||
            type == typeof(ulong) || type == typeof(sbyte) || type == typeof(float) ||
            type == typeof(double) || type == typeof(decimal))
        {
            return "0";
        }
        if (type == typeof(bool))
            return "false";
        if (type == typeof(char))
            return "'\\0'";
        if (type.IsEnum)
            return "(" + MethodSignatureFormatter.FormatType(type) + ")0";
        if (type.IsValueType)
            return "default(" + GetFriendlyTypeName(type) + ")";
        return "null";
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
