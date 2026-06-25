using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Rewrites allowlisted <b>instance method</b> call sites (Phase 25) in the target assembly to static
/// wrapper methods that consult <see cref="ShimDispatcher.TryInvokeMethod"/> and fall back to the real
/// method when no shim is registered.
/// </summary>
/// <remarks>
/// Only the call site (inside the rewritten assembly) is changed; the method's declaring assembly is
/// never modified.  Works for non-virtual and virtual instance methods (call-site rewrite does not
/// depend on virtuality).  Generic methods (single type argument) are supported by generating a concrete
/// wrapper per call-site instantiation; the wrapper's return type is the caller-supplied substitute
/// interface closed with the call's type argument (so concrete-but-unconstructible return types such as
/// EF's <c>DbRawSqlQuery&lt;T&gt;</c> can be returned as <c>IEnumerable&lt;T&gt;</c> when consumed as such).
/// </remarks>
public static class MethodCallRewriter
{
    private const string WrapperClassName = "<ShimsMethodWrappers>";

    private static readonly string[] BclScopeNames =
    {
        "mscorlib", "System.Private.CoreLib", "netstandard", "System.Runtime",
    };

    /// <summary>Rewrites allowlisted instance-method call sites in <paramref name="module"/>.</summary>
    public static int Rewrite(ModuleDefinition module, RewriteOptions options, IList<string> diagnostics)
    {
        ThrowHelper.ThrowIfNull(module);
        ThrowHelper.ThrowIfNull(options);
        ThrowHelper.ThrowIfNull(diagnostics);

        if (options.MethodTargets.Count == 0)
            return 0;

        var targets = new Dictionary<string, MethodShimTarget>(StringComparer.Ordinal);
        foreach (var t in options.MethodTargets)
        {
            targets[t.DeclaringTypeFullName + "|" + t.MethodName] = t;
            diagnostics.Add($"Method shim target registered: {t.DeclaringTypeFullName}::{t.MethodName}.");
        }

        var tryInvoke = typeof(ShimDispatcher).GetMethod(nameof(ShimDispatcher.TryInvokeMethod));
        if (tryInvoke is null)
            throw new ShimRewriteException("ShimDispatcher.TryInvokeMethod(...) could not be found.");
        var importedTryInvoke = module.ImportReference(tryInvoke);

        var objectType = module.ImportReference(typeof(object));
        var objectArrayType = new ArrayType(objectType);

        var wrapperClass = GetOrCreateWrapperClass(module);
        var wrapperCache = new Dictionary<string, MethodDefinition>(StringComparer.Ordinal);

        var allMethods = EnumerateMethods(module, skipType: wrapperClass).ToList();
        var rewritten = 0;

        foreach (var method in allMethods)
        {
            if (!method.HasBody)
                continue;

            var instructions = method.Body.Instructions.ToArray();
            for (var i = 0; i < instructions.Length; i++)
            {
                var instruction = instructions[i];
                if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)
                    continue;
                if (instruction.Operand is not MethodReference callee || !callee.HasThis)
                    continue;

                var declName = RemoveGenericArity(callee.DeclaringType.FullName);
                if (!targets.TryGetValue(declName + "|" + callee.Name, out var target))
                    continue;

                var site = $"{method.DeclaringType.FullName}.{method.Name} IL_{instruction.Offset:X4}: {declName}::{callee.Name}";

                if (!ValidateCallee(callee, target, instructions, i, diagnostics, site, out var typeArgument))
                    continue;

                diagnostics.Add($"Method call site detected: {site}.");

                var wrapper = GetOrCreateWrapper(
                    module, wrapperClass, wrapperCache, callee, target, typeArgument,
                    importedTryInvoke, objectType, objectArrayType, declName);

                instruction.OpCode = OpCodes.Call;
                instruction.Operand = wrapper;
                rewritten++;
                diagnostics.Add($"Method call site rewritten: {site} -> ShimDispatcher.TryInvokeMethod.");
            }
        }

