using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilMethodAttrs = Mono.Cecil.MethodAttributes;
using CecilParamAttrs = Mono.Cecil.ParameterAttributes;
using CecilTypeAttrs = Mono.Cecil.TypeAttributes;
using MetadataType = Mono.Cecil.MetadataType;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Rewrites allowlisted static <c>call</c> instructions to wrapper methods that delegate to
/// <see cref="StaticShimDispatcher"/>, with a fallback to the original static call.
/// </summary>
/// <remarks>
/// <b>Pattern (non-void):</b>
/// <code>
/// // Before: call static DateTime StaticClock::Now()
/// // After:  call static DateTime &lt;ShimsStaticWrappers&gt;::__Shims_Static_StaticClock_Now()
/// //
/// // Generated wrapper:
/// //   if (StaticShimDispatcher.TryInvoke&lt;DateTime&gt;("...StaticClock","Now",[],[],out r)) return r;
/// //   return StaticClock.Now();  // fallback — wrapper class is excluded from scanning
/// </code>
/// <para>
/// BCL types (System.*, mscorlib, etc.) and generic methods are never rewritten.
/// </para>
/// </remarks>
public static class StaticCallRewriter
{
    private const string StaticWrapperClassName = "<ShimsStaticWrappers>";

    private static readonly HashSet<string> BclScopeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Private.CoreLib",
        "System.Runtime",
        "mscorlib",
        "netstandard",
        "System.Core",
        "System.Collections",
        "System.Linq",
        "System.IO",
        "System.Threading",
        "System.Text",
        "System.Diagnostics",
        "System.Reflection",
        "System.Net",
    };

    /// <summary>
    /// Rewrites static call sites in <paramref name="module"/> that target allowlisted types.
    /// </summary>
    /// <returns>The total number of rewritten call sites.</returns>
    public static StaticRewriteResult Rewrite(
        ModuleDefinition module,
        RewriteOptions options,
        IList<string> diagnostics)
    {
        ThrowHelper.ThrowIfNull(module);
        ThrowHelper.ThrowIfNull(options);
        ThrowHelper.ThrowIfNull(diagnostics);

        var targetTypeNames = options.StaticTargetTypes
            .Select(t => t.FullName)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal);

        if (targetTypeNames.Count == 0)
        {
            diagnostics.Add("No allowlisted static target types were provided. No static call sites were rewritten.");
            return new StaticRewriteResult(0, [], [], []);
        }

        var tryInvokeMethod = typeof(StaticShimDispatcher)
            .GetMethod(nameof(StaticShimDispatcher.TryInvoke),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var tryInvokeVoidMethod = typeof(StaticShimDispatcher)
            .GetMethod(nameof(StaticShimDispatcher.TryInvokeVoid),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (tryInvokeMethod is null)
            throw new ShimRewriteException("StaticShimDispatcher.TryInvoke<TResult>() could not be found.");
        if (tryInvokeVoidMethod is null)
            throw new ShimRewriteException("StaticShimDispatcher.TryInvokeVoid() could not be found.");

        var importedTryInvoke = module.ImportReference(tryInvokeMethod);
        var importedTryInvokeVoid = module.ImportReference(tryInvokeVoidMethod);

        var getTypeFromHandleMethodInfo = typeof(Type)
            .GetMethod("GetTypeFromHandle", [typeof(System.RuntimeTypeHandle)]);
        if (getTypeFromHandleMethodInfo is null)
            throw new ShimRewriteException("Type.GetTypeFromHandle(RuntimeTypeHandle) could not be found.");
        var importedGetTypeFromHandle = module.ImportReference(getTypeFromHandleMethodInfo);

        var objectType = module.ImportReference(typeof(object));
        var typeType = module.ImportReference(typeof(Type));

        var wrapperClass = GetOrCreateStaticWrapperClass(module);
        var wrapperCache = new Dictionary<string, MethodDefinition>(StringComparer.Ordinal);

        // Materialize method list before adding wrapper methods.
        var allMethods = EnumerateMethods(module, skipType: wrapperClass).ToList();

        var rewrittenSites = new List<StaticCallSite>();
        var skippedSites = new List<StaticCallSite>();

        foreach (var method in allMethods)
        {
            if (!method.HasBody) continue;

            var instructions = method.Body.Instructions.ToArray();

            foreach (var instr in instructions)
            {
                if (instr.OpCode != OpCodes.Call) continue;
                if (instr.Operand is not MethodReference targetMethod) continue;

                var declTypeFullName = RemoveGenericArity(targetMethod.DeclaringType.FullName ?? string.Empty);
                if (!targetTypeNames.Contains(declTypeFullName)) continue;

                // Resolve to check IsStatic.
                MethodDefinition? resolved;
                try { resolved = targetMethod.Resolve(); }
                catch { resolved = null; }

                if (resolved is null || !resolved.IsStatic)
                    continue;

                var paramTypeNames = targetMethod.Parameters
                    .Select(p => p.ParameterType.Name).ToArray();
                var returnTypeName = targetMethod.ReturnType.FullName ?? targetMethod.ReturnType.Name;
                bool isVoidReturn = resolved.ReturnType.MetadataType == MetadataType.Void;

                // BCL check
                if (IsBclScope(targetMethod.DeclaringType.Scope?.Name))
                {
                    var skip = new StaticCallSite(
                        method.DeclaringType.FullName, method.Name, instr.Offset,
                        declTypeFullName, targetMethod.Name, paramTypeNames,
                        returnTypeName, isVoidReturn, wasRewritten: false,
                        "BCL type — not rewritten in Phase 14.");
                    skippedSites.Add(skip);
                    diagnostics.Add($"Skipped BCL static call at {method.DeclaringType.Name}.{method.Name} IL_{instr.Offset:X4}: {targetMethod.FullName}");
                    continue;
                }

                // Generic method / type check
                if (targetMethod.HasGenericParameters
                    || targetMethod is GenericInstanceMethod
                    || targetMethod.DeclaringType.HasGenericParameters)
                {
                    var skip = new StaticCallSite(
                        method.DeclaringType.FullName, method.Name, instr.Offset,
                        declTypeFullName, targetMethod.Name, paramTypeNames,
                        returnTypeName, isVoidReturn, wasRewritten: false,
                        "Generic method or generic declaring type — not supported in Phase 14.");
                    skippedSites.Add(skip);
                    diagnostics.Add($"Skipped generic static call at {method.DeclaringType.Name}.{method.Name} IL_{instr.Offset:X4}: {targetMethod.FullName}");
                    continue;
                }

                // By-ref / generic parameter check
                bool hasUnsupportedParam = false;
                foreach (var param in targetMethod.Parameters)
                {
                    if (param.ParameterType is ByReferenceType || param.ParameterType is GenericParameter)
                    {
                        hasUnsupportedParam = true;
                        break;
                    }
                }
                if (hasUnsupportedParam)
                {
                    var skip = new StaticCallSite(
                        method.DeclaringType.FullName, method.Name, instr.Offset,
                        declTypeFullName, targetMethod.Name, paramTypeNames,
                        returnTypeName, isVoidReturn, wasRewritten: false,
                        "By-ref or generic parameter — not supported in Phase 14.");
                    skippedSites.Add(skip);
                    diagnostics.Add($"Skipped by-ref param static call at {method.DeclaringType.Name}.{method.Name} IL_{instr.Offset:X4}: {targetMethod.FullName}");
                    continue;
                }

                // Get or create wrapper
                var wrapperKey = BuildWrapperKey(targetMethod);
                if (!wrapperCache.TryGetValue(wrapperKey, out var wrapperMethod))
                {
                    wrapperMethod = GenerateStaticWrapper(
                        module, targetMethod, resolved, isVoidReturn,
                        importedTryInvoke, importedTryInvokeVoid,
                        importedGetTypeFromHandle, objectType, typeType);

                    wrapperClass.Methods.Add(wrapperMethod);
                    wrapperCache[wrapperKey] = wrapperMethod;
                    diagnostics.Add($"Generated wrapper: {wrapperMethod.Name} for {targetMethod.FullName}");
                }

                instr.Operand = wrapperMethod;

                var site = new StaticCallSite(
                    method.DeclaringType.FullName, method.Name, instr.Offset,
                    declTypeFullName, targetMethod.Name, paramTypeNames,
                    returnTypeName, isVoidReturn, wasRewritten: true);
                rewrittenSites.Add(site);
                diagnostics.Add(
                    $"Rewrote {method.DeclaringType.Name}.{method.Name} IL_{instr.Offset:X4}: " +
                    $"{targetMethod.FullName} -> {wrapperMethod.Name}");
            }
        }

        return new StaticRewriteResult(
            rewrittenSites.Count, rewrittenSites, skippedSites, [.. diagnostics]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Wrapper generation
    // ─────────────────────────────────────────────────────────────────────────

    private static MethodDefinition GenerateStaticWrapper(
        ModuleDefinition module,
        MethodReference targetMethod,
        MethodDefinition resolved,
        bool isVoid,
        MethodReference importedTryInvoke,
        MethodReference importedTryInvokeVoid,
        MethodReference importedGetTypeFromHandle,
        TypeReference objectType,
        TypeReference typeType)
    {
        var returnTypeRef = module.ImportReference(targetMethod.ReturnType);
        var wrapperName = BuildWrapperMethodName(targetMethod);
        var paramCount = targetMethod.Parameters.Count;

        var attrs = CecilMethodAttrs.Assembly | CecilMethodAttrs.Static | CecilMethodAttrs.HideBySig;
        var wrapper = new MethodDefinition(wrapperName, attrs, returnTypeRef);

        for (int i = 0; i < paramCount; i++)
        {
            var p = targetMethod.Parameters[i];
            wrapper.Parameters.Add(new ParameterDefinition(
                $"p{i}", CecilParamAttrs.None, module.ImportReference(p.ParameterType)));
        }

        wrapper.Body.InitLocals = true;
        var il = wrapper.Body.GetILProcessor();

        var declaringTypeFullName = targetMethod.DeclaringType.FullName
            ?? targetMethod.DeclaringType.Name;

        if (isVoid)
            GenerateVoidWrapperBody(il, module, wrapper, targetMethod, declaringTypeFullName,
                importedTryInvokeVoid, importedGetTypeFromHandle, objectType, typeType, paramCount);
        else
            GenerateValueWrapperBody(il, module, wrapper, targetMethod, declaringTypeFullName,
                importedTryInvoke, importedGetTypeFromHandle, objectType, typeType,
                returnTypeRef, paramCount);

        return wrapper;
    }

    private static void GenerateValueWrapperBody(
        ILProcessor il,
        ModuleDefinition module,
        MethodDefinition wrapper,
        MethodReference targetMethod,
        string declaringTypeFullName,
        MethodReference importedTryInvoke,
        MethodReference importedGetTypeFromHandle,
        TypeReference objectType,
        TypeReference typeType,
        TypeReference returnTypeRef,
        int paramCount)
    {
        // .locals init (ReturnType result)
        var resultVar = new VariableDefinition(returnTypeRef);
        wrapper.Body.Variables.Add(resultVar);

        // ldstr declaringTypeFullName
        il.Emit(OpCodes.Ldstr, declaringTypeFullName);
        // ldstr methodName
        il.Emit(OpCodes.Ldstr, targetMethod.Name);

        // Type[] paramTypes
        EmitTypeArray(il, wrapper, importedGetTypeFromHandle, typeType, paramCount);

        // object?[] args
        EmitArgsArray(il, wrapper, objectType, paramCount);

        // ldloca.s result
        il.Emit(OpCodes.Ldloca_S, resultVar);

        // call bool TryInvoke<TResult>(string, string, Type[], object?[], out TResult)
        var genericTryInvoke = new GenericInstanceMethod(importedTryInvoke);
        genericTryInvoke.GenericArguments.Add(returnTypeRef);
        il.Emit(OpCodes.Call, genericTryInvoke);

        // brfalse FALLBACK
        var fallbackNop = il.Create(OpCodes.Nop);
        il.Emit(OpCodes.Brfalse, fallbackNop);

        // return shimmed result
        il.Emit(OpCodes.Ldloc, resultVar);
        il.Emit(OpCodes.Ret);

        // FALLBACK: call real method
        il.Append(fallbackNop);
        for (int i = 0; i < paramCount; i++)
            il.Emit(OpCodes.Ldarg, wrapper.Parameters[i]);
        il.Emit(OpCodes.Call, module.ImportReference(targetMethod));
        il.Emit(OpCodes.Ret);
    }

    private static void GenerateVoidWrapperBody(
        ILProcessor il,
        ModuleDefinition module,
        MethodDefinition wrapper,
        MethodReference targetMethod,
        string declaringTypeFullName,
        MethodReference importedTryInvokeVoid,
        MethodReference importedGetTypeFromHandle,
        TypeReference objectType,
        TypeReference typeType,
        int paramCount)
    {
        // ldstr declaringTypeFullName
        il.Emit(OpCodes.Ldstr, declaringTypeFullName);
        // ldstr methodName
        il.Emit(OpCodes.Ldstr, targetMethod.Name);

        // Type[] paramTypes
        EmitTypeArray(il, wrapper, importedGetTypeFromHandle, typeType, paramCount);

        // object?[] args
        EmitArgsArray(il, wrapper, objectType, paramCount);

        // call bool TryInvokeVoid(...)
        il.Emit(OpCodes.Call, importedTryInvokeVoid);

        // brtrue RETURN
        var returnInstr = il.Create(OpCodes.Ret);
        il.Emit(OpCodes.Brtrue, returnInstr);

        // fallback: call real method (void — no return value to push)
        for (int i = 0; i < paramCount; i++)
            il.Emit(OpCodes.Ldarg, wrapper.Parameters[i]);
        il.Emit(OpCodes.Call, module.ImportReference(targetMethod));

        il.Append(returnInstr);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IL helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void EmitTypeArray(
        ILProcessor il,
        MethodDefinition wrapper,
        MethodReference getTypeFromHandle,
        TypeReference typeType,
        int paramCount)
    {
        EmitLdcI4(il, paramCount);
        il.Emit(OpCodes.Newarr, typeType);

        for (int i = 0; i < paramCount; i++)
        {
            il.Emit(OpCodes.Dup);
            EmitLdcI4(il, i);
            il.Emit(OpCodes.Ldtoken, wrapper.Parameters[i].ParameterType);
            il.Emit(OpCodes.Call, getTypeFromHandle);
            il.Emit(OpCodes.Stelem_Ref);
        }
    }

    private static void EmitArgsArray(
        ILProcessor il,
        MethodDefinition wrapper,
        TypeReference objectType,
        int paramCount)
    {
        EmitLdcI4(il, paramCount);
        il.Emit(OpCodes.Newarr, objectType);

        for (int i = 0; i < paramCount; i++)
        {
            il.Emit(OpCodes.Dup);
            EmitLdcI4(il, i);
            il.Emit(OpCodes.Ldarg, wrapper.Parameters[i]);

            bool isValueType = false;
            try { isValueType = wrapper.Parameters[i].ParameterType.Resolve()?.IsValueType ?? false; }
            catch { /* unresolvable — treat as reference type */ }

            if (isValueType)
                il.Emit(OpCodes.Box, wrapper.Parameters[i].ParameterType);

            il.Emit(OpCodes.Stelem_Ref);
        }
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

    // ─────────────────────────────────────────────────────────────────────────
    // Naming helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string BuildWrapperKey(MethodReference method)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(method.DeclaringType.FullName);
        sb.Append("::");
        sb.Append(method.Name);
        sb.Append('(');
        for (int i = 0; i < method.Parameters.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(method.Parameters[i].ParameterType.FullName);
        }
        sb.Append(')');
        return sb.ToString();
    }

    private static string BuildWrapperMethodName(MethodReference method)
    {
        var typeName = SanitizeName(method.DeclaringType.Name);
        var methodName = method.Name;
        if (method.Parameters.Count == 0)
            return $"__Shims_Static_{typeName}_{methodName}";

        var paramNames = string.Join("_", method.Parameters.Select(p => SanitizeName(p.ParameterType.Name)));
        return $"__Shims_Static_{typeName}_{methodName}_{paramNames}";
    }

    private static string SanitizeName(string name)
    {
        var tick = name.IndexOf('`');
        return tick >= 0 ? name.Substring(0, tick) : name;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Module helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static TypeDefinition GetOrCreateStaticWrapperClass(ModuleDefinition module)
    {
        var existing = module.Types.FirstOrDefault(t => t.Name == StaticWrapperClassName);
        if (existing is not null) return existing;

        var cls = new TypeDefinition(
            string.Empty,
            StaticWrapperClassName,
            CecilTypeAttrs.Class | CecilTypeAttrs.NotPublic | CecilTypeAttrs.Abstract | CecilTypeAttrs.Sealed,
            module.ImportReference(typeof(object)));
        module.Types.Add(cls);
        return cls;
    }

    private static IEnumerable<MethodDefinition> EnumerateMethods(
        ModuleDefinition module,
        TypeDefinition? skipType = null)
    {
        foreach (var type in module.Types)
        {
            if (skipType is not null && type == skipType) continue;
            foreach (var m in EnumerateMethods(type)) yield return m;
        }
    }

    private static IEnumerable<MethodDefinition> EnumerateMethods(TypeDefinition type)
    {
        foreach (var m in type.Methods) yield return m;
        foreach (var nested in type.NestedTypes)
            foreach (var m in EnumerateMethods(nested)) yield return m;
    }

    private static bool IsBclScope(string? scopeName)
    {
        if (string.IsNullOrEmpty(scopeName)) return false;
        return BclScopeNames.Contains(scopeName)
            || scopeName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
            || scopeName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveGenericArity(string fullName)
    {
        var tick = fullName.IndexOf('`');
        return tick < 0 ? fullName : fullName.Substring(0, tick);
    }
}
