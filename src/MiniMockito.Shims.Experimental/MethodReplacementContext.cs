using System.Reflection;

namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Supplies the exact reflected method, receiver, and boxed arguments to a typed method
/// replacement callback.
/// </summary>
public sealed class MethodReplacementContext
{
    internal MethodReplacementContext(MethodInfo method, object? receiver, object?[] arguments)
    {
        Method = method;
        Receiver = receiver;
        Arguments = arguments;
    }

    /// <summary>Gets the exact method selected when the replacement was registered.</summary>
    public MethodInfo Method { get; }

    /// <summary>Gets the rewritten call receiver.</summary>
    public object? Receiver { get; }

    /// <summary>Gets the boxed method arguments in declaration order.</summary>
    public IReadOnlyList<object?> Arguments { get; }
}
