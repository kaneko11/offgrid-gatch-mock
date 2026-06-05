using System.Reflection;
using System.Reflection.Emit;

namespace MiniMockito.Proxy.ClassProxy;

internal static class ClassProxyMethodEmitter
{
    private static readonly MethodInfo GetMethodFromHandleMethod = typeof(MethodBase)
        .GetMethod(nameof(MethodBase.GetMethodFromHandle), [typeof(RuntimeMethodHandle)])
        ?? throw new InvalidOperationException("MethodBase.GetMethodFromHandle could not be found.");

    private static readonly MethodInfo InvokeMethod = typeof(ClassProxyInvocationDispatcher)
        .GetMethod(
            nameof(ClassProxyInvocationDispatcher.Invoke),
            BindingFlags.Public | BindingFlags.Static,
            [typeof(object), typeof(MethodInfo), typeof(object?[])])
        ?? throw new InvalidOperationException("ClassProxyInvocationDispatcher.Invoke could not be found.");

    internal static void EmitOverride(TypeBuilder typeBuilder, MethodInfo method)
    {
        var parameters = method.GetParameters();
        var parameterTypes = parameters.Select(parameter => parameter.ParameterType).ToArray();
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
        il.Emit(OpCodes.Call, InvokeMethod);

        EmitReturn(il, method.ReturnType);

        typeBuilder.DefineMethodOverride(methodBuilder, method);
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
