namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Exception thrown when a string-based external target cannot be resolved (Phase 21).
/// </summary>
/// <remarks>
/// <b>Experimental.</b> Raised by
/// <see cref="NewInterceptionHarness.ResolveExternalType(string, string)"/> and
/// <see cref="NewInterceptionHarness.WithExternalTarget(string, string)"/> when the external
/// assembly file is missing or the requested type full name does not exist in the loaded assembly.
/// The message lists the searched assembly path and the requested type full name.
/// </remarks>
public class ShimExternalTargetException : ShimException
{
    /// <summary>Initializes a new instance of the <see cref="ShimExternalTargetException"/> class.</summary>
    public ShimExternalTargetException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ShimExternalTargetException"/> class with a message.</summary>
    /// <param name="message">The exception message.</param>
    public ShimExternalTargetException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ShimExternalTargetException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
