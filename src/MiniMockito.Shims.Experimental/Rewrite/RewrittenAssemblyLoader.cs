using System.Reflection;
using System.Runtime.Loader;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Loads a rewritten assembly into an isolated context for experimental tests.
/// </summary>
public sealed class RewrittenAssemblyLoader : IDisposable
{
    private readonly RewrittenAssemblyLoadContext context;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RewrittenAssemblyLoader"/> class.
    /// </summary>
    /// <param name="assemblyPath">The rewritten assembly path.</param>
    public RewrittenAssemblyLoader(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        AssemblyPath = Path.GetFullPath(assemblyPath);
        context = new RewrittenAssemblyLoadContext(AssemblyPath);
    }

    /// <summary>
    /// Gets the rewritten assembly path.
    /// </summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// Loads the rewritten assembly.
    /// </summary>
    /// <returns>The loaded assembly.</returns>
    public Assembly Load()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return context.LoadFromAssemblyPath(AssemblyPath);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        context.Unload();
    }

    private sealed class RewrittenAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver resolver;

        public RewrittenAssemblyLoadContext(string mainAssemblyPath)
            : base(isCollectible: true)
        {
            resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var shimAssembly = typeof(ShimDispatcher).Assembly;
            if (AssemblyName.ReferenceMatchesDefinition(shimAssembly.GetName(), assemblyName))
            {
                return shimAssembly;
            }

            var assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
        }
    }
}
