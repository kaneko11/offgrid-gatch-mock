using System;
using System.Reflection;
using ExternalLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    /// <summary>Phase 25 type-safe method API coverage compiled with C# 7.3 on net48.</summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48TypeSafeMethodReplacementTests
    {
        private const string ConstructorTypeName =
            "CrossAssemblySample.ConstructorCallsIntMethod";
        private const string CallerTypeName =
            "CrossAssemblySample.TypedMethodCaller";

        private static string TargetAssemblyPath
        {
            get
            {
                return typeof(CrossAssemblySample.ConstructorCallsIntMethod)
                    .Assembly.Location;
            }
        }

        private static MethodInfo LoadMethod
        {
            get
            {
                return typeof(ExternalTableLoader).GetMethod(
                    "Load",
                    new[] { typeof(object), typeof(string), typeof(bool) });
            }
        }

        [TestMethod]
        public void MethodInfo_IntMethod_InConstructor_Completes()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath))
            {
                shims.ReplaceMethod<int>(LoadMethod).Returns(0);

                object service = shims.CreateObject(ConstructorTypeName);
                Assert.IsTrue(shims.GetValue<bool>(service, "Initialized"));
            }
        }

        [TestMethod]
        public void GenericTargetAndTypeEmptyTypes_WorkWithCSharp73()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath))
            {
                shims.ReplaceMethod<ExternalTableLoader, int>(
                        "NoArguments",
                        Type.EmptyTypes)
                    .Returns(48);

                object caller = shims.CreateObject(CallerTypeName);
                Assert.AreEqual(
                    48,
                    shims.Invoke<int>(caller, "CallNoArguments"));
            }
        }

        [TestMethod]
        public void VoidApi_DoNothing_WorksOnNet48()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath))
            {
                shims.ReplaceVoidMethod<ExternalLogger>(
                        "Write",
                        typeof(string))
                    .DoNothing();

                object caller = shims.CreateObject(CallerTypeName);
                Assert.AreEqual(
                    "completed",
                    shims.Invoke<string>(caller, "CallLogger", "net48"));
            }
        }

        [TestMethod]
        public void LegacyNullForInt_ThrowsDedicatedExceptionOnNet48()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath))
            {
                shims.ReplaceMethod(
                    typeof(ExternalTableLoader),
                    "Load",
                    delegate(object receiver, object[] arguments)
                    {
                        return null;
                    });

                ShimReturnTypeMismatchException exception =
                    Assert.ThrowsException<ShimReturnTypeMismatchException>(
                        delegate
                        {
                            shims.CreateObject(ConstructorTypeName);
                        });

                StringAssert.Contains(
                    exception.Message,
                    "returned null for a non-nullable value type");
            }
        }

        [TestMethod]
        public void OptionalParameterSignature_UsesAllThreeTypesOnNet48()
        {
            using (Shims shims = Shims.ForAssembly(TargetAssemblyPath))
            {
                shims.ReplaceMethod<int>(
                        typeof(ExternalTableLoader),
                        "Load",
                        typeof(object),
                        typeof(string),
                        typeof(bool))
                    .Returns(25);

                object caller = shims.CreateObject(CallerTypeName);
                Assert.AreEqual(
                    25,
                    shims.Invoke<int>(caller, "CallLoad", "sql", true));
            }
        }
    }
}
