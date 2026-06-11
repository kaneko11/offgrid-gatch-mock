using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniMockito;
using MiniMockito.Exceptions;
using MiniMockito.Proxy;
using MiniMockito.Verification;
using MiniMockito.Net48X86Tests.Samples;
using static MiniMockito.Mock;

namespace MiniMockito.Net48X86Tests
{
    /// <summary>
    /// Phase 18 — verifies interface mock / spy / strict / async work on
    /// .NET Framework 4.8 + PlatformTarget=x86, where DispatchProxy previously failed
    /// with TypeLoadException. These exercise the RealProxy fallback backend.
    /// </summary>
    [TestClass]
    public sealed class Net48X86InterfaceMockTests
    {
        // ── regression / backend selection ───────────────────────────────────

        [TestMethod]
        public void Net48X86_InterfaceMock_DoesNotThrowTypeLoadException()
        {
            // Before Phase 18 this threw:
            //   TypeLoadException: access is denied 'MiniMockito.Proxy.MiniMockitoDispatchProxy'
            // under net48 + x86. The RealProxy backend avoids that code path.
            IUserRepository repo = Mock.Of<IUserRepository>();

            Assert.IsNotNull(repo);
            Assert.IsInstanceOfType(repo, typeof(IUserRepository));
        }

        [TestMethod]
        public void Net48X86_MockOf_Interface_DoesNotUseBrokenDispatchProxyPath()
        {
            ProxyBackendInfo info = ProxyBackendDiagnostics.Describe();

            Assert.AreEqual("RealProxy", info.SelectedBackend, info.ToString());
            Assert.AreEqual("net48", info.TargetFramework, info.ToString());
        }

        // ── 1. Mock.Of<T>() ──────────────────────────────────────────────────

        [TestMethod]
        public void MockOf_CreatesInterfaceMock()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();
            Assert.IsNotNull(repo);
        }

        // ── 2. ThenReturn ────────────────────────────────────────────────────

        [TestMethod]
        public void ThenReturn_ReturnsStubbedValue()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();

            When(() => repo.FindById(Any<int>())).ThenReturn("mocked");

