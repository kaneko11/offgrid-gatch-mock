namespace MiniMockito;

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

    public static Times Once()
    {
        return Exactly(1);
    }

    public static Times Exactly(int count)
    {
        ValidateCount(count);
        return new Times(TimesMode.Exactly, count, $"exactly {count}");
    }

    public static Times Never()
    {
        return Exactly(0);
    }

    public static Times AtLeast(int count)
    {
        ValidateCount(count);
        return new Times(TimesMode.AtLeast, count, $"at least {count}");
    }

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
