using System.Reflection;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Identifies a specific static method by string, avoiding type identity issues across ALCs.
/// All comparisons are ordinal string-based; no CLR <see cref="Type"/> objects are held.
/// </summary>
public sealed class StaticMethodKey : IEquatable<StaticMethodKey>
{
    private readonly string _key;

    /// <param name="declaringTypeFullName">Full name of the declaring type (e.g. "Sample.StaticClock").</param>
    /// <param name="methodName">Method name.</param>
    /// <param name="parameterTypeFullNames">Full names of parameter types in declaration order.</param>
    public StaticMethodKey(string declaringTypeFullName, string methodName, string[] parameterTypeFullNames)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(declaringTypeFullName);
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodName);
        ThrowHelper.ThrowIfNull(parameterTypeFullNames);

        DeclaringTypeFullName = declaringTypeFullName;
        MethodName = methodName;
        ParameterTypeFullNames = parameterTypeFullNames;
        _key = BuildKey(declaringTypeFullName, methodName, parameterTypeFullNames);
    }

    /// <summary>Gets the full name of the declaring type.</summary>
    public string DeclaringTypeFullName { get; }

    /// <summary>Gets the method name.</summary>
    public string MethodName { get; }

    /// <summary>Gets the full names of the parameter types in declaration order.</summary>
    public string[] ParameterTypeFullNames { get; }

    /// <summary>Creates a key from a <see cref="MethodInfo"/>.</summary>
    public static StaticMethodKey From(MethodInfo method)
    {
        ThrowHelper.ThrowIfNull(method);
        return new StaticMethodKey(
            method.DeclaringType?.FullName ?? method.DeclaringType?.Name ?? string.Empty,
            method.Name,
            method.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name).ToArray());
    }

    /// <summary>Creates a key from a <see cref="Type"/>, method name, and parameter types.</summary>
    public static StaticMethodKey From(Type declaringType, string methodName, Type[] paramTypes)
    {
        ThrowHelper.ThrowIfNull(declaringType);
        ThrowHelper.ThrowIfNullOrWhiteSpace(methodName);
        ThrowHelper.ThrowIfNull(paramTypes);

        return new StaticMethodKey(
            declaringType.FullName ?? declaringType.Name,
            methodName,
            paramTypes.Select(p => p.FullName ?? p.Name).ToArray());
    }

    /// <summary>Returns the canonical lookup string: <c>TypeFull::Method(p1,p2)</c>.</summary>
    public string ToKeyString() => _key;

    /// <inheritdoc/>
    public bool Equals(StaticMethodKey? other) => other is not null && _key == other._key;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is StaticMethodKey k && Equals(k);

    /// <inheritdoc/>
    public override int GetHashCode() =>
#if NET5_0_OR_GREATER
        _key.GetHashCode(StringComparison.Ordinal);
#else
        StringComparer.Ordinal.GetHashCode(_key);
#endif

    /// <inheritdoc/>
    public override string ToString() => _key;

    private static string BuildKey(string type, string method, string[] paramTypes)
        => $"{type}::{method}({string.Join(",", paramTypes)})";
}
