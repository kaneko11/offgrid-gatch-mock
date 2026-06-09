namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Carries information about an intercepted static method invocation.
/// Analogous to <see cref="ShimConstructorContext"/> for constructor interception.
/// </summary>
public sealed class StaticInvocationContext
{
    internal StaticInvocationContext(string declaringTypeFullName, string methodName, object?[] arguments)
    {
        DeclaringTypeFullName = declaringTypeFullName;
        MethodName = methodName;
        Arguments = arguments;
    }

    /// <summary>Gets the full name of the declaring type.</summary>
    public string DeclaringTypeFullName { get; }

    /// <summary>Gets the intercepted method name.</summary>
    public string MethodName { get; }

    /// <summary>Gets the boxed arguments in declaration order.</summary>
    public IReadOnlyList<object?> Arguments { get; }

    /// <summary>Gets the argument at <paramref name="index"/> cast to <typeparamref name="T"/>.</summary>
    public T? GetArgument<T>(int index) => (T?)Arguments[index];
}
