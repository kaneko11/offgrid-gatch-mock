namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Describes a <c>newobj</c> target type that is defined in an assembly <b>other</b> than the
/// assembly being rewritten (a cross-assembly target).
/// </summary>
/// <remarks>
/// <b>Experimental.</b> Part of the Phase 20 cross-assembly new-interception PoC.
/// <para>
/// Unlike internal targets (types defined inside the rewritten assembly), external types may be
/// resolved through a different load context than the one that registered the shim.  For this
/// reason external shim rules are keyed by <see cref="TypeFullName"/> (optionally qualified with
/// <see cref="AssemblySimpleName"/>) rather than by the runtime <see cref="Type"/> identity.
/// </para>
/// </remarks>
internal sealed class ExternalNewTarget
{
    public ExternalNewTarget(Type originalType)
    {
        ThrowHelper.ThrowIfNull(originalType);
        OriginalType = originalType;
        TypeFullName = originalType.FullName
            ?? throw new InvalidOperationException(
                $"External target type {originalType.Name} has no FullName and cannot be used as a shim key.");
        AssemblySimpleName = originalType.Assembly.GetName().Name ?? string.Empty;
    }

    /// <summary>Gets the external type as seen by the test (registration) load context.</summary>
    public Type OriginalType { get; }

    /// <summary>Gets the <see cref="Type.FullName"/> used as the primary shim key.</summary>
    public string TypeFullName { get; }

    /// <summary>Gets the simple name of the assembly that defines the external type.</summary>
    public string AssemblySimpleName { get; }
}
