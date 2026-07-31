namespace MiniMockito.Shims.Experimental;

/// <summary>
/// Identifies the interception backend selected for a method replacement.
/// </summary>
public enum MethodInterceptionBackend
{
    /// <summary>The method cannot be intercepted by the requested API.</summary>
    Unsupported = 0,

    /// <summary>A class proxy intercepts a virtual method on a proxy instance.</summary>
    ClassProxy = 1,

    /// <summary>The caller's instance-method call site is rewritten.</summary>
    InstanceCallSiteRewrite = 2,

    /// <summary>The caller's static-method call site is rewritten.</summary>
    StaticCallSiteRewrite = 3,
}
