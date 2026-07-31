using System.Reflection;
using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMockito.Shims.Experimental.Tests;

/// <summary>Phase 25 type-safe method replacement and signature-validation tests.</summary>
[TestClass]
[DoNotParallelize]
public sealed class TypeSafeMethodReplacementTests
{
    private const string ConstructorTypeName =
        "CrossAssemblySample.ConstructorCallsIntMethod";
    private const string CallerTypeName =
        "CrossAssemblySample.TypedMethodCaller";

    private static string TargetAssemblyPath =>
        typeof(CrossAssemblySample.ConstructorCallsIntMethod).Assembly.Location;

    private static MethodInfo LoadMethod =>
        typeof(ExternalTableLoader).GetMethod(
            "Load",
            new[] { typeof(object), typeof(string), typeof(bool) })!;

    [TestMethod]
    public void MethodInfo_IntMethod_InConstructor_ReturnsBoxedIntAndCompletes()
    {
        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        shims.ReplaceMethod<int>(LoadMethod).Returns(0);

        var service = shims.CreateObject(ConstructorTypeName);

        Assert.IsTrue(shims.GetValue<bool>(service, "Initialized"));
        Assert.IsTrue(shims.LastMethodDispatchDiagnostics!.ReplacementFound);
        Assert.AreEqual(typeof(int), shims.LastMethodDispatchDiagnostics.ExpectedReturnType);
        Assert.AreEqual(typeof(int), shims.LastMethodDispatchDiagnostics.ActualReturnType);
        StringAssert.Contains(shims.LastMethodDispatchDiagnostics.MethodSignature, "ExternalTableLoader.Load");
        StringAssert.Contains(shims.LastMethodDispatchDiagnostics.ParameterTypes, "System.Boolean");
        StringAssert.Contains(shims.LastMethodDispatchDiagnostics.CallingMethod, "ConstructorCallsIntMethod");
        Assert.AreEqual("typed API", shims.LastMethodDispatchDiagnostics.RegistrationSource);
        Assert.IsFalse(shims.LastMethodDispatchDiagnostics.FallbackToOriginal);
        Assert.AreEqual(
            MethodInterceptionBackend.InstanceCallSiteRewrite,
            shims.LastMethodDispatchDiagnostics.SelectedBackend);
    }

    [TestMethod]
    public void GenericTargetAndTypeOverloads_ResolveExactSignature()
    {
        using (var genericTarget = Shims.ForAssembly(TargetAssemblyPath))
        {
            genericTarget
                .ReplaceMethod<ExternalTableLoader, int>(
                    "Load",
                    typeof(object),
                    typeof(string),
                    typeof(bool))
                .Returns(7);

            var caller = genericTarget.CreateObject(CallerTypeName);
            Assert.AreEqual(7, genericTarget.Invoke<int>(caller, "CallLoad", "sql", true));
        }

        using (var typeBased = Shims.ForAssembly(TargetAssemblyPath))
        {
            typeBased
                .ReplaceMethod<int>(
                    typeof(ExternalTableLoader),
                    "Load",
                    new[] { typeof(object), typeof(string), typeof(bool) })
                .Returns(8);

            var caller = typeBased.CreateObject(CallerTypeName);
            Assert.AreEqual(8, typeBased.Invoke<int>(caller, "CallLoad", "sql", true));
        }
    }

    [TestMethod]
    public void Callback_AnyEqIsAndCaptor_UseExactArguments()
    {
        string? capturedSql = null;
        var captor = ShimCaptor.For<string>();

        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        shims.ReplaceMethod<int>(LoadMethod)
            .WithArguments(
                ShimArg.Any<object>(),
                captor,
                ShimArg.Is<bool>(value => value))
            .Returns(context =>
            {
                capturedSql = (string)context.Arguments[1]!;
                return 12;
            });

        var caller = shims.CreateObject(CallerTypeName);
        Assert.AreEqual(12, shims.Invoke<int>(caller, "CallLoad", "SELECT 1", true));
        Assert.AreEqual("SELECT 1", capturedSql);
        Assert.AreEqual("SELECT 1", captor.Value);
    }

