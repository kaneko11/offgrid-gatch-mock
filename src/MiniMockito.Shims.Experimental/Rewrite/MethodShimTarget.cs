namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes an allowlisted instance-method call site to rewrite into a method shim (Phase 25).
/// </summary>
/// <remarks>
/// <b>Experimental.</b> The call site is matched by the declaring type's full name (arity stripped)
/// and the method name. Only call sites inside the rewritten (target) assembly are affected; the
/// declaring type's own assembly is never modified.
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
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(declaringTypeFullName);
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodName);
        DeclaringTypeFullName = declaringTypeFullName;
        MethodName = methodName;
        ReturnSubstituteInterface = returnSubstituteInterface;
        AssemblySimpleName = assemblySimpleName;
    }

    /// <summary>Gets the declaring type full name (arity stripped at match time).</summary>
    public string DeclaringTypeFullName { get; }

    /// <summary>Gets the method name to intercept.</summary>
    public string MethodName { get; }

    /// <summary>Gets the open generic interface used as the substitute return type for generic methods.</summary>
    public Type? ReturnSubstituteInterface { get; }

    /// <summary>Gets the declaring type's assembly simple name (for diagnostics), or <see langword="null"/>.</summary>
    public string? AssemblySimpleName { get; }
}
