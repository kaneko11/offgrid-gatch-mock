namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Phase 24 inspection wrapper around a single (possibly rewritten) object.  Lets tests observe an
/// object graph by property path without casting the value to the test's original type — which is
/// unsafe when the runtime type lives in a different load context.
/// </summary>
/// <remarks><b>Experimental.</b> Pure reflection helper; holds no session / load-context state.</remarks>
public sealed class ShimsObject
{
    /// <summary>Creates a wrapper around <paramref name="instance"/>.</summary>
    /// <param name="instance">The object to inspect (must not be null).</param>
    public ShimsObject(object instance)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
    }

    /// <summary>Gets the wrapped raw object.</summary>
    public object Instance { get; }

    /// <summary>Evaluates <paramref name="path"/> and returns the raw value (may be null at the leaf).</summary>
    public object GetValue(string path) => ShimsPathEvaluator.Evaluate(Instance, path)!;

    /// <summary>Evaluates <paramref name="path"/> and converts the value to <typeparamref name="T"/>.</summary>
    public T GetValue<T>(string path)
        => (T)ShimsReflectionAccessor.ConvertValue(ShimsPathEvaluator.Evaluate(Instance, path), typeof(T), path)!;

    /// <summary>Alias of <see cref="GetValue{T}(string)"/>.</summary>
    public T Get<T>(string path) => GetValue<T>(path);

    /// <summary>Reads a single public property or field by name (no path navigation).</summary>
    public object GetProperty(string propertyName)
        => ShimsReflectionAccessor.GetMember(Instance, propertyName, propertyName, propertyName)!;

    /// <summary>Reads a single public property or field by name and converts it to <typeparamref name="T"/>.</summary>
    public T GetProperty<T>(string propertyName)
        => (T)ShimsReflectionAccessor.ConvertValue(
            ShimsReflectionAccessor.GetMember(Instance, propertyName, propertyName, propertyName),
            typeof(T),
            propertyName)!;

    /// <summary>Evaluates <paramref name="path"/> and wraps the (non-null) result as a <see cref="ShimsObject"/>.</summary>
    public ShimsObject GetObject(string path)
    {
        var value = ShimsPathEvaluator.Evaluate(Instance, path);
        if (value is null)
            throw new ShimsInspectionException(string.Join(
                Environment.NewLine,
                "Inspection failed: the value at the path was null and cannot be wrapped as an object.",
                "Requested path: " + path));
        return new ShimsObject(value);
    }

    /// <summary>Evaluates <paramref name="path"/> and wraps the (non-null) result as a <see cref="ShimsCollection"/>.</summary>
    public ShimsCollection GetCollection(string path)
    {
        var value = ShimsPathEvaluator.Evaluate(Instance, path);
        if (value is null)
            throw new ShimsInspectionException(string.Join(
                Environment.NewLine,
                "Inspection failed: the collection at the path was null.",
                "Requested path: " + path));
        return new ShimsCollection(value);
    }
}
