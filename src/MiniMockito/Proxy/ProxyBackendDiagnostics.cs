using System;

namespace MiniMockito.Proxy;

/// <summary>
/// Internal diagnostics describing which interface proxy backend is selected for the
/// current runtime.  Not part of the public API; exposed to test assemblies via
/// <c>InternalsVisibleTo</c> to assert backend selection (e.g. RealProxy under net48 x86).
/// </summary>
internal sealed class ProxyBackendInfo
{
    internal ProxyBackendInfo(string selectedBackend, string targetFramework, bool is64BitProcess, string fallbackReason)
    {
        SelectedBackend = selectedBackend;
        TargetFramework = targetFramework;
        Is64BitProcess = is64BitProcess;
        FallbackReason = fallbackReason;
    }

    /// <summary>The selected backend name (e.g. <c>"DispatchProxy"</c> or <c>"RealProxy"</c>).</summary>
    public string SelectedBackend { get; }

    /// <summary>The compiled target framework (<c>"net48"</c> or <c>"net8.0"</c>).</summary>
    public string TargetFramework { get; }

    /// <summary>Whether the current process is 64-bit.</summary>
    public bool Is64BitProcess { get; }

    /// <summary>Human-readable reason for the backend choice.</summary>
    public string FallbackReason { get; }

    public override string ToString()
        => $"selected backend: {SelectedBackend}; target framework: {TargetFramework}; " +
           $"64-bit process: {Is64BitProcess}; reason: {FallbackReason}";
}

/// <summary>
/// Static accessor for the current <see cref="ProxyBackendInfo"/>.
/// </summary>
internal static class ProxyBackendDiagnostics
{
    /// <summary>Describes the proxy backend selected for the current runtime.</summary>
    internal static ProxyBackendInfo Describe()
    {
        var factory = InterfaceProxyFactorySelector.Resolve();
#if NETFRAMEWORK
        const string tfm = "net48";
        var reason = factory.Name == "RealProxy"
            ? "DispatchProxy.Create fails with TypeLoadException under PlatformTarget=x86 on .NET Framework; the RealProxy backend is used instead."
            : "DispatchProxy backend in use.";
#else
        const string tfm = "net8.0";
        var reason = "DispatchProxy is fully supported on this runtime.";
#endif
        return new ProxyBackendInfo(factory.Name, tfm, Environment.Is64BitProcess, reason);
    }
}