    [TestMethod]
    public void ExactOverload_DoesNotRewriteAnotherOverload()
    {
        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        shims.ReplaceMethod<int>(LoadMethod).Returns(99);

        var caller = shims.CreateObject(CallerTypeName);

        Assert.AreEqual(3, shims.Invoke<int>(caller, "CallSingleArgumentOverload", "abc"));
        Assert.AreEqual(99, shims.Invoke<int>(caller, "CallLoad", "sql", true));
    }

    [TestMethod]
    public void TypeEmptyTypes_SelectsZeroArgumentMethod()
    {
        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        shims.ReplaceMethod<int>(
                typeof(ExternalTableLoader),
                "NoArguments",
                Type.EmptyTypes)
            .Returns(42);

        var caller = shims.CreateObject(CallerTypeName);
        Assert.AreEqual(42, shims.Invoke<int>(caller, "CallNoArguments"));
    }

    [TestMethod]
    public void OptionalParameter_IsPartOfThreeArgumentSignature()
    {
        Assert.AreEqual(3, LoadMethod.GetParameters().Length);
        Assert.IsTrue(LoadMethod.GetParameters()[2].IsOptional);

        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        shims.ReplaceMethod<int>(
                typeof(ExternalTableLoader),
                "Load",
                typeof(object),
                typeof(string),
                typeof(bool))
            .Returns(5);

        var caller = shims.CreateObject(CallerTypeName);
        Assert.AreEqual(5, shims.Invoke<int>(caller, "CallLoad", "sql", true));
    }

    [TestMethod]
    public void VoidApi_DoNothingAndCallback_AreSeparatedFromReturnApi()
    {
        var method = typeof(ExternalLogger).GetMethod(
            "Write",
            new[] { typeof(string) })!;
        string? captured = null;

        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        shims.ReplaceVoidMethod(method)
            .WithArguments(ShimArg.Eq("hello"))
            .Callback(context => captured = (string)context.Arguments[0]!);

        var caller = shims.CreateObject(CallerTypeName);
        Assert.AreEqual("completed", shims.Invoke<string>(caller, "CallLogger", "hello"));
        Assert.AreEqual("hello", captured);
    }

    [TestMethod]
    public void VoidApi_DoNothing_SuppressesRealMethod()
    {
        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        shims.ReplaceVoidMethod<ExternalLogger>("Write", typeof(string))
            .DoNothing();

        var caller = shims.CreateObject(CallerTypeName);
        Assert.AreEqual("completed", shims.Invoke<string>(caller, "CallLogger", "ignored"));
    }

    [TestMethod]
    public void ReturnAndVoidApiMismatch_IsRejectedAtRegistration()
    {
        var voidMethod = typeof(ExternalLogger).GetMethod("Write")!;

        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        Assert.ThrowsException<ShimMethodSignatureException>(
            () => shims.ReplaceMethod<int>(voidMethod));
        Assert.ThrowsException<ShimMethodSignatureException>(
            () => shims.ReplaceVoidMethod(LoadMethod));
        Assert.ThrowsException<ShimMethodSignatureException>(
            () => shims.ReplaceMethod<string>(LoadMethod));
        Assert.ThrowsException<ShimMethodSignatureException>(
            () => shims.ReplaceMethod<int>(LoadMethod)
                .WithArguments(ShimArg.Any<object>()));
        Assert.ThrowsException<ShimMethodSignatureException>(
            () => shims.ReplaceMethod<int>(LoadMethod)
                .WithArguments(
                    ShimArg.Any<object>(),
                    ShimArg.Any<DateTime>(),
                    ShimArg.Any<bool>()));
    }

    [TestMethod]
    public void NullParameterTypes_IsRejectedWithCandidateDiagnostics()
    {
        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        var exception = Assert.ThrowsException<ShimMethodSignatureException>(
            () => shims.ReplaceMethod<int>(
                typeof(ExternalTableLoader),
                "Load",
                (Type[])null!));

        StringAssert.Contains(exception.Message, "Requested parameter types:");
        StringAssert.Contains(exception.Message, "Candidate methods:");
        StringAssert.Contains(exception.Message, "ParameterTypesRequired");
        StringAssert.Contains(exception.Message, "Type.EmptyTypes");
    }

