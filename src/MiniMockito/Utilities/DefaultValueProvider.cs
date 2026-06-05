using System.Reflection;

namespace MiniMockito.Utilities;

internal static class DefaultValueProvider
{
    internal static readonly MethodInfo TaskFromResultMethod = typeof(Task)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method =>
            method.Name == nameof(Task.FromResult)
            && method.IsGenericMethodDefinition
            && method.GetParameters().Length == 1);

    internal static object? GetDefaultValue(Type returnType)
    {
        if (returnType == typeof(void))
        {
            return null;
        }

        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var defaultResult = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            return TaskFromResultMethod.MakeGenericMethod(resultType).Invoke(null, [defaultResult]);
        }

        if (returnType == typeof(ValueTask))
        {
            return default(ValueTask);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            return Activator.CreateInstance(returnType);
        }

        if (!returnType.IsValueType)
        {
            return null;
        }

        return Activator.CreateInstance(returnType);
    }
}
