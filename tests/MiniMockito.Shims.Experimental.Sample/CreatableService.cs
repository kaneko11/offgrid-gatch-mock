namespace MiniMockito.Shims.Experimental.Sample;

/// <summary>
/// Sample service that implements the shared <see cref="IShimCreatable"/> contract so that the
/// high-level <c>Shims</c> facade can return it strongly-typed via <c>Create&lt;IShimCreatable&gt;()</c>.
///
/// <para><b>Note:</b> it delegates to <see cref="UserService"/> rather than calling
/// <c>new UserRepository()</c> directly, so it does not add any extra <c>UserRepository</c>
/// newobj call site to the sample assembly.</para>
/// </summary>
public class CreatableService : IShimCreatable
{
    public string Describe()
    {
        var service = new UserService();
        return service.GetDisplayName(99);
    }
}
