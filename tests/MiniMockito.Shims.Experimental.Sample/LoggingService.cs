namespace MiniMockito.Shims.Experimental.Sample;

/// <summary>
/// Sample service that calls the void static method <see cref="StaticClock.LogCall(string)"/>,
/// used to exercise void static-method shims through the high-level facade.
/// </summary>
public class LoggingService
{
    public void Run(string message)
    {
        StaticClock.LogCall(message);
    }
}
