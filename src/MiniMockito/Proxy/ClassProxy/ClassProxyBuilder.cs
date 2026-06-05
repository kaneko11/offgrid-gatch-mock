using System.Reflection;
using System.Reflection.Emit;

namespace MiniMockito.Proxy.ClassProxy;

internal sealed class ClassProxyBuilder
{
    private static readonly AssemblyBuilder AssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
        new AssemblyName("MiniMockito.DynamicClassProxies"),
        AssemblyBuilderAccess.Run);

    private static readonly ModuleBuilder ModuleBuilder = AssemblyBuilder.DefineDynamicModule("MiniMockito.DynamicClassProxies");
    private static int _typeId;

    internal Type Build(Type targetType)
    {
        var supportedMethods = ClassProxyValidation.ValidateTarget(targetType);
        var typeName = $"MiniMockito.DynamicClassProxies.{SanitizeTypeName(targetType)}Proxy{Interlocked.Increment(ref _typeId)}";
        var typeBuilder = ModuleBuilder.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Class,
            targetType);

        DefineParameterlessConstructor(typeBuilder, targetType);

        foreach (var method in supportedMethods)
        {
            ClassProxyMethodEmitter.EmitOverride(typeBuilder, method);
        }

        return typeBuilder.CreateTypeInfo()!.AsType();
    }

    private static void DefineParameterlessConstructor(TypeBuilder typeBuilder, Type targetType)
    {
        var baseConstructor = ClassProxyValidation.FindParameterlessConstructor(targetType)
            ?? throw ClassProxyValidation.CreateException(
                targetType,
                null,
                ClassProxyUnsupportedReason.NoParameterlessConstructor,
                hint: "Add a public or protected parameterless constructor.");

        var constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            Type.EmptyTypes);
        var il = constructorBuilder.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, baseConstructor);
        il.Emit(OpCodes.Ret);
    }

    private static string SanitizeTypeName(Type targetType)
    {
        var fullName = targetType.FullName ?? targetType.Name;
        var invalidCharacters = new[] { '.', '+', '`', '[', ']', ',', ' ' };

        foreach (var invalidCharacter in invalidCharacters)
        {
            fullName = fullName.Replace(invalidCharacter, '_');
        }

        return fullName;
    }
}
