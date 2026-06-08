namespace MiniMockito.Shims.Experimental.Sample;

public class UserService
{
    public string GetDisplayName(int id)
    {
        var repository = new UserRepository();
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
}
