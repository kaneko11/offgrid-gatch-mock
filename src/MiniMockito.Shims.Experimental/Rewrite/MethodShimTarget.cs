namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes an allowlisted instance-method call site to rewrite into a method shim (Phase 25).
/// </summary>
/// <remarks>
/// <b>Experimental.</b> Legacy targets are matched by declaring type and method name.
/// Type-safe targets additionally carry the exact parameter types and registry key, so overloads
/// are never selected by name alone. Only call sites inside the rewritten assembly are affected.
/// </remarks>
public sealed class MethodShimTarget
{
    /// <summary>Creates a method-shim target.</summary>
    /// <param name="declaringTypeFullName">Declaring type full name (e.g. <c>"ExternalLib.ExternalGateway"</c>).</param>
    /// <param name="methodName">The instance method name to intercept.</param>
    /// <param name="returnSubstituteInterface">
    /// For <b>generic</b> methods (arity 1), the open generic interface to use as the wrapper return type,
    /// closed with the call site's type argument (e.g. <c>typeof(IEnumerable&lt;&gt;)</c>).  Required for generic
    /// methods; ignored for non-generic methods (which keep their concrete return type).
    /// </param>
    /// <param name="assemblySimpleName">Optional simple name of the declaring type's assembly (for diagnostics).</param>
    public MethodShimTarget(
        string declaringTypeFullName,
        string methodName,
        Type? returnSubstituteInterface = null,
        string? assemblySimpleName = null)
        : this(
            declaringTypeFullName,
            methodName,
            returnSubstituteInterface,
            assemblySimpleName,
            parameterTypeNames: null,
            registryKey: null,
            methodSignature: null,
            returnTypeName: null,
            isVirtual: null,
            registrationSource: "legacy untyped API")
    {
    }

    internal MethodShimTarget(
        string declaringTypeFullName,
        string methodName,
        Type? returnSubstituteInterface,
        string? assemblySimpleName,
        IReadOnlyList<string>? parameterTypeNames,
        string? registryKey,
        string? methodSignature,
        string? returnTypeName,
        bool? isVirtual,
        string registrationSource)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(declaringTypeFullName);
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodName);
        ThrowHelper.ThrowIfNullOrWhiteSpace(registrationSource);
        DeclaringTypeFullName = declaringTypeFullName;
        MethodName = methodName;
        ReturnSubstituteInterface = returnSubstituteInterface;
        AssemblySimpleName = assemblySimpleName;
        ParameterTypeNames = parameterTypeNames;
        RegistryKey = registryKey ??
            MethodShimRegistry.MakeKey(declaringTypeFullName, methodName);
        MethodSignature = methodSignature ??
            declaringTypeFullName + "." + methodName + "(<legacy name-only>)";
        ReturnTypeName = returnTypeName;
        IsVirtual = isVirtual;
        RegistrationSource = registrationSource;
    }

    /// <summary>Gets the declaring type full name (arity stripped at match time).</summary>
    public string DeclaringTypeFullName { get; }

    /// <summary>Gets the method name to intercept.</summary>
    public string MethodName { get; }

    /// <summary>Gets the open generic interface used as the substitute return type for generic methods.</summary>
    public Type? ReturnSubstituteInterface { get; }

    /// <summary>Gets the declaring type's assembly simple name (for diagnostics), or <see langword="null"/>.</summary>
    public string? AssemblySimpleName { get; }

    /// <summary>Gets exact parameter type names, or null for the advanced legacy name-only API.</summary>
    public IReadOnlyList<string>? ParameterTypeNames { get; }

    /// <summary>Gets whether this target was resolved from an exact <see cref="System.Reflection.MethodInfo"/>.</summary>
    public bool HasExactSignature => ParameterTypeNames is not null;

    /// <summary>Gets the overload-safe registry key used by the generated wrapper.</summary>
    public string RegistryKey { get; }

    /// <summary>Gets the exact reflected signature, or a legacy name-only description.</summary>
    public string MethodSignature { get; }

    /// <summary>Gets the reflected return type name, or null for a legacy target.</summary>
    public string? ReturnTypeName { get; }

    /// <summary>Gets reflected virtuality, or null for a legacy target.</summary>
    public bool? IsVirtual { get; }

    /// <summary>Gets the API family that registered the target.</summary>
    public string RegistrationSource { get; }
}
