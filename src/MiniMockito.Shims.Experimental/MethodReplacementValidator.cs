using System.Reflection;

namespace MiniMockito.Shims.Experimental;

internal static class MethodReplacementValidator
{
    internal static MethodReplacementDescriptor ValidateTyped<TResult>(MethodInfo method)
    {
        var descriptor = ValidateInstanceMethod(method, "typed API");
        var requestedReturnType = typeof(TResult);

        if (method.ReturnType == typeof(void))
        {
            throw SignatureFailure(
                method,
                "ReturnValueApiUsedForVoidMethod",
                "Use ReplaceVoidMethod(methodInfo).DoNothing() or .Callback(...) for a void method.");
        }

        if (method.ReturnType != requestedReturnType)
        {
            throw new ShimMethodSignatureException(string.Join(
                Environment.NewLine,
                "The typed method replacement return type does not match the reflected method.",
                "Target type: " + FormatDeclaringType(method),
                "Method name: " + method.Name,
                "Exact MethodInfo signature: " + MethodSignatureFormatter.Format(method),
                "Method return type: " + MethodSignatureFormatter.FormatType(method.ReturnType),
                "Requested TResult: " + MethodSignatureFormatter.FormatType(requestedReturnType),
                "Instance / static: instance",
                "Virtual / non-virtual: " + (method.IsVirtual ? "virtual" : "non-virtual"),
                "Selected backend: " + descriptor.Backend,
                "Reason: TResult must exactly match MethodInfo.ReturnType for the type-safe API.",
                "Hint: use ReplaceMethod<" + GetFriendlyTypeName(method.ReturnType) +
                    ">(methodInfo), or use ReplaceVoidMethod for void."));
        }

        return descriptor;
    }

    internal static MethodReplacementDescriptor ValidateVoid(MethodInfo method)
    {
        var descriptor = ValidateInstanceMethod(method, "typed void API");
        if (method.ReturnType != typeof(void))
        {
            throw SignatureFailure(
                method,
                "VoidApiUsedForReturnValueMethod",
                "Use ReplaceMethod<" + GetFriendlyTypeName(method.ReturnType) +
                    ">(methodInfo).Returns(...) for this method.");
        }

        return descriptor;
    }

    internal static MethodReplacementDescriptor ValidateInstanceMethod(
        MethodInfo method,
        string registrationSource)
    {
        if (method is null)
            throw new ArgumentNullException(nameof(method));

        if (method.DeclaringType is null)
        {
            throw new ShimMethodSignatureException(string.Join(
                Environment.NewLine,
                "Method replacement requires a declaring type.",
                "Target type: <unknown>",
                "Method name: " + method.Name,
                "Reason: MethodInfo.DeclaringType was null.",
                "Hint: pass a MethodInfo obtained from a concrete runtime Type."));
        }

        if (method.IsStatic)
        {
            throw SignatureFailure(
                method,
                "StaticMethodPassedToInstanceApi",
                "Use the existing Static<TResult>(...) / Static(...) API for static methods.");
        }

        var declaringAssemblyName = method.DeclaringType.Assembly.GetName().Name ?? string.Empty;
        if (declaringAssemblyName == "mscorlib" ||
            declaringAssemblyName == "System.Private.CoreLib" ||
            declaringAssemblyName == "System.Runtime" ||
            declaringAssemblyName == "netstandard")
        {
            throw SignatureFailure(
                method,
                "BclDeclaringTypeNotSupported",
                "BCL method interception is outside MiniMockito.Shims.Experimental's supported scope.");
        }

        if (!method.IsPublic)
        {
            throw SignatureFailure(
                method,
                "NonPublicMethodNotSupported",
                "The typed instance method API currently supports public methods only.");
        }

        if (method.IsSpecialName)
        {
            throw SignatureFailure(
                method,
                "SpecialNameMethodNotSupported",
                "Property/event accessors and operators are outside the initial type-safe method replacement scope.");
        }

        if (method.IsAbstract)
        {
            throw SignatureFailure(
                method,
                "AbstractMethodNotSupported",
                "Use a class/interface proxy for abstract members; call-site fallback cannot invoke an abstract method.");
        }

        if (method.IsGenericMethod || method.ContainsGenericParameters)
        {
            throw SignatureFailure(
                method,
                "GenericMethodTypedApiNotSupported",
                "Use the advanced legacy ReplaceMethod(..., returnSubstituteInterface) API for the existing single-type-argument generic scenario.");
        }

        if (method.ReturnType.IsByRef || method.ReturnType.IsPointer)
        {
            throw SignatureFailure(
                method,
                "ByRefOrPointerReturnNotSupported",
                "By-ref and pointer returns are outside the initial type-safe method replacement scope.");
        }

        foreach (var parameter in method.GetParameters())
        {
            if (parameter.ParameterType.IsByRef || parameter.ParameterType.IsPointer)
            {
                throw SignatureFailure(
                    method,
                    "ByRefOrPointerParameterNotSupported",
                    "ref/out/in and pointer parameters are outside the initial type-safe method replacement scope.");
            }
        }

        return new MethodReplacementDescriptor(
            method,
            MethodInterceptionBackend.InstanceCallSiteRewrite,
            registrationSource);
    }

