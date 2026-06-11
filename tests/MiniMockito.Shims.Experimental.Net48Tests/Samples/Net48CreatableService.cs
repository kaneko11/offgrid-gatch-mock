using MiniMockito.Shims.Experimental;

namespace MiniMockito.Shims.Experimental.Net48Tests.Samples
{
    // Implements the shared IShimCreatable contract so the high-level facade can return it
    // strongly-typed via Create<IShimCreatable>(). Delegates to Net48UserService so it does not
    // add an extra Net48UserRepository newobj call site.
    public class Net48CreatableService : IShimCreatable
    {
        public string Describe()
        {
            Net48UserService service = new Net48UserService();
            return service.GetDisplayName(99);
        }
    }
}
