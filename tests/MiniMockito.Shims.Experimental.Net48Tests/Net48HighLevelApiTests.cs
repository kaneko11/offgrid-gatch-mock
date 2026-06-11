using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito.Shims.Experimental;
using MiniMockito.Shims.Experimental.Net48Tests.Samples;

namespace MiniMockito.Shims.Experimental.Net48Tests
{
    /// <summary>
    /// Phase 17 — high-level <c>Shims</c> facade tests on .NET Framework 4.8 / C# 7.3.
    /// Uses using-statements (not using-declarations) and no nullable annotations.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public sealed class Net48HighLevelApiTests
    {
        [TestMethod]
        public void HighLevel_ParameterlessNew_IsShimmed()
        {
            using (Shims shims = Shims.For<Net48UserService>().WithNew<Net48UserRepository>())
            {
                object fakeRepo = shims.CreateFake<Net48UserRepository>("fake");
                shims.New<Net48UserRepository>().Returns(fakeRepo);

                object service = shims.CreateObject(typeof(Net48UserService).FullName);
                string result = shims.Invoke<string>(service, "GetDisplayName", 1);

                Assert.AreEqual("fake-1", result);
            }
        }

        [TestMethod]
        public void HighLevel_ConstructorArgsNew_WithEqMatcher_IsShimmed()
        {
            using (Shims shims = Shims.For<Net48UserService>().WithNew<Net48UserRepository>())
            {
                object fakeRepo = shims.CreateFake<Net48UserRepository>("fake");
                shims.New<Net48UserRepository>()
                     .WithArguments(ShimArg.Eq<string>("prod"))
                     .Returns(fakeRepo);

                object service = shims.CreateObject(typeof(Net48UserService).FullName);
                string result = shims.Invoke<string>(service, "GetDisplayNameWithArg", 1);

                Assert.AreEqual("fake-1", result);
            }
        }

        [TestMethod]
        public void HighLevel_ShimCaptor_CapturesConstructorArg()
        {
            using (Shims shims = Shims.For<Net48UserService>().WithNew<Net48UserRepository>())
            {
                ShimCaptor<string> captor = ShimCaptor.For<string>();
                object fakeRepo = shims.CreateFake<Net48UserRepository>("fake");
                shims.New<Net48UserRepository>()
                     .WithArguments(captor)
                     .Returns(fakeRepo);

                object service = shims.CreateObject(typeof(Net48UserService).FullName);
                shims.Invoke<string>(service, "GetDisplayNameWithArg", 1);

                Assert.IsTrue(captor.HasValue);
                Assert.AreEqual("prod", captor.Value);
            }
        }

        [TestMethod]
        public void HighLevel_StaticMethod_IsShimmed()
        {
            using (Shims shims = Shims.For<Net48TimedService>().WithStatic(typeof(Net48StaticClock)))
            {
                shims.Static<string>(typeof(Net48StaticClock), "GetLabel", typeof(int))
                     .WithArguments(ShimArg.Eq(1))
                     .Returns("fake-label");

                object service = shims.CreateObject(typeof(Net48TimedService).FullName);
                string result = shims.Invoke<string>(service, "GetLabel", 1);

                Assert.AreEqual("fake-label", result);
            }
        }

        [TestMethod]
        public void HighLevel_VoidStaticMethod_IsShimmed()
        {
            using (Shims shims = Shims.For<Net48TimedService>().WithStatic(typeof(Net48StaticClock)))
            {
                string recorded = null;
                shims.Static(typeof(Net48StaticClock), "RecordCall", typeof(string))
                     .Callback(args => recorded = (string)args[0]);

                object service = shims.CreateObject(typeof(Net48TimedService).FullName);
                shims.Invoke(service, "RecordCall", "hello");

                Assert.AreEqual("hello", recorded);
            }
        }

        [TestMethod]
        public void HighLevel_NewAndStatic_Coexist()
        {
            using (Shims shims = Shims.For<Net48UserService>()
                .WithNew<Net48UserRepository>()
                .WithStatic(typeof(Net48StaticClock)))
            {
                object fakeRepo = shims.CreateFake<Net48UserRepository>("fake");
                shims.New<Net48UserRepository>().Returns(fakeRepo);
                shims.Static<string>(typeof(Net48StaticClock), "GetLabel", typeof(int))
                     .Returns("static-label");

                object userService = shims.CreateObject(typeof(Net48UserService).FullName);
                Assert.AreEqual("fake-1", shims.Invoke<string>(userService, "GetDisplayName", 1));

                object timedService = shims.CreateObject(typeof(Net48TimedService).FullName);
                Assert.AreEqual("static-label", shims.Invoke<string>(timedService, "GetLabel", 1));
            }
        }

        [TestMethod]
        public void HighLevel_Create_SharedContract_Works()
        {
            using (Shims shims = Shims.For<Net48CreatableService>().WithNew<Net48UserRepository>())
            {
                object fakeRepo = shims.CreateFake<Net48UserRepository>("fake");
                shims.New<Net48UserRepository>().Returns(fakeRepo);

                IShimCreatable service = shims.Create<IShimCreatable>();
                string result = service.Describe();

                Assert.AreEqual("fake-99", result);
            }
        }

        [TestMethod]
        public void HighLevel_Create_ConcreteType_ThrowsWithGuidance()
        {
            using (Shims shims = Shims.For<Net48UserService>().WithNew<Net48UserRepository>())
            {
                InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
                    () => shims.Create<Net48UserService>());

                StringAssert.Contains(ex.Message, "isolated load context");
                StringAssert.Contains(ex.Message, "CreateObject");
            }
        }

        [TestMethod]
        public void HighLevel_CreateObject_And_Invoke_Fallback_Works()
        {
            using (Shims shims = Shims.For<Net48UserService>().WithNew<Net48UserRepository>())
            {
                object fakeRepo = shims.CreateFake<Net48UserRepository>("fake");
                shims.New<Net48UserRepository>().Returns(fakeRepo);

                object service = shims.CreateObject(typeof(Net48UserService).FullName);
                string result = shims.Invoke<string>(service, "GetDisplayName", 1);

                Assert.AreEqual("fake-1", result);
            }
        }

        [TestMethod]
        public void HighLevel_WithNew_AfterFinalize_Throws()
        {
            using (Shims shims = Shims.For<Net48UserService>().WithNew<Net48UserRepository>())
            {
                shims.CreateObject(typeof(Net48UserService).FullName);

                Assert.ThrowsException<InvalidOperationException>(
                    () => shims.WithNew<Net48UserRepository>());
            }
        }

        [TestMethod]
        public void HighLevel_WithStatic_AfterFinalize_Throws()
        {
            using (Shims shims = Shims.For<Net48UserService>().WithNew<Net48UserRepository>())
            {
                shims.CreateObject(typeof(Net48UserService).FullName);

                Assert.ThrowsException<InvalidOperationException>(
                    () => shims.WithStatic(typeof(Net48StaticClock)));
            }
        }
    }
}