    [TestMethod]
    public void StaticMethod_IsRejectedAndStaticApiIsSuggested()
    {
        var method = typeof(ExternalTableLoader).GetMethod(
            "StaticLoad",
            BindingFlags.Public | BindingFlags.Static)!;

        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        var exception = Assert.ThrowsException<ShimMethodSignatureException>(
            () => shims.ReplaceMethod<int>(method));

        StringAssert.Contains(exception.Message, "StaticMethodPassedToInstanceApi");
        StringAssert.Contains(exception.Message, "Static<TResult>");
    }

    [TestMethod]
    public void VirtualAndNonVirtual_AreDetectedFromMethodInfo()
    {
        var virtualMethod = typeof(ExternalTableLoader).GetMethod(
            "VirtualLoad",
            new[] { typeof(int) })!;

        using (var virtualShims = Shims.ForAssembly(TargetAssemblyPath))
        {
            var builder = virtualShims.ReplaceMethod<int>(virtualMethod);
            Assert.IsTrue(builder.Method.IsVirtual);
            Assert.AreEqual(MethodInterceptionBackend.InstanceCallSiteRewrite, builder.Backend);
            builder.Returns(77);

            var caller = virtualShims.CreateObject(CallerTypeName);
            Assert.AreEqual(77, virtualShims.Invoke<int>(caller, "CallVirtual", 1));
        }

        using (var nonVirtualShims = Shims.ForAssembly(TargetAssemblyPath))
        {
            var builder = nonVirtualShims.ReplaceMethod<int>(LoadMethod);
            Assert.IsFalse(builder.Method.IsVirtual);
            Assert.AreEqual(MethodInterceptionBackend.InstanceCallSiteRewrite, builder.Backend);
        }
    }

    [TestMethod]
    public void Throws_UsesTypedRule()
    {
        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        shims.ReplaceMethod<int>(LoadMethod)
            .Throws(new ShimException("typed failure"));

        var exception = Assert.ThrowsException<ShimException>(
            () => shims.CreateObject(ConstructorTypeName));
        Assert.AreEqual("typed failure", exception.Message);
    }

    [TestMethod]
    public void LegacyNullForInt_ThrowsDedicatedReturnTypeExceptionBeforeUnbox()
    {
        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        shims.ReplaceMethod(
            typeof(ExternalTableLoader),
            "Load",
            (receiver, arguments) => null);

        var exception = Assert.ThrowsException<ShimReturnTypeMismatchException>(
            () => shims.CreateObject(ConstructorTypeName));

        StringAssert.Contains(
            exception.Message,
            "returned null for a non-nullable value type");
        StringAssert.Contains(exception.Message, "System.Int32");
        StringAssert.Contains(exception.Message, "(recv, args) => (object)0");
        StringAssert.Contains(exception.Message, "ReplaceMethod<int>(methodInfo).Returns(0)");
        Assert.IsTrue(shims.LastMethodDispatchDiagnostics!.NullReturnedForNonNullableValueType);
        Assert.AreEqual("legacy untyped API", shims.LastMethodDispatchDiagnostics.RegistrationSource);
    }

    [TestMethod]
    public void NoMatchingTypedRule_FallsBackToOriginal()
    {
        var getName = typeof(ExternalGateway).GetMethod(
            "GetName",
            new[] { typeof(int) })!;

        using var shims = Shims.ForAssembly(TargetAssemblyPath);
        shims.ReplaceMethod<string>(getName)
            .WithArguments(ShimArg.Eq(99))
            .Returns("not-used");

        var service = shims.CreateObject("CrossAssemblySample.GatewayUserService");
        Assert.AreEqual("real-2", shims.Invoke<string>(service, "Run", 2));
        Assert.IsFalse(shims.LastMethodDispatchDiagnostics!.ReplacementFound);
    }
}
