namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Optional contract that enables strongly-typed creation through the high-level
/// <see cref="Shims"/> facade.
/// </summary>
/// <remarks>
/// <b>Experimental.</b> Part of the <c>MiniMockito.Shims.Experimental</c> package.
/// <para>
/// Rewritten assemblies are loaded into an isolated load context (a collectible
/// <c>AssemblyLoadContext</c> on .NET, or via <c>Assembly.Load(byte[])</c> on .NET Framework),
/// so a rewritten concrete type has a <em>different</em> runtime identity from the same-named
/// type in the default load context — a direct cast to the original concrete type always fails.
/// </para>
/// <para>
/// The <c>MiniMockito.Shims.Experimental</c> assembly itself is always resolved from the parent
/// (default) context, so an interface declared <em>in this assembly</em> keeps a single shared
/// identity across the boundary.  A class that implements <see cref="IShimCreatable"/> can
/// therefore be returned strongly-typed by <see cref="Shims.Create{T}"/> (called as
/// <c>shims.Create&lt;IShimCreatable&gt;()</c>) while still routing its method calls through the
/// rewritten (intercepted) code.  For types that do not implement a shared contract, use
/// <see cref="Shims.CreateObject"/> + <see cref="Shims.Invoke{TResult}"/> instead.
/// </para>
/// </remarks>
public interface IShimCreatable
{
    /// <summary>
    /// Runs the service's primary operation and returns a textual result.
    /// Implementations typically delegate to code paths that contain shimmed
    /// <c>new</c> / static call sites.
    /// </summary>
    string Describe();
}
