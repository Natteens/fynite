using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fynite.Tests
{
    /// <summary>
    /// Runner behaviour that genuinely needs the player loop: OnEnable, Update, FixedUpdate, OnDisable
    /// and OnDestroy.
    /// </summary>
    [TestFixture]
    internal sealed class FyniteRunnerPlayModeTests
    {
        private GameObject _host;
        private RunnerTestDefinitionAsset _asset;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("runner-host");
            _host.SetActive(false);
            _asset = ScriptableObject.CreateInstance<RunnerTestDefinitionAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.Destroy(_host);
            }

            Object.Destroy(_asset);
        }

        [UnityTest]
        public IEnumerator EnablingTheRunnerStartsTheMachine()
        {
            var context = _host.AddComponent<RunnerTestContext>();
            var runner = _host.AddComponent<FyniteRunner>();
            runner.Bind(_asset, context);

            _host.SetActive(true);
            yield return null;

            Assert.IsTrue(runner.IsRunning);
            Assert.AreEqual(1, context.Entered);
        }

        [UnityTest]
        public IEnumerator UpdateForwardsTickAndFixedUpdateForwardsFixedTick()
        {
            var context = _host.AddComponent<RunnerTestContext>();
            var runner = _host.AddComponent<FyniteRunner>();
            runner.Bind(_asset, context);

            _host.SetActive(true);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.Greater(context.Ticks, 0, "Update should forward Tick.");
            Assert.Greater(context.FixedTicks, 0, "FixedUpdate should forward FixedTick.");
            Assert.IsTrue(runner.IsRunning);
        }

        [UnityTest]
        public IEnumerator DisablingStopsTheMachineAndReenablingRestartsIt()
        {
            var context = _host.AddComponent<RunnerTestContext>();
            var runner = _host.AddComponent<FyniteRunner>();
            runner.Bind(_asset, context);

            _host.SetActive(true);
            yield return null;

            var machine = runner.Machine;
            runner.enabled = false;

            Assert.IsFalse(runner.IsRunning);
            Assert.AreEqual(1, context.Exited);
            Assert.AreEqual(FyniteLifecycle.Stopped, machine.Lifecycle);

            runner.enabled = true;
            yield return null;

            Assert.IsTrue(runner.IsRunning);
            Assert.AreEqual(2, context.Entered);
            Assert.AreSame(machine, runner.Machine, "The runner must reuse its machine instead of rebuilding it.");
        }

        [UnityTest]
        public IEnumerator DestroyingTheRunnerDisposesItsMachine()
        {
            var context = _host.AddComponent<RunnerTestContext>();
            var runner = _host.AddComponent<FyniteRunner>();
            runner.Bind(_asset, context);

            _host.SetActive(true);
            yield return null;

            var machine = runner.Machine;
            Object.Destroy(runner);
            yield return null;

            Assert.AreEqual(FyniteLifecycle.Disposed, machine.Lifecycle);
            Assert.AreEqual(1, context.Exited);
        }

        [UnityTest]
        public IEnumerator SignalsRaisedThroughTheRunnerReachTheMachine()
        {
            var context = _host.AddComponent<RunnerTestContext>();
            var runner = _host.AddComponent<FyniteRunner>();
            runner.Bind(_asset, context);

            _host.SetActive(true);
            yield return null;

            runner.Raise(RunnerTestDefinitionAsset.Advance);

            Assert.AreEqual(1, context.Advanced);
            Assert.AreEqual("Working", runner.Machine.Definition.GetStateName(runner.Machine.ActiveLeaf));
        }

        [UnityTest]
        public IEnumerator TwoRunnersSharingOneAssetStayIndependent()
        {
            var firstContext = _host.AddComponent<RunnerTestContext>();
            var firstRunner = _host.AddComponent<FyniteRunner>();
            firstRunner.Bind(_asset, firstContext);

            var secondHost = new GameObject("second-host");
            secondHost.SetActive(false);
            var secondContext = secondHost.AddComponent<RunnerTestContext>();
            var secondRunner = secondHost.AddComponent<FyniteRunner>();
            secondRunner.Bind(_asset, secondContext);

            _host.SetActive(true);
            secondHost.SetActive(true);
            yield return null;

            firstRunner.Raise(RunnerTestDefinitionAsset.Advance);

            Assert.AreEqual("Working", firstRunner.Machine.Definition.GetStateName(firstRunner.Machine.ActiveLeaf));
            Assert.AreEqual("Ready", secondRunner.Machine.Definition.GetStateName(secondRunner.Machine.ActiveLeaf));
            Assert.AreEqual(1, firstContext.Advanced);
            Assert.AreEqual(0, secondContext.Advanced);

            Object.Destroy(secondHost);
        }

        [UnityTest]
        public IEnumerator AMisconfiguredRunnerLogsOnceAndStaysInert()
        {
            _host.AddComponent<FyniteRunner>();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("has no definition assigned"));
            _host.SetActive(true);
            yield return null;
            yield return null;

            Assert.IsFalse(_host.GetComponent<FyniteRunner>().IsRunning);
        }
    }
}
