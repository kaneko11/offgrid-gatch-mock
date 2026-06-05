namespace MiniMockito;

/// <summary>
/// Describes the expected number of matching invocations for verification.
/// </summary>
public sealed class Times
{
    private Times(TimesMode mode, int count, string description)
    {
        Mode = mode;
        Count = count;
        Description = description;
    }

    internal TimesMode Mode { get; }

    internal int Count { get; }

    internal string Description { get; }

    /// <summary>
    /// Expects exactly one matching invocation.
    /// </summary>
    /// <returns>A count rule for one invocation.</returns>
    public static Times Once()
    {
        return Exactly(1);
    }

    /// <summary>
    /// Expects exactly <paramref name="count"/> matching invocations.
    /// </summary>
    /// <param name="count">The exact expected count.</param>
    /// <returns>A count rule for the exact count.</returns>
    public static Times Exactly(int count)
    {
        ValidateCount(count);
        return new Times(TimesMode.Exactly, count, $"exactly {count}");
    }

    /// <summary>
    /// Expects zero matching invocations.
    /// </summary>
    /// <returns>A count rule for zero invocations.</returns>
    public static Times Never()
    {
        return Exactly(0);
    }

    /// <summary>
    /// Expects at least <paramref name="count"/> matching invocations.
    /// </summary>
    /// <param name="count">The minimum expected count.</param>
    /// <returns>A count rule for the minimum count.</returns>
    public static Times AtLeast(int count)
    {
        ValidateCount(count);
        return new Times(TimesMode.AtLeast, count, $"at least {count}");
    }

    /// <summary>
    /// Expects at most <paramref name="count"/> matching invocations.
    /// </summary>
    /// <param name="count">The maximum expected count.</param>
    /// <returns>A count rule for the maximum count.</returns>
    public static Times AtMost(int count)
    {
        ValidateCount(count);
        return new Times(TimesMode.AtMost, count, $"at most {count}");
    }

    internal bool IsSatisfiedBy(int actualCount)
    {
        return Mode switch
        {
            TimesMode.Exactly => actualCount == Count,
            TimesMode.AtLeast => actualCount >= Count,
            TimesMode.AtMost => actualCount <= Count,
            _ => false
        };
    }

    private static void ValidateCount(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be zero or greater.");
        }
    }
}
