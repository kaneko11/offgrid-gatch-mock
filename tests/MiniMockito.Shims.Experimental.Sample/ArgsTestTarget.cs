namespace MiniMockito.Shims.Experimental.Sample;

public class ArgsTestTarget
{
    private readonly string _info;

    public ArgsTestTarget(int value)
    {
        _info = $"int:{value}";
    }

    public ArgsTestTarget(bool value)
    {
        _info = $"bool:{value}";
    }

    public ArgsTestTarget(string value)
    {
        _info = $"str:{value}";
    }

    public ArgsTestTarget(string first, int second)
    {
        _info = $"str:{first},int:{second}";
    }

    public string GetInfo() => _info;
}
