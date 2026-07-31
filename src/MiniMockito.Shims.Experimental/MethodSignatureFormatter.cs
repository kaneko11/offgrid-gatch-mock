using System.Reflection;
using System.Text;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Formats reflected method signatures and type identities for method-replacement diagnostics.
/// </summary>
public static class MethodSignatureFormatter
{
    /// <summary>Formats a method including its declaring type, parameters, and return type.</summary>
    public static string Format(MethodInfo method)
    {
        ThrowHelper.ThrowIfNull(method);

        var declaring = method.DeclaringType is null
            ? "<unknown>"
            : FormatType(method.DeclaringType);
        var parameters = string.Join(
            ", ",
            method.GetParameters().Select(FormatParameter));

        return FormatType(method.ReturnType) + " " + declaring + "." + method.Name +
            "(" + parameters + ")";
    }

    /// <summary>Formats a runtime type without assembly-qualified generic argument noise.</summary>
    public static string FormatType(Type type)
    {
        ThrowHelper.ThrowIfNull(type);

        if (type.IsByRef)
            return FormatType(type.GetElementType()!) + "&";
        if (type.IsPointer)
            return FormatType(type.GetElementType()!) + "*";
        if (type.IsArray)
            return FormatType(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsGenericParameter)
            return type.Name;

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var definitionName = (definition.FullName ?? definition.Name).Replace('+', '/');
            var arguments = string.Join(", ", type.GetGenericArguments().Select(FormatType));
            return definitionName + "<" + arguments + ">";
        }

        return (type.FullName ?? type.Name).Replace('+', '/');
    }

    internal static string MakeRegistryKey(MethodInfo method)
    {
        var declaring = method.DeclaringType?.FullName
            ?? throw new ShimMethodSignatureException(
                "Method replacement requires a declaring type. Method: " + method.Name);
        return MethodShimRegistry.MakeSignatureKey(
            declaring.Replace('+', '/'),
            method.Name,
            method.GetParameters().Select(p => FormatType(p.ParameterType)));
    }

    internal static string FormatRequestedParameterTypes(IEnumerable<Type> parameterTypes)
        => "[" + string.Join(", ", parameterTypes.Select(FormatType)) + "]";

    private static string FormatParameter(ParameterInfo parameter)
    {
        var builder = new StringBuilder();
        if (parameter.IsOut)
            builder.Append("out ");
        else if (parameter.ParameterType.IsByRef)
            builder.Append(parameter.IsIn ? "in " : "ref ");

        var parameterType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()!
            : parameter.ParameterType;
        builder.Append(FormatType(parameterType));
        builder.Append(' ');
        builder.Append(parameter.Name ?? "arg");

        if (parameter.IsOptional)
        {
            builder.Append(" = ");
            builder.Append(FormatDefaultValue(parameter.DefaultValue));
        }

        return builder.ToString();
    }

    private static string FormatDefaultValue(object? value)
    {
        if (value is null || value == DBNull.Value || value == Missing.Value)
            return "default";
        if (value is string text)
            return "\"" + text + "\"";
        if (value is bool boolean)
            return boolean ? "true" : "false";
        return value.ToString() ?? "default";
    }
}
