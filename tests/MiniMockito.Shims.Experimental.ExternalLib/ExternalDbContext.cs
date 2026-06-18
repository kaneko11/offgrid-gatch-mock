using System;

namespace ExternalLib
{
    /// <summary>
    /// A generic sample "external" type that lives in a different assembly than the code that
    /// constructs it.  <see cref="GetName"/> is virtual so a hand-written subclass (or a class mock)
    /// can override it when used as a shim fake.
    /// </summary>
    public class ExternalDbContext : IDisposable
    {
        public virtual string GetName(int id)
        {
            return "real-" + id;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// A second sample external type that is intentionally <b>not</b> registered as a shim target in
    /// most tests, used to prove that only allowlisted external types are rewritten.
    /// </summary>
    public class ExternalOtherContext : IDisposable
    {
        public virtual string GetTag()
        {
            return "real-tag";
        }

        public void Dispose()
        {
        }
    }
}
