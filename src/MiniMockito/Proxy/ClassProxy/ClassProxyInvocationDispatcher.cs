using System.Reflection;
using System.Runtime.ExceptionServices;
using MiniMockito.Core;
using MiniMockito.Exceptions;
using MiniMockito.Utilities;
using MiniMockito.Verification;

namespace MiniMockito.Proxy.ClassProxy;

/// <summary>
/// Dispatches calls from generated class proxies into the MiniMockito invocation pipeline.
/// </summary>
public static class ClassProxyInvocationDispatcher
{
    /// <summary>
    /// Handles a generated class proxy invocation.
    /// </summary>
    /// <param name="proxy">The generated proxy instance.</param>
    /// <param name="targetMethod">The original target method.</param>
    /// <param name="args">The invocation arguments.</param>
    /// <param name="baseInvokerName">The generated private method that calls the base implementation.</param>
    /// <returns>The return value to pass back to the generated override.</returns>
    public static object? Invoke(object proxy, MethodInfo targetMethod, object?[] args, string baseInvokerName)
    {
        ThrowHelper.ThrowIfNull(proxy);
        ThrowHelper.ThrowIfNull(targetMethod);
        ThrowHelper.ThrowIfNull(args);
        ThrowHelper.ThrowIfNullOrWhiteSpace(baseInvokerName);

        MockState state;
        try
        {
            state = MockRepository.Default.GetState(proxy);
        }
        catch (UnsupportedMockTargetException exception)
        {
            throw new ClassProxyException(
                "Class proxy invocation occurred before the proxy was registered. Avoid virtual calls from base constructors.",
                exception);
        }

        var invocation = state.RecordInvocation(targetMethod, args);

        try
        {
            var stubRule = state.FindStubRule(targetMethod, args);
            object? returnValue;

            if (stubRule is not null)
            {
                returnValue = stubRule.Invoke(invocation, targetMethod.ReturnType);
            }
            else if (state.CallsBase)
            {
                returnValue = InvokeBase(proxy, baseInvokerName, args);
            }
            else if (state.Behavior == MockBehavior.Strict)
            {
                throw CreateStrictException(state, targetMethod, args);
            }
            else
            {
                returnValue = DefaultValueProvider.GetDefaultValue(targetMethod.ReturnType);
            }

            invocation.ReturnValue = returnValue;
            return returnValue;
        }
        catch (Exception exception)
        {
            invocation.Exception = exception;
            throw;
        }
    }

    private static object? InvokeBase(object proxy, string baseInvokerName, object?[] args)
    {
        var baseInvoker = proxy
            .GetType()
            .GetMethod(baseInvokerName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new ClassProxyException(string.Join(
                Environment.NewLine,
                "Class proxy base invocation failed.",
                $"Target class: {proxy.GetType().BaseType?.FullName}",
                $"Method: {baseInvokerName}",
                "Reason: Base invoker method was not found.",
                "Supported methods:",
                "  <unknown>",
                "Unsupported methods:",
                "  <unknown>",
                "Hint: Recreate the class proxy and report this as a MiniMockito class proxy generation bug."));

        try
        {
            return baseInvoker.Invoke(proxy, [args]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static ClassProxyException CreateStrictException(MockState state, MethodInfo targetMethod, object?[] args)
    {
        var candidates = state.DescribeStubCandidates(targetMethod);
        var candidateText = candidates.Count == 0
            ? "  <none>"
            : string.Join(Environment.NewLine, candidates.Select(candidate => $"  {candidate}"));

        var argumentText = string.Join(", ", args.Select(InvocationFormatter.FormatValue));
        var supports = ClassProxyValidation.GetMethodSupports(state.MockedType);
        var supported = supports
            .Where(support => support.IsSupported)
            .Select(support => $"  {support.Describe()}")
            .DefaultIfEmpty("  <none>");
        var unsupported = supports
            .Where(support => !support.IsSupported)
            .Select(support => $"  {support.Describe()}")
            .DefaultIfEmpty("  <none>");

        return new ClassProxyException(string.Join(
            Environment.NewLine,
            "Strict class mock received an unstubbed invocation.",
            $"Target class: {state.MockedType.FullName}",
            $"Method: {targetMethod.Name}",
            $"Reason: No stub matched the invocation.",
            $"Arguments: {argumentText}",
            "Existing stub candidates:",
            candidateText,
            "Supported methods:",
            string.Join(Environment.NewLine, supported),
            "Unsupported methods:",
            string.Join(Environment.NewLine, unsupported),
            "Hint: Configure a stub with When(...).ThenReturn(...) or use lenient behavior."));
    }
}
