namespace MiniMockito.Shims.Experimental.Sample;

public class ByRefTarget
{
    private readonly int _value;

    public ByRefTarget(ref int value)
    {
        _value = value;
    }

    public int GetValue() => _value;
}
