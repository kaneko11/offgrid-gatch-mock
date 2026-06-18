using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Rewrites supported <c>newobj</c> instructions to <see cref="ShimDispatcher.New{T}"/>
/// or <see cref="ShimDispatcher.NewWithArgs{T}"/> depending on constructor arity.
/// </summary>
public static class NewObjRewriter
{
    private const string WrapperClassName = "<ShimsWrappers>";

    /// <summary>
    /// Rewrites supported <c>newobj</c> instructions in the supplied module.
    /// </summary>
    public static int Rewrite(ModuleDefinition module, RewriteOptions options, IList<string> diagnostics)
    {
        ThrowHelper.ThrowIfNull(module);
        ThrowHelper.ThrowIfNull(options);
        ThrowHelper.ThrowIfNull(diagnostics);

        var internalTypeNames = options.TargetTypes
            .Select(type => type.FullName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

        var externalTypeNames = options.ExternalTargetTypes
            .Select(type => type.FullName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

        var allTargetNames = new HashSet<string>(internalTypeNames, StringComparer.Ordinal);
        allTargetNames.UnionWith(externalTypeNames);

        if (allTargetNames.Count == 0)
        {
            diagnostics.Add("No allowlisted target types were provided. No newobj call sites were rewritten.");
            return 0;
        }

        foreach (var externalName in externalTypeNames)
        {
            diagnostics.Add($"External target registered: {externalName}.");
        }

        var dispatcherNewMethod = typeof(ShimDispatcher).GetMethod(nameof(ShimDispatcher.New), Type.EmptyTypes);
        if (dispatcherNewMethod is null)
            throw new ShimRewriteException("ShimDispatcher.New<T>() could not be found.");

        var dispatcherNewWithArgsMethod = typeof(ShimDispatcher).GetMethod(
            nameof(ShimDispatcher.NewWithArgs), [typeof(object[])]);
        if (dispatcherNewWithArgsMethod is null)
            throw new ShimRewriteException("ShimDispatcher.NewWithArgs<T>(object?[]) could not be found.");

        var importedDispatcherNewMethod = module.ImportReference(dispatcherNewMethod);
        var importedDispatcherNewWithArgsMethod = module.ImportReference(dispatcherNewWithArgsMethod);

        // Create wrapper class upfront so wrapper methods can be added to it during iteration.
        var wrapperClass = GetOrCreateWrapperClass(module);
        var wrapperMethodCache = new Dictionary<string, MethodDefinition>(StringComparer.Ordinal);

        // Materialize method list before adding wrapper methods to avoid modifying the collection.
        var allMethods = EnumerateMethods(module, skipType: wrapperClass).ToList();

        var rewrittenCount = 0;

        foreach (var method in allMethods)
        {
            if (!method.HasBody)
                continue;

            // Snapshot instructions; in-place property modifications are safe.
            var instructions = method.Body.Instructions.ToArray();

            foreach (var instruction in instructions)
            {
                if (instruction.OpCode != OpCodes.Newobj || instruction.Operand is not MethodReference constructor)
                    continue;

                var declaringType = constructor.DeclaringType;
                var declaringTypeName = RemoveGenericArity(declaringType.FullName);
                if (!allTargetNames.Contains(declaringTypeName))
                    continue;

                var isExternal = externalTypeNames.Contains(declaringTypeName);

                if (!ValidateDeclaringType(declaringType, diagnostics, method, instruction))
                    continue;

                if (isExternal)
                {
                    var externalAssembly = declaringType.Scope?.Name ?? "<unknown>";
                    diagnostics.Add(
                        $"External newobj detected: {method.DeclaringType.FullName}.{method.Name} " +
                        $"IL_{instruction.Offset:X4}: new {declaringType.FullName}() from assembly {externalAssembly}.");
                }

                if (constructor.Parameters.Count == 0)
                {
                    var replacement = new GenericInstanceMethod(importedDispatcherNewMethod);
                    replacement.GenericArguments.Add(module.ImportReference(declaringType));
                    instruction.OpCode = OpCodes.Call;
                    instruction.Operand = replacement;
                    rewrittenCount++;
                    diagnostics.Add(
                        $"Rewrote {method.DeclaringType.FullName}.{method.Name} IL_{instruction.Offset:X4}: " +
                        $"new {declaringType.FullName}() -> ShimDispatcher.New<{declaringType.FullName}>().");
                    if (isExternal)
                    {
                        diagnostics.Add(
                            $"External newobj rewritten: {declaringType.FullName} TypeReference imported; " +
                            $"assembly reference '{declaringType.Scope?.Name ?? "<unknown>"}' preserved.");
                    }
                }
                else
                {
                    if (!TryGetOrCreateWrapperMethod(
                            module, wrapperClass, wrapperMethodCache, constructor, declaringType,
                            importedDispatcherNewWithArgsMethod, diagnostics, method, instruction,
                            out var wrapperMethod))
                    {
                        continue;
                    }

                    instruction.OpCode = OpCodes.Call;
                    instruction.Operand = wrapperMethod;
                    rewrittenCount++;

                    var paramSig = string.Join(", ", constructor.Parameters.Select(p => p.ParameterType.Name));
                    diagnostics.Add(
                        $"Rewrote {method.DeclaringType.FullName}.{method.Name} IL_{instruction.Offset:X4}: " +
                        $"new {declaringType.FullName}({paramSig}) -> ShimDispatcher.NewWithArgs<{declaringType.FullName}>(args).");
                    if (isExternal)
                    {
                        diagnostics.Add(
                            $"External newobj rewritten: {declaringType.FullName} TypeReference imported; " +
                            $"assembly reference '{declaringType.Scope?.Name ?? "<unknown>"}' preserved.");
                    }
                }
            }
        }

        return rewrittenCount;
    }

    private static bool ValidateDeclaringType(
        TypeReference declaringType,
        IList<string> diagnostics,
        MethodDefinition callingMethod,
        Instruction instruction)
    {
        if (declaringType is GenericInstanceType || declaringType.HasGenericParameters)
        {
            diagnostics.Add(
                $"Skipped {callingMethod.DeclaringType.FullName}.{callingMethod.Name} " +
                $"IL_{instruction.Offset:X4}: generic target types are not supported.");
            return false;
        }

        TypeDefinition? resolvedType;
        try
        {
            resolvedType = declaringType.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            resolvedType = null;
        }

        if (resolvedType is null)
        {
            diagnostics.Add(
                $"Skipped {callingMethod.DeclaringType.FullName}.{callingMethod.Name} " +
                $"IL_{instruction.Offset:X4}: target type could not be resolved.");
            return false;
        }

        if (!resolvedType.IsPublic || !resolvedType.IsClass)
        {
            diagnostics.Add(
                $"Skipped {callingMethod.DeclaringType.FullName}.{callingMethod.Name} " +
                $"IL_{instruction.Offset:X4}: target type must be a public class.");
            return false;
        }

        return true;
    }

    private static bool TryGetOrCreateWrapperMethod(
        ModuleDefinition module,
        TypeDefinition wrapperClass,
        Dictionary<string, MethodDefinition> wrapperMethodCache,
        MethodReference constructor,
        TypeReference declaringType,
        MethodReference importedNewWithArgsMethod,
        IList<string> diagnostics,
        MethodDefinition callingMethod,
        Instruction instruction,
        out MethodDefinition? wrapperMethod)
    {
        foreach (var param in constructor.Parameters)
        {
            var paramType = param.ParameterType;

            if (paramType is ByReferenceType)
            {
                diagnostics.Add(
                    $"Skipped {callingMethod.DeclaringType.FullName}.{callingMethod.Name} " +
                    $"IL_{instruction.Offset:X4}: by-ref parameter is not supported. " +
                    $"Constructor: {constructor.FullName}");
                wrapperMethod = null;
                return false;
            }

            if (paramType is GenericParameter)
            {
                diagnostics.Add(
                    $"Skipped {callingMethod.DeclaringType.FullName}.{callingMethod.Name} " +
                    $"IL_{instruction.Offset:X4}: generic parameter is not supported. " +
                    $"Constructor: {constructor.FullName}");
                wrapperMethod = null;
                return false;
            }

            if (param.HasCustomAttributes && param.CustomAttributes.Any(a =>
                    a.AttributeType.FullName == "System.ParamArrayAttribute"))
            {
                diagnostics.Add(
                    $"Skipped {callingMethod.DeclaringType.FullName}.{callingMethod.Name} " +
                    $"IL_{instruction.Offset:X4}: params parameter is not supported. " +
                    $"Constructor: {constructor.FullName}");
                wrapperMethod = null;
                return false;
            }
        }

        var key = GetConstructorKey(constructor);
        if (wrapperMethodCache.TryGetValue(key, out wrapperMethod))
            return true;

        wrapperMethod = GenerateWrapperMethod(module, declaringType, constructor, importedNewWithArgsMethod);
        wrapperClass.Methods.Add(wrapperMethod);
        wrapperMethodCache[key] = wrapperMethod;
        return true;
    }

    private static string GetConstructorKey(MethodReference constructor)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(constructor.DeclaringType.FullName);
        sb.Append('(');
        for (int i = 0; i < constructor.Parameters.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(constructor.Parameters[i].ParameterType.FullName);
        }
        sb.Append(')');
        return sb.ToString();
    }

    private static MethodDefinition GenerateWrapperMethod(
        ModuleDefinition module,
        TypeReference declaringType,
        MethodReference constructor,
        MethodReference importedNewWithArgsMethod)
    {
        var returnType = module.ImportReference(declaringType);
        var methodName = GetWrapperMethodName(declaringType, constructor);

        var method = new MethodDefinition(
            methodName,
            MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig,
            returnType);

        for (int i = 0; i < constructor.Parameters.Count; i++)
        {
            var origParam = constructor.Parameters[i];
            method.Parameters.Add(new ParameterDefinition(
                $"p{i}",
                ParameterAttributes.None,
                module.ImportReference(origParam.ParameterType)));
        }

        method.Body.InitLocals = false;
        var il = method.Body.GetILProcessor();
        var objectType = module.ImportReference(typeof(object));
        var paramCount = constructor.Parameters.Count;

        // object?[] args = new object[paramCount]
        EmitLdcI4(il, paramCount);
        il.Emit(OpCodes.Newarr, objectType);

        for (int i = 0; i < paramCount; i++)
        {
            var param = method.Parameters[i];
            il.Emit(OpCodes.Dup);
            EmitLdcI4(il, i);
            il.Emit(OpCodes.Ldarg, param);

            // Box value types before storing into object?[]
            bool isValueType = false;
            try
            {
                var resolved = param.ParameterType.Resolve();
                isValueType = resolved?.IsValueType == true;
            }
            catch { /* unresolvable — treat as reference type */ }

            if (isValueType)
                il.Emit(OpCodes.Box, param.ParameterType);

            il.Emit(OpCodes.Stelem_Ref);
        }

        var genericMethod = new GenericInstanceMethod(importedNewWithArgsMethod);
        genericMethod.GenericArguments.Add(returnType);
        il.Emit(OpCodes.Call, genericMethod);
        il.Emit(OpCodes.Ret);

        return method;
    }

    private static string GetWrapperMethodName(TypeReference declaringType, MethodReference constructor)
    {
        var typeName = declaringType.Name;
        var tickIndex = typeName.IndexOf('`');
        if (tickIndex >= 0) typeName = typeName.Substring(0, tickIndex);

        if (constructor.Parameters.Count == 0)
            return $"__Shims_New_{typeName}";

        var paramNames = string.Join("_", constructor.Parameters.Select(p =>
        {
            var name = p.ParameterType.Name;
            var tick = name.IndexOf('`');
            return tick >= 0 ? name.Substring(0, tick) : name;
        }));

        return $"__Shims_New_{typeName}_{paramNames}";
    }

    private static TypeDefinition GetOrCreateWrapperClass(ModuleDefinition module)
    {
        var existing = module.Types.FirstOrDefault(t => t.Name == WrapperClassName);
        if (existing is not null)
            return existing;

        var wrapperClass = new TypeDefinition(
            string.Empty,
            WrapperClassName,
            TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed,
            module.ImportReference(typeof(object)));
        module.Types.Add(wrapperClass);
        return wrapperClass;
    }

    private static void EmitLdcI4(ILProcessor il, int value)
    {
        switch (value)
        {
            case 0: il.Emit(OpCodes.Ldc_I4_0); break;
            case 1: il.Emit(OpCodes.Ldc_I4_1); break;
            case 2: il.Emit(OpCodes.Ldc_I4_2); break;
            case 3: il.Emit(OpCodes.Ldc_I4_3); break;
            case 4: il.Emit(OpCodes.Ldc_I4_4); break;
            case 5: il.Emit(OpCodes.Ldc_I4_5); break;
            case 6: il.Emit(OpCodes.Ldc_I4_6); break;
            case 7: il.Emit(OpCodes.Ldc_I4_7); break;
            case 8: il.Emit(OpCodes.Ldc_I4_8); break;
            default:
                if (value >= -128 && value <= 127)
                    il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
                else
                    il.Emit(OpCodes.Ldc_I4, value);
                break;
        }
    }

    private static IEnumerable<MethodDefinition> EnumerateMethods(ModuleDefinition module, TypeDefinition? skipType = null)
    {
        foreach (var type in module.Types)
        {
            if (skipType is not null && type == skipType)
                continue;
            foreach (var method in EnumerateMethods(type))
                yield return method;
        }
    }

    private static IEnumerable<MethodDefinition> EnumerateMethods(TypeDefinition type)
    {
        foreach (var method in type.Methods)
            yield return method;
        foreach (var nestedType in type.NestedTypes)
            foreach (var method in EnumerateMethods(nestedType))
                yield return method;
    }

    private static string RemoveGenericArity(string fullName)
    {
        var tickIndex = fullName.IndexOf('`');
        return tickIndex < 0 ? fullName : fullName.Substring(0, tickIndex);
    }
}
