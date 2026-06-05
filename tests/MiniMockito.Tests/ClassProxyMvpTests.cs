using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Exceptions;
using static MiniMockito.Mock;

namespace MiniMockito.Tests;

[TestClass]
public sealed class ClassProxyMvpTests
{
    [TestMethod]
    public void Class_WhenTargetIsPublicNonSealedClassWithParameterlessConstructor_CreatesMock()
    {
        var mock = Mock.Class<ClassProxySampleService>();

        Assert.IsNotNull(mock);
        Assert.IsInstanceOfType<ClassProxySampleService>(mock);
        Assert.AreNotEqual(typeof(ClassProxySampleService), mock.GetType());
    }

    [TestMethod]
    public void ClassProxy_PublicVirtualMethod_CanBeStubbedWithThenReturn()
    {
        var mock = Mock.Class<ClassProxySampleService>();

        When(() => mock.GetName(1)).ThenReturn("mocked");

        Assert.AreEqual("mocked", mock.GetName(1));
        Assert.IsNull(mock.GetName(2));
    }

    [TestMethod]
    public void ClassProxy_PublicVirtualMethod_CanBeStubbedWithThenThrow()
    {
        var mock = Mock.Class<ClassProxySampleService>();
        var exception = new InvalidOperationException("configured");

        When(() => mock.GetName(1)).ThenThrow(exception);

        var actual = Assert.ThrowsException<InvalidOperationException>(() => mock.GetName(1));
        Assert.AreSame(exception, actual);
    }

    [TestMethod]
    public void ClassProxy_PublicVirtualMethod_CanBeStubbedWithThenAnswer()
    {
        var mock = Mock.Class<ClassProxySampleService>();

        When(() => mock.GetName(Any<int>())).ThenAnswer(ctx => $"id={ctx.Arguments[0]}");

        Assert.AreEqual("id=42", mock.GetName(42));
    }

    [TestMethod]
    public void ClassProxy_PublicVirtualMethod_CanBeVerified()
    {
        var mock = Mock.Class<ClassProxySampleService>();

        mock.GetName(1);

        Verify(() => mock.GetName(1), Times.Once());
        VerifyNoMoreInteractions(mock);
    }

    [TestMethod]
    public void ClassProxy_TimesOnceAndExactly_Work()
    {
        var mock = Mock.Class<ClassProxySampleService>();

        mock.GetName(1);
        mock.GetName(1);

        Verify(() => mock.GetName(1), Times.Exactly(2));
        Verify(() => mock.GetName(2), Times.Never());
    }

    [TestMethod]
    public void ClassProxy_LenientUnstubbedVirtualMethod_ReturnsDefaultValue()
    {
        var mock = Mock.Class<ClassProxySampleService>();

        Assert.IsNull(mock.GetName(1));
        Assert.AreEqual(0, mock.GetCount());
        mock.Save("abc");

        Verify(() => mock.Save("abc"));
    }

    [TestMethod]
    public void ClassProxy_StrictUnstubbedVirtualMethod_ThrowsClassProxyException()
    {
        var mock = Mock.Class<ClassProxySampleService>(MockBehavior.Strict);

        var exception = Assert.ThrowsException<ClassProxyException>(() => mock.GetName(1));

        StringAssert.Contains(exception.Message, "Target class:");
        StringAssert.Contains(exception.Message, "Method: GetName");
        StringAssert.Contains(exception.Message, "Reason:");
        StringAssert.Contains(exception.Message, "Supported methods:");
        StringAssert.Contains(exception.Message, "Unsupported methods:");
        StringAssert.Contains(exception.Message, "Hint:");
    }

    [TestMethod]
    public void ClassProxy_WhenTargetIsInterface_ThrowsClassProxyException()
    {
        var exception = Assert.ThrowsException<ClassProxyException>(() => Mock.Class<IClassProxyInterface>());

        StringAssert.Contains(exception.Message, "Target class:");
        StringAssert.Contains(exception.Message, "Reason: NotAClass");
    }

    [TestMethod]
    public void ClassProxy_WhenTargetIsSealed_ThrowsClassProxyException()
    {
        var exception = Assert.ThrowsException<ClassProxyException>(() => Mock.Class<ClassProxySealedService>());

        StringAssert.Contains(exception.Message, "Target class:");
        StringAssert.Contains(exception.Message, "Reason: SealedClass");
        StringAssert.Contains(exception.Message, "Hint:");
    }

    [TestMethod]
    public void ClassProxy_WhenTargetHasNoParameterlessConstructor_ThrowsClassProxyException()
    {
        var exception = Assert.ThrowsException<ClassProxyException>(() => Mock.Class<ClassProxyNoParameterlessConstructorService>());

        StringAssert.Contains(exception.Message, "Target class:");
        StringAssert.Contains(exception.Message, "Reason: NoParameterlessConstructor");
        StringAssert.Contains(exception.Message, "Hint:");
    }

    [TestMethod]
    public void ClassProxy_WhenMethodIsNonVirtual_StubbingShowsUnsupportedDiagnostic()
    {
        var mock = Mock.Class<ClassProxyMixedService>();

        var exception = Assert.ThrowsException<ClassProxyException>(
            () => When(() => mock.NonVirtualName()).ThenReturn("mocked"));

        StringAssert.Contains(exception.Message, "Target class:");
        StringAssert.Contains(exception.Message, "Method:");
        StringAssert.Contains(exception.Message, "Reason: NonVirtualMethod");
        StringAssert.Contains(exception.Message, "Supported methods:");
        StringAssert.Contains(exception.Message, "Unsupported methods:");
        StringAssert.Contains(exception.Message, "Hint:");
    }

    [TestMethod]
    public void ClassProxy_DoesNotBreakExistingInterfaceMockUsage()
    {
        var mock = Mock.Of<IClassProxyInterface>();

        When(() => mock.GetName(Any<int>())).ThenReturn("interface");

        Assert.AreEqual("interface", mock.GetName(1));
        Verify(() => mock.GetName(1), Times.Once());
    }
}

public interface IClassProxyInterface
{
    string? GetName(int id);
}

public class ClassProxySampleService
{
    public virtual string? GetName(int id)
    {
        return $"real-{id}";
    }

    public virtual int GetCount()
    {
        return 123;
    }

    public virtual void Save(string value)
    {
    }
}

public sealed class ClassProxySealedService
{
    public string GetName()
    {
        return "sealed";
    }
}

public class ClassProxyNoParameterlessConstructorService
{
    public ClassProxyNoParameterlessConstructorService(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public virtual string GetName()
    {
        return Name;
    }
}

public class ClassProxyMixedService
{
    public virtual string VirtualName()
    {
        return "virtual";
    }

    public string NonVirtualName()
    {
        return "non-virtual";
    }
}
