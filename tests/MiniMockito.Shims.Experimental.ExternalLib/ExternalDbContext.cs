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

    /// <summary>
    /// A second sample external type used to demonstrate replacing multiple external <c>new</c> calls
    /// in a single session.  <see cref="Tag"/> is virtual so a fake subclass can override it.
    /// </summary>
    public class ExternalLogger
    {
        public virtual string Tag()
        {
            return "real-log";
        }

        public void Write(string message)
        {
            throw new InvalidOperationException("Real logging");
        }
    }

    /// <summary>
    /// A sealed sample external type — used to verify that <c>CreateFakeExternal</c> rejects sealed
    /// types with a clear <see cref="System.NotSupportedException"/>.
    /// </summary>
    public sealed class SealedExternalContext
    {
        public string GetName(int id)
        {
            return "sealed-" + id;
        }
    }

    /// <summary>
    /// A sample external type without a public parameterless constructor — used to verify that
    /// <c>CreateFakeExternal</c> rejects it when no constructor arguments are supplied.
    /// </summary>
    public class NoDefaultCtorContext
    {
        private readonly string _prefix;

        public NoDefaultCtorContext(string prefix)
        {
            _prefix = prefix;
        }

        public string GetName(int id)
        {
            return _prefix + "-" + id;
        }
    }

    /// <summary>
    /// A sample external type whose constructor takes a by-ref parameter — its <c>newobj</c> is
    /// detected but skipped by the rewriter (by-ref constructors are not supported), exercising the
    /// "External newobj skipped" diagnostic.
    /// </summary>
    public class ExternalByRefContext
    {
        public ExternalByRefContext(ref int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }
    }
}
