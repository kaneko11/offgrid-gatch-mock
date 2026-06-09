namespace MiniMockito.Shims.Experimental.Sample;

public class UserService
{
    public string GetDisplayName(int id)
    {
        var repository = new UserRepository();
        return repository.GetName(id);
    }

    public string GetDisplayNameWithArgRepository(int id)
    {
        var repository = new UserRepository("prod");
        return repository.GetName(id);
    }

    public UserRepository CreateRepositoryWithArguments()
    {
        return new UserRepository("argument");
    }

    public GenericRepository<string> CreateGenericRepository()
    {
        return new GenericRepository<string>();
    }

    public string GetArgsTargetByInt(int value)
    {
        var target = new ArgsTestTarget(value);
        return target.GetInfo();
    }

    public string GetArgsTargetByBool(bool value)
    {
        var target = new ArgsTestTarget(value);
        return target.GetInfo();
    }

    public string GetArgsTargetByStringAndInt(string first, int second)
    {
        var target = new ArgsTestTarget(first, second);
        return target.GetInfo();
    }

    public ByRefTarget CreateByRefTarget(int value)
    {
        return new ByRefTarget(ref value);
    }
}