            Assert.AreEqual("mocked", repo.FindById(1));
        }

        // ── 3 & 4. Verify / Verify + Times.Once ──────────────────────────────

        [TestMethod]
        public void Verify_Succeeds()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();

            repo.FindById(1);

            Verify(() => repo.FindById(1));
        }

        [TestMethod]
        public void Verify_WithTimesOnce_Succeeds()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();

            repo.FindById(7);

            Verify(() => repo.FindById(7), Times.Once());
        }

        // ── 5. Strict mock ───────────────────────────────────────────────────

        [TestMethod]
        public void StrictMock_UnstubbedCall_Throws()
        {
            IUserService strict = Mock.Of<IUserService>(MockBehavior.Strict);

            Assert.ThrowsException<MockException>(() => strict.GetName(1));
        }

        // ── 6. Lenient default ───────────────────────────────────────────────

        [TestMethod]
        public void LenientMock_UnstubbedCall_ReturnsDefault()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();

            Assert.IsNull(repo.FindById(1));     // reference default
            Assert.AreEqual(0, repo.Count());    // value default
        }

        // ── 7 & 8. Spy delegation / partial stub ─────────────────────────────

        [TestMethod]
        public void Spy_DelegatesToRealImplementation()
        {
            RealUserService real = new RealUserService();
            IUserService spy = Spy.Of<IUserService>(real);

            Assert.AreEqual("real-7", spy.GetName(7));
        }

        [TestMethod]
        public void Spy_StubbedMethod_OverridesReal()
        {
            RealUserService real = new RealUserService();
            IUserService spy = Spy.Of<IUserService>(real);

            When(() => spy.GetName(0)).ThenReturn("stubbed");

            Assert.AreEqual("stubbed", spy.GetName(0));  // stub wins
            Assert.AreEqual("real-7", spy.GetName(7));   // unstubbed → real
        }

        // ── 9, 10, 11. Matchers ──────────────────────────────────────────────

        [TestMethod]
        public void AnyMatcher_Works()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();
            When(() => repo.FindById(Any<int>())).ThenReturn("any");
            Assert.AreEqual("any", repo.FindById(123));
        }

        [TestMethod]
        public void EqMatcher_Works()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();
            When(() => repo.FindById(Eq(10))).ThenReturn("ten");
            Assert.AreEqual("ten", repo.FindById(10));
            Assert.IsNull(repo.FindById(11));
        }

        [TestMethod]
        public void IsMatcher_Works()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();
            When(() => repo.FindById(Is<int>(v => v > 0))).ThenReturn("positive");
            Assert.AreEqual("positive", repo.FindById(5));
            Assert.IsNull(repo.FindById(-1));
        }

        // ── 12. Captor ───────────────────────────────────────────────────────

        [TestMethod]
        public void Captor_CapturesArgument()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();

            repo.Save("abc");

            ArgumentCaptor<string> captor = Capture<string>();
            Verify(() => repo.Save(captor.Value));

            Assert.AreEqual("abc", captor.CapturedValue);
        }

        // ── 13. ThenThrow ────────────────────────────────────────────────────

        [TestMethod]
        public void ThenThrow_ThrowsConfiguredException()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();

            When(() => repo.FindById(1)).ThenThrow(new InvalidOperationException("boom"));

            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
                () => repo.FindById(1));
            Assert.AreEqual("boom", ex.Message);
        }

        // ── 14. ThenAnswer ───────────────────────────────────────────────────

        [TestMethod]
        public void ThenAnswer_ProducesDynamicValue()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();

            When(() => repo.FindById(Any<int>()))
                .ThenAnswer(ctx => "id=" + ctx.Arguments[0]);

            Assert.AreEqual("id=42", repo.FindById(42));
        }

        // ── 15. ThenReturnSequence ───────────────────────────────────────────

        [TestMethod]
        public void ThenReturnSequence_ReturnsValuesInOrder()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();

            When(() => repo.FindById(2)).ThenReturnSequence("a", "b", "c");

            Assert.AreEqual("a", repo.FindById(2));
            Assert.AreEqual("b", repo.FindById(2));
            Assert.AreEqual("c", repo.FindById(2));
            Assert.AreEqual("c", repo.FindById(2)); // last value repeats
        }

        // ── 16 & 17. Task<T> ─────────────────────────────────────────────────

        [TestMethod]
        public async Task TaskOfT_StubbedReturn_Works()
        {
            IUserService svc = Mock.Of<IUserService>();

            When(() => svc.GetNameAsync(Any<int>())).ThenReturn("async-stub");

            string result = await svc.GetNameAsync(1);
            Assert.AreEqual("async-stub", result);
        }

        [TestMethod]
        public async Task TaskOfT_UnstubbedDefault_Works()
        {
            IUserService svc = Mock.Of<IUserService>();

            string result = await svc.GetNameAsync(1);
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task Task_UnstubbedDefault_CompletesNormally()
        {
            IUserService svc = Mock.Of<IUserService>();
            await svc.DoWorkAsync();
        }

        // ── 18 & 19. ValueTask<T> ────────────────────────────────────────────

        [TestMethod]
        public async Task ValueTaskOfT_StubbedReturn_Works()
        {
            IUserService svc = Mock.Of<IUserService>();

            When(() => svc.GetCountAsync(Any<int>())).ThenReturn(7);

            int result = await svc.GetCountAsync(1);
            Assert.AreEqual(7, result);
        }

        [TestMethod]
        public async Task ValueTaskOfT_UnstubbedDefault_Works()
        {
            IUserService svc = Mock.Of<IUserService>();

            int result = await svc.GetCountAsync(1);
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public async Task ValueTask_UnstubbedDefault_CompletesNormally()
        {
            IUserService svc = Mock.Of<IUserService>();
            await svc.DoValueWorkAsync();
        }

        // ── 20. VerifyNoInteractions ─────────────────────────────────────────

        [TestMethod]
        public void VerifyNoInteractions_PassesForUntouchedMock()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();
            VerifyNoInteractions(repo);
        }

        // ── 21. VerifyNoMoreInteractions ─────────────────────────────────────

        [TestMethod]
        public void VerifyNoMoreInteractions_PassesWhenAllVerified()
        {
            IUserRepository repo = Mock.Of<IUserRepository>();

            repo.Save("abc");
            Verify(() => repo.Save("abc"));

            VerifyNoMoreInteractions(repo);
        }

        // ── 22. InOrder ──────────────────────────────────────────────────────

        [TestMethod]
        public void InOrder_VerifiesSequenceAcrossMocks()
        {
            IWorkflowStep first = Mock.Of<IWorkflowStep>();
            IWorkflowStep second = Mock.Of<IWorkflowStep>();

            first.Start();
            second.Save();
            first.End();

            InOrderContext order = InOrder(first, second);
            order.Verify(() => first.Start());
            order.Verify(() => second.Save());
            order.Verify(() => first.End());
        }
    }
}
