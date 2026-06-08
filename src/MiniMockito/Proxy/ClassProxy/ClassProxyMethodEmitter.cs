using System.Reflection;
using System.Reflection.Emit;

namespace MiniMockito.Proxy.ClassProxy;

internal static class ClassProxyMethodEmitter
{
    private static int _baseInvokerId;

    private static readonly MethodInfo GetMethodFromHandleMethod = typeof(MethodBase)
        .GetMethod(nameof(MethodBase.GetMethodFromHandle), [typeof(RuntimeMethodHandle)])
        ?? throw new InvalidOperationException("MethodBase.GetMethodFromHandle could not be found.");

    private static readonly MethodInfo InvokeMethod = typeof(ClassProxyInvocationDispatcher)
        .GetMethod(
            nameof(ClassProxyInvocationDispatcher.Invoke),
            BindingFlags.Public | BindingFlags.Static,
            [typeof(object), typeof(MethodInfo), typeof(object?[]), typeof(string)])
        ?? throw new InvalidOperationException("ClassProxyInvocationDispatcher.Invoke could not be found.");

    internal static void EmitOverride(TypeBuilder typeBuilder, MethodInfo method)
    {
        var parameters = method.GetParameters();
        var parameterTypes = parameters.Select(parameter => parameter.ParameterType).ToArray();
        var baseInvokerName = EmitBaseInvoker(typeBuilder, method, parameters);
        var attributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig;
        if (method.IsSpecialName)
        {
            attributes |= MethodAttributes.SpecialName;
        }

        var methodBuilder = typeBuilder.DefineMethod(
            method.Name,
            attributes,
            method.ReturnType,
            parameterTypes);

        for (var index = 0; index < parameters.Length; index++)
        {
            methodBuilder.DefineParameter(index + 1, parameters[index].Attributes, parameters[index].Name);
        }

        var il = methodBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldtoken, method);
        il.Emit(OpCodes.Call, GetMethodFromHandleMethod);
        il.Emit(OpCodes.Castclass, typeof(MethodInfo));
        EmitArgumentArray(il, parameters);
        il.Emit(OpCodes.Ldstr, baseInvokerName);
        il.Emit(OpCodes.Call, InvokeMethod);

        EmitReturn(il, method.ReturnType);

        typeBuilder.DefineMethodOverride(methodBuilder, method);
    }

    private static string EmitBaseInvoker(TypeBuilder typeBuilder, MethodInfo method, IReadOnlyList<ParameterInfo> parameters)
    {
        var methodName = $"__MiniMockito_CallBase_{method.Name}_{Interlocked.Increment(ref _baseInvokerId)}";
        var methodBuilder = typeBuilder.DefineMethod(
            methodName,
            MethodAttributes.Private | MethodAttributes.HideBySig,
            typeof(object),
            [typeof(object?[])]);
        var il = methodBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        for (var index = 0; index < parameters.Count; index++)
        {
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4, index);
            il.Emit(OpCodes.Ldelem_Ref);

            var parameterType = parameters[index].ParameterType;
            if (parameterType.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, parameterType);
            }
            else
            {
                il.Emit(OpCodes.Castclass, parameterType);
            }
        }

        il.Emit(OpCodes.Call, method);
        if (method.ReturnType == typeof(void))
        {
            il.Emit(OpCodes.Ldnull);
        }
        else if (method.ReturnType.IsValueType)
        {
            il.Emit(OpCodes.Box, method.ReturnType);
        }

        il.Emit(OpCodes.Ret);
        return methodName;
    }

    private static void EmitArgumentArray(ILGenerator il, IReadOnlyList<ParameterInfo> parameters)
    {
        il.Emit(OpCodes.Ldc_I4, parameters.Count);
        il.Emit(OpCodes.Newarr, typeof(object));

        for (var index = 0; index < parameters.Count; index++)
        {
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldc_I4, index);
            il.Emit(OpCodes.Ldarg, index + 1);

            var parameterType = parameters[index].ParameterType;
            if (parameterType.IsValueType)
            {
                il.Emit(OpCodes.Box, parameterType);
            }

            il.Emit(OpCodes.Stelem_Ref);
        }
    }

    private static void EmitReturn(ILGenerator il, Type returnType)
    {
        if (returnType == typeof(void))
        {
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
            return;
        }

        if (returnType.IsValueType)
        {
            il.Emit(OpCodes.Unbox_Any, returnType);
        }
        else
        {
            il.Emit(OpCodes.Castclass, returnType);
        }

        il.Emit(OpCodes.Ret);
    }
}
