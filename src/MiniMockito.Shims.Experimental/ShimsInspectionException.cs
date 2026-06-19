namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Thrown by the Phase 24 inspection API (<see cref="ShimsObject"/> / <see cref="ShimsCollection"/> /
/// the <c>Shims.GetValue</c> family) when a property path cannot be resolved or a value cannot be
/// converted to the requested type.
/// </summary>
/// <remarks>
/// <b>Experimental.</b> The message includes the requested path, the failed segment, the runtime
/// type involved, and the reason — so type-identity mismatches between a rewritten object and the
/// test's original type can be diagnosed without an <see cref="System.InvalidCastException"/>.
/// </remarks>
public class ShimsInspectionException : ShimException
{
    /// <summary>Initializes a new instance of the <see cref="ShimsInspectionException"/> class.</summary>
    public ShimsInspectionException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The exception message.</param>
    public ShimsInspectionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ShimsInspectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
