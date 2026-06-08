using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Rewrites supported <c>newobj</c> instructions to <see cref="ShimDispatcher.New{T}"/>.
/// </summary>
public static class NewObjRewriter
{
    /// <summary>
    /// Rewrites supported <c>newobj</c> instructions in the supplied module.
    /// </summary>
    /// <param name="module">The module to rewrite.</param>
    /// <param name="options">The rewrite options.</param>
    /// <param name="diagnostics">Diagnostics written during rewriting.</param>
    /// <returns>The number of rewritten call sites.</returns>
    public static int Rewrite(ModuleDefinition module, RewriteOptions options, IList<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var targetTypeNames = options.TargetTypes
            .Select(type => type.FullName)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        if (targetTypeNames.Count == 0)
        {
            diagnostics.Add("No allowlisted target types were provided. No newobj call sites were rewritten.");
            return 0;
        }

        var dispatcherNewMethod = typeof(ShimDispatcher).GetMethod(nameof(ShimDispatcher.New), Type.EmptyTypes);
        if (dispatcherNewMethod is null)
        {
            throw new ShimRewriteException("ShimDispatcher.New<T>() could not be found.");
        }

        var importedDispatcherNewMethod = module.ImportReference(dispatcherNewMethod);
        var rewrittenCount = 0;

        foreach (var method in EnumerateMethods(module))
        {
            if (!method.HasBody)
            {
                continue;
            }

            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode != OpCodes.Newobj || instruction.Operand is not MethodReference constructor)
                {
                    continue;
                }

                var declaringType = constructor.DeclaringType;
                if (!targetTypeNames.Contains(RemoveGenericArity(declaringType.FullName)))
                {
                    continue;
                }

                if (!IsSupportedNewObj(constructor, declaringType, diagnostics, method, instruction))
                {
                    continue;
                }

                var replacement = new GenericInstanceMethod(importedDispatcherNewMethod);
                replacement.GenericArguments.Add(module.ImportReference(declaringType));

                instruction.OpCode = OpCodes.Call;
                instruction.Operand = replacement;
                rewrittenCount++;

                diagnostics.Add(
                    $"Rewrote {method.DeclaringType.FullName}.{method.Name} IL_{instruction.Offset:X4}: new {declaringType.FullName}() -> ShimDispatcher.New<{declaringType.FullName}>().");
            }
        }

        return rewrittenCount;
    }

    private static IEnumerable<MethodDefinition> EnumerateMethods(ModuleDefinition module)
    {
        foreach (var type in module.Types)
        {
            foreach (var method in EnumerateMethods(type))
            {
                yield return method;
            }
        }
    }

    private static IEnumerable<MethodDefinition> EnumerateMethods(TypeDefinition type)
    {
        foreach (var method in type.Methods)
        {
            yield return method;
        }

        foreach (var nestedType in type.NestedTypes)
        {
            foreach (var method in EnumerateMethods(nestedType))
            {
                yield return method;
            }
        }
    }

    private static bool IsSupportedNewObj(
        MethodReference constructor,
        TypeReference declaringType,
        IList<string> diagnostics,
        MethodDefinition callingMethod,
        Instruction instruction)
    {
        if (constructor.Parameters.Count != 0)
        {
            diagnostics.Add(
                $"Skipped {callingMethod.DeclaringType.FullName}.{callingMethod.Name} IL_{instruction.Offset:X4}: constructor arguments are not supported.");
            return false;
        }

        if (declaringType is GenericInstanceType || declaringType.HasGenericParameters)
        {
            diagnostics.Add(
                $"Skipped {callingMethod.DeclaringType.FullName}.{callingMethod.Name} IL_{instruction.Offset:X4}: generic target types are not supported.");
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
                $"Skipped {callingMethod.DeclaringType.FullName}.{callingMethod.Name} IL_{instruction.Offset:X4}: target type could not be resolved.");
            return false;
        }

        if (!resolvedType.IsPublic || !resolvedType.IsClass)
        {
            diagnostics.Add(
                $"Skipped {callingMethod.DeclaringType.FullName}.{callingMethod.Name} IL_{instruction.Offset:X4}: target type must be a public class.");
            return false;
        }

        return true;
    }

    private static string RemoveGenericArity(string fullName)
    {
        var tickIndex = fullName.IndexOf('`', StringComparison.Ordinal);
        return tickIndex < 0 ? fullName : fullName[..tickIndex];
    }
}
