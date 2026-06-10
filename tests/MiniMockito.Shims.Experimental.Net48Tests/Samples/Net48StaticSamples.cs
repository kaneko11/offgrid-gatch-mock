namespace MiniMockito.Shims.Experimental.Net48Tests.Samples
{
    public static class Net48StaticClock
    {
        public static string GetLabel(int id)
        {
            return "real-" + id;
        }

        public static void RecordCall(string message)
        {
            // intentional no-op — used for void shim tests
        }
    }

    public class Net48TimedService
    {
        public string GetLabel(int id)
        {
            return Net48StaticClock.GetLabel(id);
        }

        public void RecordCall(string message)
        {
            Net48StaticClock.RecordCall(message);
        }
    }
}