        return rewritten;
    }

    private static bool ValidateCallee(
        MethodReference callee,
        MethodShimTarget target,
        Instruction[] instructions,
        int index,
        IList<string> diagnostics,
        string site,
        out TypeReference? typeArgument)
    {
        typeArgument = null;

        // BCL declaring types are out of scope.
        var scope = callee.DeclaringType.Scope?.Name;
        if (scope is not null && BclScopeNames.Any(b => scope.StartsWith(b, StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics.Add($"Method call site skipped: {site}. Skipped reason: BCL declaring type is not supported.");
            return false;
        }

        // ref / out / generic-parameter parameters are out of scope.
        // (params object[] is allowed: at the IL level it is a normal object[] argument that is passed
        //  through to the shim via args — this is what enables EF's SqlQuery<T>(string, params object[]).)
        foreach (var p in callee.Parameters)
        {
            if (p.ParameterType is ByReferenceType)
            {
                diagnostics.Add($"Method call site skipped: {site}. Skipped reason: by-ref/out parameter is not supported.");
                return false;
            }

            if (p.ParameterType is GenericParameter || p.ParameterType.ContainsGenericParameter)
            {
                diagnostics.Add($"Method call site skipped: {site}. Skipped reason: generic parameter type is not supported.");
                return false;
            }
        }

        if (callee is GenericInstanceMethod generic)
        {
            if (generic.GenericArguments.Count != 1)
            {
                diagnostics.Add($"Method call site skipped: {site}. Skipped reason: only a single generic type argument is supported.");
                return false;
            }

            if (target.ReturnSubstituteInterface is null)
            {
                diagnostics.Add($"Method call site skipped: {site}. Skipped reason: a generic method requires a return interface (ReturnSubstituteInterface).");
                return false;
            }

            // Substituting the return type to an interface is only safe when the result is consumed by a
            // following call/callvirt (e.g. .ToList() / .FirstOrDefault()). Skip nops (Debug builds) when
            // looking at the consumer. Otherwise skip.
            var next = NextRealInstruction(instructions, index);
            if (next is null || (next.OpCode != OpCodes.Call && next.OpCode != OpCodes.Callvirt))
            {
                diagnostics.Add($"Method call site skipped: {site}. Skipped reason: generic result is not immediately consumed as an interface (return substitution unsafe).");
                return false;
            }

            typeArgument = generic.GenericArguments[0];
        }

        return true;
    }

    private static MethodDefinition GetOrCreateWrapper(
        ModuleDefinition module,
        TypeDefinition wrapperClass,
        Dictionary<string, MethodDefinition> cache,
        MethodReference callee,
        MethodShimTarget target,
        TypeReference? typeArgument,
        MethodReference importedTryInvoke,
        TypeReference objectType,
        ArrayType objectArrayType,
        string declName)
    {
        var isGeneric = typeArgument is not null;
        var cacheKey = declName + "::" + callee.Name + (isGeneric ? "<" + typeArgument!.FullName + ">" : string.Empty);
        if (cache.TryGetValue(cacheKey, out var existing))
            return existing;

        // Determine the wrapper return type.
        TypeReference? returnType;
        var isVoid = callee.ReturnType.FullName == "System.Void";
        if (isVoid)
        {
            returnType = module.ImportReference(typeof(void));
        }
        else if (isGeneric)
        {
            // Close the substitute interface with the call-site type argument.
            var importedOpen = module.ImportReference(target.ReturnSubstituteInterface!);
            var closed = new GenericInstanceType(importedOpen);
            closed.GenericArguments.Add(module.ImportReference(typeArgument!));
            returnType = closed;
        }
        else
        {
            returnType = module.ImportReference(callee.ReturnType);
        }

        var receiverType = module.ImportReference(callee.DeclaringType);
        var wrapperName = "__Shims_Call_" + Sanitize(declName) + "_" + callee.Name + (isGeneric ? "_" + Sanitize(typeArgument!.Name) : string.Empty);

        var wrapper = new MethodDefinition(
            wrapperName,
            MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig,
            returnType);

        // p0 = receiver, p1.. = method args
        wrapper.Parameters.Add(new ParameterDefinition("receiver", ParameterAttributes.None, receiverType));
        for (var i = 0; i < callee.Parameters.Count; i++)
        {
            wrapper.Parameters.Add(new ParameterDefinition(
                "p" + i, ParameterAttributes.None, module.ImportReference(callee.Parameters[i].ParameterType)));
        }

        var body = wrapper.Body;
        body.InitLocals = true;
        var argsVar = new VariableDefinition(objectArrayType);
        var resultVar = new VariableDefinition(objectType);
        body.Variables.Add(argsVar);
        body.Variables.Add(resultVar);

        var il = body.GetILProcessor();
        var paramCount = callee.Parameters.Count;

        // object[] args = new object[paramCount];
        EmitLdcI4(il, paramCount);
        il.Emit(OpCodes.Newarr, objectType);
        il.Emit(OpCodes.Stloc, argsVar);
        for (var i = 0; i < paramCount; i++)
        {
            var argParam = wrapper.Parameters[i + 1];
            il.Emit(OpCodes.Ldloc, argsVar);
            EmitLdcI4(il, i);
            il.Emit(OpCodes.Ldarg, argParam);
            if (IsValueType(argParam.ParameterType))
                il.Emit(OpCodes.Box, argParam.ParameterType);
            il.Emit(OpCodes.Stelem_Ref);
        }

        // ShimDispatcher.TryInvokeMethod(key, receiver, args, out result)
        il.Emit(OpCodes.Ldstr, declName + "::" + callee.Name);
        il.Emit(OpCodes.Ldarg, wrapper.Parameters[0]);
        if (IsValueType(receiverType))
            il.Emit(OpCodes.Box, receiverType);
        il.Emit(OpCodes.Ldloc, argsVar);
        il.Emit(OpCodes.Ldloca_S, resultVar);
        il.Emit(OpCodes.Call, importedTryInvoke);

        var fallbackFirst = Instruction.Create(OpCodes.Ldarg, wrapper.Parameters[0]);
        il.Emit(OpCodes.Brfalse, fallbackFirst);

        // shim hit
        if (!isVoid)
        {
            il.Emit(OpCodes.Ldloc, resultVar);
            if (IsValueType(returnType))
                il.Emit(OpCodes.Unbox_Any, returnType);
            else
                il.Emit(OpCodes.Castclass, returnType);
        }
        il.Emit(OpCodes.Ret);

        // fallback: real call
        il.Append(fallbackFirst); // ldarg receiver
        for (var i = 0; i < paramCount; i++)
            il.Emit(OpCodes.Ldarg, wrapper.Parameters[i + 1]);
        il.Emit(OpCodes.Callvirt, module.ImportReference(callee));
        il.Emit(OpCodes.Ret);

        wrapperClass.Methods.Add(wrapper);
        cache[cacheKey] = wrapper;
        return wrapper;
    }

    private static bool IsValueType(TypeReference type)
    {
        if (type.IsValueType)
            return true;
        try
        {
            return type.Resolve()?.IsValueType == true;
        }
        catch
        {
            return false;
        }
    }

    private static string Sanitize(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars);
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
            foreach (var m in EnumerateMethods(type))
                yield return m;
        }
    }

    private static IEnumerable<MethodDefinition> EnumerateMethods(TypeDefinition type)
    {
        foreach (var m in type.Methods)
            yield return m;
        foreach (var nested in type.NestedTypes)
            foreach (var m in EnumerateMethods(nested))
                yield return m;
    }

    private static string RemoveGenericArity(string fullName)
    {
        var tick = fullName.IndexOf('`');
        return tick < 0 ? fullName : fullName.Substring(0, tick);
    }

    private static Instruction? NextRealInstruction(Instruction[] instructions, int index)
    {
        for (var i = index + 1; i < instructions.Length; i++)
        {
            if (instructions[i].OpCode != OpCodes.Nop)
                return instructions[i];
        }

        return null;
    }
}

