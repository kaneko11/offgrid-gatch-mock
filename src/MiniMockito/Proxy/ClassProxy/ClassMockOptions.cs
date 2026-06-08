namespace MiniMockito;

/// <summary>
/// Configures class mock creation.
/// </summary>
public sealed class ClassMockOptions
{
    /// <summary>
    /// Gets a lenient class mock option set.
    /// </summary>
    public static ClassMockOptions Lenient { get; } = new(MockBehavior.Lenient);

    /// <summary>
    /// Gets a strict class mock option set.
    /// </summary>
    public static ClassMockOptions Strict { get; } = new(MockBehavior.Strict);

    /// <summary>
    /// Gets an option set that calls base implementations for unstubbed virtual method invocations.
    /// </summary>
    public static ClassMockOptions CallBase { get; } = new()
    {
        CallsBase = true
    };

    /// <summary>
    /// Initializes a new lenient class mock option set.
    /// </summary>
    public ClassMockOptions()
    {
    }

    /// <summary>
    /// Initializes a new class mock option set with the supplied behavior.
    /// </summary>
    /// <param name="behavior">The behavior for unstubbed invocations.</param>
    public ClassMockOptions(MockBehavior behavior)
    {
        Behavior = behavior;
    }

    /// <summary>
    /// Gets or initializes the behavior for unstubbed invocations.
    /// </summary>
    public MockBehavior Behavior { get; init; } = MockBehavior.Lenient;

    /// <summary>
    /// Gets or initializes whether unstubbed virtual method invocations should call the base implementation.
    /// </summary>
    public bool CallsBase { get; init; }
}
