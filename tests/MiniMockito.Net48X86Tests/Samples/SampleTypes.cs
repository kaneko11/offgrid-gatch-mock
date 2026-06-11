using System.Threading.Tasks;

namespace MiniMockito.Net48X86Tests.Samples
{
    public interface IUserRepository
    {
        string FindById(int id);
        void Save(string value);
        int Count();
    }

    public interface IUserService
    {
        string GetName(int id);
        Task<string> GetNameAsync(int id);
        Task DoWorkAsync();
        ValueTask<int> GetCountAsync(int id);
        ValueTask DoValueWorkAsync();
    }

    // Real implementation used to verify spy delegation.
    public class RealUserService : IUserService
    {
        public string GetName(int id)
        {
            return "real-" + id;
        }

        public Task<string> GetNameAsync(int id)
        {
            return Task.FromResult("real-async-" + id);
        }

        public Task DoWorkAsync()
        {
            return Task.CompletedTask;
        }

        public ValueTask<int> GetCountAsync(int id)
        {
            return new ValueTask<int>(id * 10);
        }

        public ValueTask DoValueWorkAsync()
        {
            return default(ValueTask);
        }
    }

    public interface IWorkflowStep
    {
        void Start();
        void Save();
        void End();
    }
}
