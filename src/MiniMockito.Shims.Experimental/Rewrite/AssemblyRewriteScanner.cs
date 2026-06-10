using System.Reflection;
using System.Reflection.Emit;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Scans assemblies for allowlisted <c>newobj</c> call sites without rewriting IL.
/// </summary>
public static class AssemblyRewriteScanner
{
    private static readonly OpCode[] SingleByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] MultiByteOpCodes = new OpCode[0x100];

    static AssemblyRewriteScanner()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            var value = unchecked((ushort)opCode.Value);
            if (value < 0x100)
            {
                SingleByteOpCodes[value] = opCode;
            }
            else if ((value & 0xff00) == 0xfe00)
            {
                MultiByteOpCodes[value & 0xff] = opCode;
            }
        }
    }

    /// <summary>
    /// Scans an assembly path for allowlisted <c>newobj</c> call sites.
    /// </summary>
    /// <param name="assemblyPath">The assembly path to scan.</param>
    /// <param name="options">The scan options.</param>
    /// <returns>A dry-run rewrite report.</returns>
    public static RewriteReport Scan(string assemblyPath, NewObjScanOptions options)
    {
        var plan = RewritePlan.FromOptions(assemblyPath, options);
        return Scan(plan);
    }

    /// <summary>
    /// Scans an assembly using a rewrite plan.
    /// </summary>
    /// <param name="plan">The rewrite plan.</param>
    /// <returns>A dry-run rewrite report.</returns>
    public static RewriteReport Scan(RewritePlan plan)
    {
        ThrowHelper.ThrowIfNull(plan);
        if (!File.Exists(plan.AssemblyPath))
        {
            throw new ShimRewriteException(string.Join(
                Environment.NewLine,
                "Assembly rewrite dry-run failed.",
                $"Target assembly: {plan.AssemblyPath}",
                "Rewrite mode: DryRunScan",
                "Reason: AssemblyFileNotFound",
                "Hint: Provide a compiled managed assembly path."));
        }

        var assembly = Assembly.LoadFrom(plan.AssemblyPath);
        var callSites = new List<NewObjCallSite>();

        foreach (var type in assembly.GetTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            foreach (var method in EnumerateMethods(type))
            {
                ScanMethod(plan, type, method, callSites);
            }
        }

        return new RewriteReport(plan.AssemblyPath, plan.Targets, new NewObjScanResult(callSites));
    }

    private static IEnumerable<MethodBase> EnumerateMethods(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;

        foreach (var constructor in type.GetConstructors(flags))
        {
            yield return constructor;
        }

        foreach (var method in type.GetMethods(flags))
        {
            yield return method;
        }
    }

    private static void ScanMethod(RewritePlan plan, Type callingType, MethodBase method, List<NewObjCallSite> callSites)
    {
        var body = method.GetMethodBody();
        if (body is null)
        {
            return;
        }

        var il = body.GetILAsByteArray();
        if (il is null || il.Length == 0)
        {
            return;
        }

        var position = 0;
        while (position < il.Length)
        {
            var offset = position;
            var opCode = ReadOpCode(il, ref position);
            var operandStart = position;
            var operandSize = GetOperandSize(opCode, il, operandStart);

            if (opCode == OpCodes.Newobj && operandSize == 4)
            {
                var metadataToken = BitConverter.ToInt32(il, operandStart);
                var constructor = TryResolveConstructor(method, metadataToken);
                if (constructor?.DeclaringType is { } targetType && IsAllowlisted(plan.Targets, targetType))
                {
                    callSites.Add(CreateCallSite(plan.AssemblyPath, callingType, method, constructor, targetType, offset));
                }
            }

            position = operandStart + operandSize;
        }
    }

    private static OpCode ReadOpCode(byte[] il, ref int position)
    {
        var first = il[position++];
        if (first != 0xfe)
        {
            return SingleByteOpCodes[first];
        }

        var second = il[position++];
        return MultiByteOpCodes[second];
    }

    private static int GetOperandSize(OpCode opCode, byte[] il, int operandStart)
    {
        return opCode.OperandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget => 1,
            OperandType.ShortInlineI => 1,
            OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget => 4,
            OperandType.InlineField => 4,
            OperandType.InlineI => 4,
            OperandType.InlineMethod => 4,
            OperandType.InlineSig => 4,
            OperandType.InlineString => 4,
            OperandType.InlineSwitch => GetInlineSwitchOperandSize(il, operandStart),
            OperandType.InlineTok => 4,
            OperandType.InlineType => 4,
            OperandType.ShortInlineR => 4,
            OperandType.InlineI8 => 8,
            OperandType.InlineR => 8,
            _ => throw new ShimRewriteException($"Unsupported IL operand type '{opCode.OperandType}' while scanning newobj instructions.")
        };
    }

    private static int GetInlineSwitchOperandSize(byte[] il, int operandStart)
    {
        if (operandStart + 4 > il.Length)
        {
            throw new ShimRewriteException("Invalid switch instruction while scanning newobj instructions.");
        }

        var count = BitConverter.ToInt32(il, operandStart);
        return 4 + (count * 4);
    }

    private static ConstructorInfo? TryResolveConstructor(MethodBase callingMethod, int metadataToken)
    {
        try
        {
            var typeArguments = callingMethod.DeclaringType?.GetGenericArguments();
            var methodArguments = callingMethod is MethodInfo { IsGenericMethod: true } methodInfo
                ? methodInfo.GetGenericArguments()
                : null;
            return callingMethod.Module.ResolveMethod(metadataToken, typeArguments, methodArguments) as ConstructorInfo;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsAllowlisted(IReadOnlyList<RewriteTarget> targets, Type targetType)
    {
        return targets.Any(target => target.Matches(targetType));
    }

    private static NewObjCallSite CreateCallSite(
        string assemblyPath,
        Type callingType,
        MethodBase callingMethod,
        ConstructorInfo constructor,
        Type targetType,
        int ilOffset)
    {
        var unsupportedReason = GetUnsupportedReason(constructor, targetType);
        return new NewObjCallSite(
            assemblyPath,
            GetFriendlyTypeName(targetType),
            FormatConstructor(constructor),
            GetFriendlyTypeName(callingType),
            callingMethod.Name,
            ilOffset,
            unsupportedReason is null,
            unsupportedReason);
    }

    private static string? GetUnsupportedReason(ConstructorInfo constructor, Type targetType)
    {
        if (targetType.Assembly == typeof(string).Assembly)
        {
            return "BclTypeNotSupported";
        }

        if (!targetType.IsClass)
        {
            return "TargetTypeIsNotAClass";
        }

        if (!targetType.IsPublic && !targetType.IsNestedPublic)
        {
            return "TargetTypeIsNotPublic";
        }

        if (targetType.IsGenericType || targetType.ContainsGenericParameters)
        {
            return "GenericTypeNotSupported";
        }

        foreach (var param in constructor.GetParameters())
        {
            if (param.ParameterType.IsByRef)
                return "ByRefArgumentNotSupported";
            if (param.ParameterType.IsGenericParameter || param.ParameterType.ContainsGenericParameters)
                return "GenericArgumentNotSupported";
            if (param.IsDefined(typeof(ParamArrayAttribute), false))
                return "ParamsArgumentNotSupported";
        }

        if (!constructor.IsPublic)
        {
            return "ConstructorIsNotPublic";
        }

        return null;
    }

    private static string FormatConstructor(ConstructorInfo constructor)
    {
        var parameters = constructor
            .GetParameters()
            .Select(parameter => GetFriendlyTypeName(parameter.ParameterType));
        return $".ctor({string.Join(", ", parameters)})";
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var genericTypeDefinition = type.GetGenericTypeDefinition();
        var name = genericTypeDefinition.FullName ?? genericTypeDefinition.Name;
        var tickIndex = name.IndexOf('`');
        if (tickIndex >= 0)
        {
            name = name.Substring(0, tickIndex);
        }

        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName))}>";
    }
}
