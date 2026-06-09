using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

[TestClass]
public sealed class ParallelizationSettingsTests
{
    /// <summary>
    /// Verifies that the shims test assembly has <c>[assembly: DoNotParallelize]</c>.
    /// Parallel test execution is unsafe when shims are active because the shim dispatcher
    /// uses process-wide state that can be corrupted by concurrent test runs.
    /// </summary>
    [TestMethod]
    public void ShimsTestAssembly_HasDoNotParallelizeAttribute()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var attribute = assembly.GetCustomAttribute<DoNotParallelizeAttribute>();

        Assert.IsNotNull(
            attribute,
            "MiniMockito.Shims.Experimental.Tests must have [assembly: DoNotParallelize]. " +
            "The shim dispatcher uses process-wide state that is unsafe under parallel test runs.");
    }
}
