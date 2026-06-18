using ExternalLib;

namespace CrossAssemblySample
{
    /// <summary>
    /// Sample caller defined in a different assembly than <see cref="ExternalDbContext"/>.
    /// Each method constructs an external type with <c>new</c>; these cross-assembly <c>newobj</c>
    /// instructions are the target of the Phase 20 rewrite.
    /// </summary>
    public class CrossAssemblyUserService
    {
        public string GetDisplayName(int id)
        {
            using (var context = new ExternalDbContext())
            {
                return context.GetName(id);
            }
        }

        public string GetOtherTag()
        {
            using (var context = new ExternalOtherContext())
            {
                return context.GetTag();
            }
        }

        // Constructs an external type whose constructor takes a by-ref parameter.
        // The rewriter detects this cross-assembly newobj but skips it (by-ref ctor unsupported),
        // exercising the "External newobj skipped" diagnostic.
        public int CreateByRefSeed(int seed)
        {
            var context = new ExternalByRefContext(ref seed);
            return context.Seed;
        }
    }
}
