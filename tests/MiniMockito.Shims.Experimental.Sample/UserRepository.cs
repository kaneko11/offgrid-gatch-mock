namespace MiniMockito.Shims.Experimental.Sample;

public class UserRepository
{
    private readonly string prefix;

    public UserRepository()
        : this("real")
    {
    }

    public UserRepository(string prefix)
    {
        this.prefix = prefix;
    }

    public string GetName(int id)
    {
        return $"{prefix}-{id}";
    }
}
