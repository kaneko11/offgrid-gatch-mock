namespace MiniMockito.Shims.Experimental.Sample;

public class GenericRepository<T>
{
    public string GetName()
    {
        return typeof(T).Name;
    }
}
