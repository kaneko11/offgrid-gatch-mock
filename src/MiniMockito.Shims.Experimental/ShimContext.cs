namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Represents an active experimental shim scope.
///
/// <para><b>Nested contexts:</b> calling <see cref="Create"/> inside an existing context creates
/// a child scope.  The child has its own isolated rule registry.  Registrations in the outer
/// context are not visible to the inner context, and vice versa.  When the inner context is
/// disposed the outer context becomes active again.  Always dispose contexts in LIFO order
/// (i.e. inner before outer) to maintain correct nesting.</para>
///
/// <para><b>Async / threading:</b> <see cref="ShimContext"/> uses <see cref="AsyncLocal{T}"/>
/// to track the active context per async-flow.  Each <c>Task</c> or <c>async</c> continuation
/// captures the context value at the time it is started, so changes made to the ambient context
/// after the continuation starts are not visible to it.  Background threads started inside a
/// <c>using</c> block inherit the context value at thread-start time; changes on those threads
/// do not propagate back to the parent flow.  For these reasons, do <em>not</em> rely on
/// context isolation when spawning background work inside a shim scope — pass required rule
/// registrations explicitly instead.</para>
///
/// <para><b>Parallel tests:</b> because the dispatcher shares process-wide data structures,
/// parallel test execution is unsafe.  Always run shim tests with
/// <c>[assembly: DoNotParallelize]</c> or <c>[DoNotParallelize]</c> on every test class.</para>
/// </summary>
public sealed class ShimContext : IDisposable
{
    private static readonly AsyncLocal<ShimContext?> CurrentContext = new();
    private static int _activeContextCount;

    private readonly ShimContext? _previousContext;
    private bool _disposed;

    private ShimContext(ShimContext? previousContext)
    {
        _previousContext = previousContext;
        Interlocked.Increment(ref _activeContextCount);
    }

    /// <summary>
    /// Gets the unique identifier for this shim context.
    /// </summary>
    public Guid ContextId { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets any exception thrown during the cleanup phase of <see cref="Dispose"/>.
    /// Returns <see langword="null"/> when cleanup succeeded.
    /// </summary>
    public Exception? CleanupException { get; private set; }

    /// <summary>
    /// Gets the number of <see cref="ShimContext"/> instances that have been created but not yet disposed.
    /// Useful in tests to detect context leaks.
    /// </summary>
    public static int ActiveContextCount => Volatile.Read(ref _activeContextCount);

    /// <summary>
    /// Gets the diagnostics captured by the most recent
    /// <see cref="ShimDispatcher.New{T}"/> or <see cref="ShimDispatcher.NewWithArgs{T}"/> call
    /// within this context.  Returns <see langword="null"/> if no dispatch has occurred yet.
    /// </summary>
    /// <remarks>
    /// <b>Experimental.</b> Intended for debugging and test assertions.
    /// Overwritten on each dispatch call.
    /// </remarks>
    public ShimDispatchDiagnostics? LastDispatchDiagnostics { get; internal set; }

    internal ShimRuleRegistry Registry { get; } = new();

    internal bool IsDisposed => _disposed;

    internal static ShimContext? Current => CurrentContext.Value;

    /// <summary>
    /// Creates a new shim context and makes it active for the current async flow.
    /// Nested calls create a child context whose rules are isolated from the parent.
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
    /// Any exception thrown during cleanup is stored in <see cref="CleanupException"/> and re-thrown.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);

        try
        {
            Registry.Clear();
        }
        catch (Exception ex)
        {
            CleanupException = ex;
            Interlocked.Decrement(ref _activeContextCount);
            if (ReferenceEquals(CurrentContext.Value, this))
            {
                CurrentContext.Value = _previousContext;
            }

            throw new ShimException(
                string.Join(
                    Environment.NewLine,
                    "ShimContext cleanup failed.",
                    $"Context ID: {ContextId}",
                    "Reason: An exception was thrown while clearing registered shim rules.",
                    "Hint: Check CleanupException for details."),
                ex);
        }

        Interlocked.Decrement(ref _activeContextCount);

        if (ReferenceEquals(CurrentContext.Value, this))
        {
            CurrentContext.Value = _previousContext;
        }
    }

    internal static ShimContext RequireCurrent()
    {
        var context = CurrentContext.Value;

        if (context is null)
        {
            throw new ShimException(string.Join(
                Environment.NewLine,
                "No active ShimContext.",
                "Reason: Shim.New<T>() requires an active shim context.",
                "Supported patterns:",
                "  using (ShimContext.Create()) { Shim.New<T>().Returns(fake); }",
                "Hint: Wrap shim setup in using (ShimContext.Create()) before registering rules."));
        }

        if (context.IsDisposed)
        {
            throw new ShimException(string.Join(
                Environment.NewLine,
                "The active ShimContext has already been disposed.",
                $"Context ID: {context.ContextId}",
                "Reason: Shim.New<T>() cannot be called on a disposed context.",
                "Hint: Create a new ShimContext or ensure the using block is still active."));
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
