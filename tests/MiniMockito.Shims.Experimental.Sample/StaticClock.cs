namespace MiniMockito.Shims.Experimental.Sample;

/// <summary>
/// Sample static class used as a target for Phase 14 static method mocking PoC.
/// </summary>
public static class StaticClock
{
    public static DateTime Now() => DateTime.Now;

    public static string GetName(int id) => $"real-name-{id}";

    public static bool IsOpen(bool flag) => flag;

    public static string Concat(string a, string b) => a + b;

    public static void LogCall(string message) { /* side-effect-free stub */ }
}

/// <summary>
/// Sample service that calls <see cref="StaticClock"/> — target for integration tests.
/// </summary>
public class TimedService
{
    public string GetTimedName(int id)
    {
        var ts = StaticClock.Now().ToString("yyyyMMdd");
        return $"{id}-{ts}";
    }

    public string GetDisplayName(int id)
    {
        return StaticClock.GetName(id);
    }

    public bool CheckOpen(bool flag)
    {
        return StaticClock.IsOpen(flag);
    }

    public string ConcatNames(string a, string b)
    {
        return StaticClock.Concat(a, b);
    }
}
