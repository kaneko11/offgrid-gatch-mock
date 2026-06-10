// Polyfill for `init` accessor support on .NET Framework 4.8.
// The C# compiler looks up IsExternalInit by full name; defining it here
// enables `init`-only properties when targeting net48 with C# 12.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