    internal static MethodInfo ResolveMethod(Type declaringType, string methodName, Type[]? parameterTypes)
    {
        if (declaringType is null)
            throw new ArgumentNullException(nameof(declaringType));
        if (string.IsNullOrWhiteSpace(methodName))
            throw new ArgumentException("Method name must be provided.", nameof(methodName));

        var candidates = declaringType
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
            .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))
            .ToArray();

        if (parameterTypes is null)
        {
            throw ResolutionFailure(
                declaringType,
                methodName,
                null,
                candidates,
                "ParameterTypesRequired",
                "Pass an exact Type[]; use Type.EmptyTypes for a zero-argument method.");
        }

        if (parameterTypes.Any(t => t is null))
        {
            throw ResolutionFailure(
                declaringType,
                methodName,
                parameterTypes,
                candidates,
                "NullParameterType",
                "Every parameter type must be non-null.");
        }

        var matches = candidates
            .Where(candidate => ParametersEqual(candidate.GetParameters(), parameterTypes))
            .ToArray();

        if (matches.Length != 1)
        {
            throw ResolutionFailure(
                declaringType,
                methodName,
                parameterTypes,
                candidates,
                matches.Length == 0 ? "ExactSignatureNotFound" : "OverloadAmbiguous",
                matches.Length == 0
                    ? "Check every parameter type, including optional parameters."
                    : "Obtain the exact MethodInfo with reflection and pass it to ReplaceMethod<TResult>(methodInfo).");
        }

        return matches[0];
    }

    internal static Type[] CombineParameterTypes(Type firstParameterType, Type[]? additionalParameterTypes)
    {
        if (firstParameterType is null)
            throw new ArgumentNullException(nameof(firstParameterType));
        if (additionalParameterTypes is null)
            throw new ArgumentNullException(nameof(additionalParameterTypes));

        var result = new Type[additionalParameterTypes.Length + 1];
        result[0] = firstParameterType;
        Array.Copy(additionalParameterTypes, 0, result, 1, additionalParameterTypes.Length);
        return result;
    }

    internal static void ValidateMatchers(MethodInfo method, IReadOnlyList<IShimArgumentMatcher> matchers)
    {
        var parameters = method.GetParameters();
        if (matchers.Count != parameters.Length)
        {
            throw new ShimMethodSignatureException(string.Join(
                Environment.NewLine,
                "Method replacement argument matcher count does not match the exact signature.",
                "Target type: " + FormatDeclaringType(method),
                "Method name: " + method.Name,
                "Exact MethodInfo signature: " + MethodSignatureFormatter.Format(method),
                "Expected matcher count: " + parameters.Length,
                "Actual matcher count: " + matchers.Count,
                "Reason: optional parameters are still part of MethodInfo.GetParameters().",
                "Hint: supply one matcher per declared parameter."));
        }

        for (var i = 0; i < matchers.Count; i++)
        {
            var expected = matchers[i].ExpectedType;
            if (expected is null)
                continue;

            var parameterType = parameters[i].ParameterType;
            if (TypesCanOverlap(parameterType, expected))
                continue;

            throw new ShimMethodSignatureException(string.Join(
                Environment.NewLine,
                "Method replacement argument matcher type is incompatible with the exact signature.",
                "Target type: " + FormatDeclaringType(method),
                "Method name: " + method.Name,
                "Exact MethodInfo signature: " + MethodSignatureFormatter.Format(method),
                "Parameter index: " + i,
                "Parameter type: " + MethodSignatureFormatter.FormatType(parameterType),
                "Matcher type: " + MethodSignatureFormatter.FormatType(expected),
                "Matcher: " + matchers[i].Describe(),
                "Reason: the matcher type cannot match a value of the declared parameter type.",
                "Hint: choose Any<T>/Eq<T>/Is<T>/ShimCaptor<T> with a compatible T."));
        }
    }

    private static bool ParametersEqual(ParameterInfo[] parameters, Type[] requested)
    {
        if (parameters.Length != requested.Length)
            return false;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType != requested[i])
                return false;
        }
        return true;
    }

    private static bool TypesCanOverlap(Type parameterType, Type matcherType)
    {
        if (parameterType == matcherType)
            return true;

        var parameterUnderlying = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
        var matcherUnderlying = Nullable.GetUnderlyingType(matcherType) ?? matcherType;
        if (parameterUnderlying == matcherUnderlying)
            return true;

        return parameterType.IsAssignableFrom(matcherType) ||
               matcherType.IsAssignableFrom(parameterType);
    }

    private static ShimMethodSignatureException ResolutionFailure(
        Type declaringType,
        string methodName,
        Type[]? parameterTypes,
        IEnumerable<MethodInfo> candidates,
        string reason,
        string hint)
    {
        var candidateList = candidates.Select(MethodSignatureFormatter.Format).ToArray();
        return new ShimMethodSignatureException(string.Join(
            Environment.NewLine,
            "Method replacement could not resolve one exact method signature.",
            "Target type: " + MethodSignatureFormatter.FormatType(declaringType),
            "Method name: " + methodName,
            "Requested parameter types: " +
                (parameterTypes is null
                    ? "<null / not specified>"
                    : MethodSignatureFormatter.FormatRequestedParameterTypes(parameterTypes)),
            "Candidate methods:",
            candidateList.Length == 0
                ? "  <none>"
                : string.Join(Environment.NewLine, candidateList.Select(candidate => "  " + candidate)),
            "Reason: " + reason,
            "Hint: " + hint));
    }

    private static ShimMethodSignatureException SignatureFailure(
        MethodInfo method,
        string reason,
        string hint)
        => new(string.Join(
            Environment.NewLine,
            "Method replacement rejected the reflected method.",
            "Target type: " + FormatDeclaringType(method),
            "Method name: " + method.Name,
            "Exact MethodInfo signature: " + MethodSignatureFormatter.Format(method),
            "Return type: " + MethodSignatureFormatter.FormatType(method.ReturnType),
            "Parameter types: " + MethodSignatureFormatter.FormatRequestedParameterTypes(
                method.GetParameters().Select(p => p.ParameterType)),
            "Instance / static: " + (method.IsStatic ? "static" : "instance"),
            "Virtual / non-virtual: " + (method.IsVirtual ? "virtual" : "non-virtual"),
            "Abstract: " + method.IsAbstract,
            "Final: " + method.IsFinal,
            "Selected backend: " + MethodInterceptionBackend.Unsupported,
            "Reason: " + reason,
            "Hint: " + hint));

    private static string FormatDeclaringType(MethodInfo method)
        => method.DeclaringType is null
            ? "<unknown>"
            : MethodSignatureFormatter.FormatType(method.DeclaringType);

    private static string GetFriendlyTypeName(Type type)
        => type == typeof(int) ? "int"
            : type == typeof(bool) ? "bool"
            : type == typeof(string) ? "string"
            : MethodSignatureFormatter.FormatType(type);
}

internal sealed class MethodReplacementDescriptor
{
    internal MethodReplacementDescriptor(
        MethodInfo method,
        MethodInterceptionBackend backend,
        string registrationSource)
    {
        Method = method;
        Backend = backend;
        RegistrationSource = registrationSource;
        RegistryKey = MethodSignatureFormatter.MakeRegistryKey(method);
        Signature = MethodSignatureFormatter.Format(method);
    }

    internal MethodInfo Method { get; }
    internal MethodInterceptionBackend Backend { get; }
    internal string RegistrationSource { get; }
    internal string RegistryKey { get; }
    internal string Signature { get; }
}
