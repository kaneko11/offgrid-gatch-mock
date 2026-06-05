using System.Reflection;
using MiniMockito.Exceptions;

namespace MiniMockito.Proxy.ClassProxy;

internal static class ClassProxyValidation
{
    internal static IReadOnlyList<MethodInfo> ValidateTarget(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        var supports = GetMethodSupports(targetType);

        if (!targetType.IsClass)
        {
            throw CreateException(targetType, null, ClassProxyUnsupportedReason.NotAClass, supports, "Use Mock.Of<T>() for interface mocks.");
        }

        if (!targetType.IsPublic && !targetType.IsNestedPublic)
        {
            throw CreateException(targetType, null, ClassProxyUnsupportedReason.NotPublic, supports, "Only public classes are supported by class proxy MVP.");
        }

        if (targetType.IsSealed)
        {
            throw CreateException(targetType, null, ClassProxyUnsupportedReason.SealedClass, supports, "Sealed classes cannot be inherited by a proxy.");
        }

        if (targetType.IsAbstract)
        {
            throw CreateException(targetType, null, ClassProxyUnsupportedReason.AbstractClass, supports, "Abstract classes are outside the v2 Phase 2 MVP.");
        }

        if (targetType.ContainsGenericParameters)
        {
            throw CreateException(targetType, null, ClassProxyUnsupportedReason.OpenGenericType, supports, "Use a closed non-open class type.");
        }

        if (FindParameterlessConstructor(targetType) is null)
        {
            throw CreateException(targetType, null, ClassProxyUnsupportedReason.NoParameterlessConstructor, supports, "Add a public or protected parameterless constructor.");
        }

        var supportedMethods = supports
            .Where(support => support.IsSupported)
            .Select(support => support.Method)
            .ToArray();

        if (supportedMethods.Length == 0)
        {
            throw CreateException(targetType, null, ClassProxyUnsupportedReason.NoSupportedVirtualMethods, supports, "Add a public virtual non-generic method.");
        }

        return supportedMethods;
    }

    internal static ConstructorInfo? FindParameterlessConstructor(Type targetType)
    {
        return targetType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(constructor =>
                constructor.GetParameters().Length == 0
                && (constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly));
    }

    internal static void EnsureMethodSupported(Type targetType, MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(method);

        if (!targetType.IsClass)
        {
            return;
        }

        var supports = GetMethodSupports(targetType);
        var support = supports.FirstOrDefault(item => MethodsRepresentSameSlot(item.Method, method))
            ?? new ClassProxyMethodSupport(method, false, GetUnsupportedReason(method));

        if (!support.IsSupported)
        {
            throw CreateException(targetType, method, support.Reason ?? ClassProxyUnsupportedReason.NonVirtualMethod, supports, "Only public virtual non-generic methods with normal parameters can be stubbed or verified.");
        }
    }

    internal static IReadOnlyList<ClassProxyMethodSupport> GetMethodSupports(Type targetType)
    {
        return targetType
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.DeclaringType != typeof(object))
            .Where(method => !method.IsSpecialName || method.Name.StartsWith("get_", StringComparison.Ordinal) || method.Name.StartsWith("set_", StringComparison.Ordinal))
            .GroupBy(GetMethodIdentity)
            .Select(group => group.First())
            .Select(method =>
            {
                var reason = GetUnsupportedReason(method);
                return new ClassProxyMethodSupport(method, reason is null, reason);
            })
            .OrderBy(support => support.Method.Name, StringComparer.Ordinal)
            .ToArray();
    }

    internal static ClassProxyException CreateException(
        Type targetType,
        MethodInfo? method,
        ClassProxyUnsupportedReason reason,
        IReadOnlyList<ClassProxyMethodSupport>? methodSupports = null,
        string? hint = null)
    {
        methodSupports ??= GetMethodSupports(targetType);
        var supported = methodSupports
            .Where(support => support.IsSupported)
            .Select(support => $"  {support.Describe()}")
            .DefaultIfEmpty("  <none>");
        var unsupported = methodSupports
            .Where(support => !support.IsSupported)
            .Select(support => $"  {support.Describe()}")
            .DefaultIfEmpty("  <none>");

        return new ClassProxyException(string.Join(
            Environment.NewLine,
            "Class proxy target is not supported.",
            $"Target class: {targetType.FullName}",
            $"Method: {(method is null ? "<type>" : $"{method.DeclaringType?.FullName}.{method.Name}")}",
            $"Reason: {reason}",
            "Supported methods:",
            string.Join(Environment.NewLine, supported),
            "Unsupported methods:",
            string.Join(Environment.NewLine, unsupported),
            $"Hint: {hint ?? "Use a public non-sealed class with a parameterless constructor and public virtual methods."}"));
    }

    private static ClassProxyUnsupportedReason? GetUnsupportedReason(MethodInfo method)
    {
        if (method.DeclaringType == typeof(object))
        {
            return ClassProxyUnsupportedReason.ObjectMethod;
        }

        if (method.IsStatic)
        {
            return ClassProxyUnsupportedReason.StaticMethod;
        }

        if (method.IsPrivate)
        {
            return ClassProxyUnsupportedReason.PrivateMethod;
        }

        if (!method.IsPublic)
        {
            return ClassProxyUnsupportedReason.NonPublicMethod;
        }

        if (!method.IsVirtual)
        {
            return ClassProxyUnsupportedReason.NonVirtualMethod;
        }

        if (method.IsFinal)
        {
            return ClassProxyUnsupportedReason.FinalMethod;
        }

        if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
        {
            return ClassProxyUnsupportedReason.GenericMethod;
        }

        if (method.GetParameters().Any(parameter => parameter.ParameterType.IsByRef || parameter.IsOut))
        {
            return ClassProxyUnsupportedReason.RefOrOutParameter;
        }

        return null;
    }

    private static string GetMethodIdentity(MethodInfo method)
    {
        return $"{method.DeclaringType?.FullName}.{method.Name}:{string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.FullName))}";
    }

    private static bool MethodsRepresentSameSlot(MethodInfo left, MethodInfo right)
    {
        return left == right || left.GetBaseDefinition() == right.GetBaseDefinition();
    }
}
