using System.Collections;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Phase 24 inspection wrapper around a (possibly rewritten) collection.  Supports any
/// <see cref="IEnumerable"/> — arrays, <see cref="IList"/>, <c>IReadOnlyList&lt;T&gt;</c>,
/// <c>ICollection&lt;T&gt;</c>, and <c>ObservableCollection&lt;T&gt;</c> — and exposes each element as
/// a <see cref="ShimsObject"/> so the element type (which may be a rewritten type) can be inspected
/// without casting.
/// </summary>
/// <remarks><b>Experimental.</b> The collection is materialized once at construction.</remarks>
public sealed class ShimsCollection : IEnumerable<ShimsObject>
{
    private readonly List<object?> _items;

    /// <summary>Creates a wrapper around the collection <paramref name="instance"/>.</summary>
    /// <param name="instance">The collection to inspect (must implement <see cref="IEnumerable"/>).</param>
    public ShimsCollection(object instance)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _items = ShimsReflectionAccessor.Materialize(instance);
    }

    /// <summary>Gets the wrapped raw collection.</summary>
    public object Instance { get; }

    /// <summary>Gets the number of elements.</summary>
    public int Count => _items.Count;

    /// <summary>Gets the element at <paramref name="index"/> wrapped as a <see cref="ShimsObject"/>.</summary>
    public ShimsObject this[int index] => new ShimsObject(GetRawItem(index));

    /// <summary>Gets the raw (unwrapped) element at <paramref name="index"/>.</summary>
    public object GetRawItem(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            throw new ShimsInspectionException(string.Join(
                Environment.NewLine,
                "Inspection failed: index out of range.",
                "Failed segment: [" + index + "]",
                "Target runtime type: " + (Instance.GetType().FullName ?? Instance.GetType().Name),
                "Reason: index " + index + " is out of range for a collection of count " + _items.Count + "."));
        }

        var item = _items[index];
        if (item is null)
        {
            throw new ShimsInspectionException(string.Join(
                Environment.NewLine,
                "Inspection failed: the element at the index is null.",
                "Failed segment: [" + index + "]",
                "Reason: a null element cannot be wrapped as a ShimsObject. Use GetRawItem only when the",
                "        element is expected to be non-null, or read scalar values via GetValue."));
        }

        return item;
    }

    /// <summary>Returns all elements wrapped as <see cref="ShimsObject"/> instances.</summary>
    public IReadOnlyList<ShimsObject> ToList()
    {
        var result = new List<ShimsObject>(_items.Count);
        for (var i = 0; i < _items.Count; i++)
            result.Add(this[i]);
        return result;
    }

    /// <inheritdoc />
    public IEnumerator<ShimsObject> GetEnumerator()
    {
        for (var i = 0; i < _items.Count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
