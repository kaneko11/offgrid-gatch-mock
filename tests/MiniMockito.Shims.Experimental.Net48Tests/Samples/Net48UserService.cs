namespace MiniMockito.Shims.Experimental.Net48Tests.Samples
{
    public class Net48UserService
    {
        public string GetDisplayName(int id)
        {
            Net48UserRepository repository = new Net48UserRepository();
            return repository.GetName(id);
        }

        public string GetDisplayNameWithArg(int id)
        {
            Net48UserRepository repository = new Net48UserRepository("prod");
            return repository.GetName(id);
        }
    }
}
