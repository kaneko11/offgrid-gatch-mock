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

        // Constructs two external types (ExternalDbContext, ExternalLogger) and one internal type
        // (InternalGreeter, defined in this same assembly), so a single session can mix internal and
        // external new-interception targets.
        public string Run(int id)
        {
            using (var db = new ExternalDbContext())
            {
                var logger = new ExternalLogger();
                var greeter = new InternalGreeter();
                return greeter.Decorate(db.GetName(id) + "|" + logger.Tag());
            }
        }
    }

    /// <summary>
    /// An <b>internal</b> sample type (defined in the same assembly that is rewritten).  Used to verify
    /// that internal and external <c>new</c> targets can be mixed in one Easy-API session.
    /// </summary>
    public class InternalGreeter
    {
        private readonly string _mode;

        public InternalGreeter() : this("real")
        {
        }

        public InternalGreeter(string mode)
        {
            _mode = mode;
        }

        public virtual string Decorate(string value)
        {
            return _mode + "(" + value + ")";
        }
    }
}
