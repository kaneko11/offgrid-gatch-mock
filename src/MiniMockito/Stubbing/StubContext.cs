using System.Reflection;

namespace MiniMockito.Stubbing;

public sealed class StubContext
{
    internal StubContext(Guid mockId, MethodInfo method, IReadOnlyList<object?> arguments)
    {
        MockId = mockId;
        Method = method;
        Arguments = arguments;
    }

    public Guid MockId { get; }

    public MethodInfo Method { get; }

    public IReadOnlyList<object?> Arguments { get; }
}
