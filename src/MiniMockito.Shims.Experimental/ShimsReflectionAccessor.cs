using System.Collections;
using System.Globalization;
using System.Reflection;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Internal reflection helpers shared by the Phase 24 inspection API.  Operates on plain
/// <see cref="object"/> instances (typically created by <see cref="Shims.CreateObject"/>) so that
/// rewritten types — which live in a different load context — can be observed without casting them
/// to the test's original type.
/// </summary>
internal static class ShimsReflectionAccessor
{
    private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>Reads a public property or field named <paramref name="name"/> from <paramref name="target"/>.</summary>
    internal static object? GetMember(object target, string name, string requestedPath, string segment)
    {
        var type = target.GetType();

        var property = type.GetProperty(name, MemberFlags);
        if (property is not null && property.CanRead)
        {
            return property.GetValue(target);
        }

        var field = type.GetField(name, MemberFlags);
        if (field is not null)
        {
            return field.GetValue(target);
        }

        if (string.Equals(name, "Count", StringComparison.Ordinal) && target is IEnumerable)
        {
            return GetCount(target);
        }

        throw new ShimsInspectionException(string.Join(
            Environment.NewLine,
            "Inspection failed: property or field was not found.",
            "Requested path: " + requestedPath,
            "Failed segment: " + segment,
            "Target runtime type: " + (type.FullName ?? type.Name),
            "Reason: no public readable property or field named '" + name + "' exists on the runtime type.",
            "Hint: the runtime type may differ from the original (rewritten load context) — inspect with",
            "      shims.Inspect(obj) and use the exact member names of the runtime type."));
    }

    /// <summary>Reads element <paramref name="index"/> from an indexable <paramref name="target"/>.</summary>
    internal static object? GetIndex(object target, int index, string requestedPath, string segment)
    {
        if (target is IList list)
        {
            if (index < 0 || index >= list.Count)
                throw IndexOutOfRange(requestedPath, segment, target, index, list.Count);
            return list[index];
        }

        if (target is IEnumerable enumerable)
        {
            var materialized = new List<object?>();
            foreach (var item in enumerable)
                materialized.Add(item);

            if (index < 0 || index >= materialized.Count)
                throw IndexOutOfRange(requestedPath, segment, target, index, materialized.Count);
            return materialized[index];
        }

        throw new ShimsInspectionException(string.Join(
            Environment.NewLine,
            "Inspection failed: target is not indexable.",
            "Requested path: " + requestedPath,
            "Failed segment: " + segment,
            "Target runtime type: " + (target.GetType().FullName ?? target.GetType().Name),
            "Reason: the value does not implement IList or IEnumerable, so [index] cannot be applied."));
    }

    /// <summary>Counts the elements of a collection via ICollection / ICollection&lt;T&gt; / IReadOnlyCollection&lt;T&gt; / enumeration.</summary>
    internal static int GetCount(object target)
    {
        if (target is ICollection nonGeneric)
            return nonGeneric.Count;

        foreach (var iface in target.GetType().GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;
            var definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(ICollection<>) || definition == typeof(IReadOnlyCollection<>))
            {
                var countProperty = iface.GetProperty("Count");
                if (countProperty is not null)
                    return (int)countProperty.GetValue(target)!;
            }
        }

        if (target is IEnumerable enumerable)
        {
            var count = 0;
            foreach (var _ in enumerable)
                count++;
            return count;
        }

        throw new ShimsInspectionException(
            "Inspection failed: 'Count' is not available on runtime type " +
            (target.GetType().FullName ?? target.GetType().Name) + ".");
    }

    /// <summary>Materializes an enumerable into a list of raw items for collection inspection.</summary>
    internal static List<object?> Materialize(object instance)
    {
        if (instance is IEnumerable enumerable)
        {
            var items = new List<object?>();
            foreach (var item in enumerable)
                items.Add(item);
            return items;
        }

        throw new ShimsInspectionException(string.Join(
            Environment.NewLine,
            "Inspection failed: value is not a collection.",
            "Target runtime type: " + (instance.GetType().FullName ?? instance.GetType().Name),
            "Reason: the value does not implement IEnumerable, so it cannot be inspected as a collection.",
            "Hint: use shims.GetValue(obj, path) for scalar properties instead of GetCollection."));
    }

    /// <summary>
    /// Converts <paramref name="value"/> to <paramref name="targetType"/> for the typed inspection
    /// accessors.  Does <b>not</b> cast a rewritten value to a same-named original type; when no safe
    /// conversion exists it throws a <see cref="ShimsInspectionException"/> with identity-mismatch guidance.
    /// </summary>
    internal static object? ConvertValue(object? value, Type targetType, string requestedPath)
    {
        if (targetType == typeof(object))
            return value;

        var underlying = Nullable.GetUnderlyingType(targetType);

        if (value is null)
        {
            if (!targetType.IsValueType || underlying is not null)
                return null;

            throw new ShimsInspectionException(string.Join(
                Environment.NewLine,
                "Inspection failed: cannot convert a null value to a non-nullable value type.",
                "Requested path: " + requestedPath,
                "Requested type: " + (targetType.FullName ?? targetType.Name),
                "Reason: the value at the path was null.",
                "Hint: use a nullable type (e.g. int?) or GetValue<object> to read a possibly-null value."));
        }

        var effective = underlying ?? targetType;

        // Already assignable (includes shared BCL types such as string/int). We never force a cast of a
        // rewritten type to a same-named original type — IsInstanceOfType is false across load contexts.
        if (effective.IsInstanceOfType(value))
            return value;

        try
        {
            if (effective.IsEnum)
            {
                return value is string enumText
                    ? Enum.Parse(effective, enumText, ignoreCase: true)
                    : Enum.ToObject(effective, value);
            }

            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(effective))
            {
                return System.Convert.ChangeType(value, effective, CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            throw new ShimsInspectionException(string.Join(
                Environment.NewLine,
                "Inspection failed: value could not be converted to the requested type.",
                "Requested path: " + requestedPath,
                "Requested type: " + (targetType.FullName ?? targetType.Name),
                "Actual runtime type: " + (value.GetType().FullName ?? value.GetType().Name),
                "Reason: " + ex.Message),
                ex);
        }

        throw new ShimsInspectionException(string.Join(
            Environment.NewLine,
            "Inspection failed: value is not assignable or convertible to the requested type.",
            "Requested path: " + requestedPath,
            "Requested type: " + (targetType.FullName ?? targetType.Name),
            "Actual runtime type: " + (value.GetType().FullName ?? value.GetType().Name),
            "Reason: the rewritten object may belong to a different load context / assembly identity",
            "        than the requested type, so a strongly typed cast is unsafe.",
            "Hint: use object / the inspection API instead of a strongly typed cast;",
            "      use GetValue<T> for primitive properties (string/int/bool/enum/...)."));
    }

    private static ShimsInspectionException IndexOutOfRange(
        string requestedPath, string segment, object target, int index, int count)
    {
        return new ShimsInspectionException(string.Join(
            Environment.NewLine,
            "Inspection failed: index out of range.",
            "Requested path: " + requestedPath,
            "Failed segment: " + segment,
            "Target runtime type: " + (target.GetType().FullName ?? target.GetType().Name),
            "Reason: index " + index + " is out of range for a collection of count " + count + "."));
    }
}
