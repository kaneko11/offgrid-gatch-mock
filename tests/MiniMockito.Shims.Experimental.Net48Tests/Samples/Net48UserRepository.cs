namespace MiniMockito.Shims.Experimental.Net48Tests.Samples
{
    public class Net48UserRepository
    {
        private readonly string _prefix;

        public Net48UserRepository() : this("real") { }

        public Net48UserRepository(string prefix)
        {
            _prefix = prefix;
        }

        public string GetName(int id)
        {
            return _prefix + "-" + id;
        }
    }
}
