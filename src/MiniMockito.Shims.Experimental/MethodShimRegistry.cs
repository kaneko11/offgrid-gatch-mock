namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Stores instance-method shims for a single <see cref="ShimContext"/> (Phase 25).
/// Keyed by <c>DeclaringTypeFullName::MethodName</c> so cross-assembly / cross-load-context
/// method calls can be matched by name rather than by runtime <see cref="System.Reflection.MethodInfo"/>
/// identity.  When the same key is registered more than once, the most recent registration wins.
/// </summary>
public sealed class MethodShimRegistry
{
    private readonly Dictionary<string, Func<object?, object?[], object?>> _shims =
        new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();

    /// <summary>Gets the number of registered method shims.</summary>
    public int Count
    {
        get { lock (_syncRoot) { return _shims.Count; } }
    }

    /// <summary>Builds the registry key for a method shim.</summary>
    public static string MakeKey(string declaringTypeFullName, string methodName)
        => declaringTypeFullName + "::" + methodName;

    /// <summary>
    /// Registers a method shim. The shim receives the call receiver (or <see langword="null"/> for
    /// the static-like case) and the boxed arguments, and returns the replacement result.
    /// </summary>
    internal void Register(string declaringTypeFullName, string methodName, Func<object?, object?[], object?> shim)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(declaringTypeFullName);
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodName);
        ThrowHelper.ThrowIfNull(shim);

        lock (_syncRoot)
        {
            _shims[MakeKey(declaringTypeFullName, methodName)] = shim; // last stub wins
        }
    }

    internal bool TryGet(string key, out Func<object?, object?[], object?>? shim)
    {
        lock (_syncRoot)
        {
            if (_shims.TryGetValue(key, out var found))
            {
                shim = found;
                return true;
            }
        }

        shim = null;
        return false;
    }

    /// <summary>Removes all registered method shims.</summary>
    public void Clear()
    {
        lock (_syncRoot)
        {
            _shims.Clear();
        }
    }
}
