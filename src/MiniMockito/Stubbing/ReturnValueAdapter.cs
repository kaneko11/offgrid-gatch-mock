using System.Reflection;
using MiniMockito.Exceptions;
using MiniMockito.Utilities;

namespace MiniMockito.Stubbing;

internal static class ReturnValueAdapter
{
    internal static object? ToReturnValue(object? value, Type returnType)
    {
        if (returnType == typeof(void))
        {
            return null;
        }

        if (returnType == typeof(Task))
        {
            return value is Task task ? task : Task.CompletedTask;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            if (value is not null && returnType.IsInstanceOfType(value))
            {
                return value;
            }

            var resultType = returnType.GetGenericArguments()[0];
            var converted = ConvertValue(value, resultType);
            return DefaultValueProvider.TaskFromResultMethod.MakeGenericMethod(resultType).Invoke(null, [converted]);
        }

        if (returnType == typeof(ValueTask))
        {
            if (value is ValueTask valueTask)
            {
                return valueTask;
            }

            if (value is Task task)
            {
                return new ValueTask(task);
            }

            return default(ValueTask);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            if (value is not null && returnType.IsInstanceOfType(value))
            {
                return value;
            }

            var resultType = returnType.GetGenericArguments()[0];
            var taskType = typeof(Task<>).MakeGenericType(resultType);
            if (value is not null && taskType.IsInstanceOfType(value))
            {
                return CreateValueTaskFromTask(resultType, value);
            }

            var converted = ConvertValue(value, resultType);
            return Activator.CreateInstance(returnType, converted);
        }

        return ConvertValue(value, returnType);
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        throw new StubbingException(
            $"Configured return value of type '{value.GetType().FullName}' cannot be returned as '{targetType.FullName}'.");
    }

    private static object CreateValueTaskFromTask(Type resultType, object task)
    {
        var taskType = typeof(Task<>).MakeGenericType(resultType);
        var constructor = typeof(ValueTask<>)
            .MakeGenericType(resultType)
            .GetConstructor([taskType]);

        if (constructor is null)
        {
            throw new StubbingException($"Could not create ValueTask<{resultType.Name}> from Task.");
        }

        return constructor.Invoke([task]);
    }
}
