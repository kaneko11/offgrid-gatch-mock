namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Represents an active experimental shim scope.
/// </summary>
public sealed class ShimContext : IDisposable
{
    private static readonly AsyncLocal<ShimContext?> CurrentContext = new();

    private readonly ShimContext? _previousContext;
    private bool _disposed;

    private ShimContext(ShimContext? previousContext)
    {
        _previousContext = previousContext;
    }

    /// <summary>
    /// Gets the unique identifier for this shim context.
    /// </summary>
    public Guid ContextId { get; } = Guid.NewGuid();

    internal ShimRuleRegistry Registry { get; } = new();

    internal bool IsDisposed => _disposed;

    internal static ShimContext? Current => CurrentContext.Value;

    /// <summary>
    /// Creates a new shim context and makes it active for the current async flow.
    /// </summary>
    /// <returns>The active shim context.</returns>
    public static ShimContext Create()
    {
        var context = new ShimContext(CurrentContext.Value);
        CurrentContext.Value = context;
        return context;
    }

    /// <summary>
    /// Disposes this context and removes its registered shim rules.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Registry.Clear();

        if (ReferenceEquals(CurrentContext.Value, this))
        {
            CurrentContext.Value = _previousContext;
        }
    }

    internal static ShimContext RequireCurrent()
    {
        var context = CurrentContext.Value;
        if (context is null || context.IsDisposed)
        {
            throw new ShimException(string.Join(
                Environment.NewLine,
                "No active ShimContext.",
                "Reason: Shim.New<T>() requires an active shim context.",
                "Supported patterns:",
                "  using (ShimContext.Create()) { Shim.New<T>().Returns(fake); }",
                "Hint: Wrap shim setup in using (ShimContext.Create()) before registering rules."));
        }

        return context;
    }

    internal void EnsureActive()
    {
        if (_disposed)
        {
            throw new ShimException(string.Join(
                Environment.NewLine,
                "ShimContext has already been disposed.",
                $"Context ID: {ContextId}",
                "Reason: Rules cannot be registered after Dispose.",
                "Hint: Create a new ShimContext for additional shim setup."));
        }
    }
}
