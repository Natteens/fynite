using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fynite.Tests
{
    /// <summary>
    /// Drives a compiled graph through a real <see cref="FyniteRunner"/> in play mode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the last link in the chain: the graph was authored in the editor, compiled by the
    /// importer in an earlier process, and here it is executed by the component that ships with the
    /// package, on Unity's own player loop.
    /// </para>
    /// <para>
    /// The asset is loaded through the asset database, which only exists in the editor. That is a
    /// property of the test, not of the runtime: in a player the reference would come from a scene or a
    /// prefab, which is exactly what the player build validation covers.
    /// </para>
    /// </remarks>
    public sealed class CompiledGraphPlayModeTests
    {
        private const string FixturePath = "Assets/FyniteReloadFixture/Machine.fyn";

        private GameObject _host;

        private static FyniteGraphAsset LoadFixture()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(FixturePath);
#else
            return null;
#endif
        }

        [SetUp]
        public void SetUp()
        {
            if (LoadFixture() == null)
            {
                Assert.Ignore(
                    "No compiled graph at '" + FixturePath + "'. The validation run creates it in an " +
                    "earlier Unity session.");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
                _host = null;
            }
        }

        private FyniteRunner CreateRunner(out GraphTestContext context)
        {
            _host = new GameObject("compiled-graph-runner");
            _host.SetActive(false);

            context = _host.AddComponent<GraphTestContext>();
            var runner = _host.AddComponent<FyniteRunner>();
            runner.Bind(LoadFixture(), context);

            _host.SetActive(true);
            return runner;
        }

        [UnityTest]
        public IEnumerator TheRunnerStartsTheCompiledGraphAndTicksIt()
        {
            var runner = CreateRunner(out var context);

            Assert.IsTrue(runner.IsRunning, "enabling the runner started the machine");
            Assert.AreEqual("Idle", runner.Machine.Definition.GetStateName(runner.Machine.ActiveLeaf));
            Assert.AreEqual("idleEnter#1", context.TraceText);

            int ticksBefore = context.Ticks;
            yield return null;

            Assert.Greater(context.Ticks, ticksBefore, "Update drives Tick");
        }

        [UnityTest]
        public IEnumerator FixedUpdateDrivesFixedTickSeparately()
        {
            var runner = CreateRunner(out var context);
            Assert.IsTrue(runner.IsRunning);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.Greater(context.FixedTicks, 0, "FixedUpdate drives FixedTick");
        }

        [UnityTest]
        public IEnumerator APersistentSignalReferenceDrivesATransition()
        {
            var runner = CreateRunner(out var context);
            var asset = LoadFixture();

            var move = new FyniteSignalReference(asset, asset.Signals.First(s => s.name == "Move").guid);
            runner.Raise(move);

            yield return null;

            Assert.AreEqual("Moving", runner.Machine.Definition.GetStateName(runner.Machine.ActiveLeaf));

            var stop = new FyniteSignalReference(asset, asset.Signals.First(s => s.name == "Stop").guid);
            runner.Raise(stop);

            yield return null;

            Assert.AreEqual("Idle", runner.Machine.Definition.GetStateName(runner.Machine.ActiveLeaf));
            StringAssert.Contains("moveEffect", context.TraceText);
        }

        [UnityTest]
        public IEnumerator APayloadSignalReachesItsEffectWithoutMovingTheActivePath()
        {
            var runner = CreateRunner(out var context);
            var asset = LoadFixture();

            runner.Raise(new FyniteSignalReference(asset, asset.Signals.First(s => s.name == "Move").guid));
            yield return null;

            var leafBefore = runner.Machine.ActiveLeaf;

            runner.Raise(new FyniteSignalReference(asset, asset.Signals.First(s => s.name == "Notify").guid), "played");
            yield return null;

            Assert.AreEqual(leafBefore, runner.Machine.ActiveLeaf);
            Assert.AreEqual("played", context.LastPayload);
        }

        [UnityTest]
        public IEnumerator TheObservationReportsWhatTheMachineDid()
        {
            var runner = CreateRunner(out _);
            var asset = LoadFixture();

            runner.Raise(new FyniteSignalReference(asset, asset.Signals.First(s => s.name == "Move").guid));
            yield return null;

            var observation = runner.Observation;
            Assert.IsNotNull(observation, "the runner installs an observation in the editor and dev builds");
            Assert.IsTrue(observation.HasReaction);
            Assert.IsTrue(observation.HasTransition);

            var definition = runner.Machine.Definition;
            Assert.AreEqual("Idle", definition.GetStateName(observation.LastTransitionSource));
            Assert.AreEqual("Moving", definition.GetStateName(observation.LastTransitionTarget));
            Assert.AreEqual("Move", definition.GetSignalName(observation.LastSignal));
            Assert.IsNull(observation.Fault);
        }

        [UnityTest]
        public IEnumerator DisablingStopsTheMachineAndDestroyingDisposesIt()
        {
            var runner = CreateRunner(out _);
            Assert.IsTrue(runner.IsRunning);

            runner.enabled = false;
            yield return null;

            Assert.IsFalse(runner.IsRunning, "disabling stops the machine");
            Assert.AreEqual(FyniteLifecycle.Stopped, runner.Machine.Lifecycle);

            var machine = runner.Machine;
            Object.DestroyImmediate(_host);
            _host = null;

            Assert.AreEqual(FyniteLifecycle.Disposed, machine.Lifecycle, "destroying disposes it");
        }

        [UnityTest]
        public IEnumerator TwoRunnersOfTheSameGraphDoNotShareState()
        {
            var first = CreateRunner(out var firstContext);

            var secondHost = new GameObject("second-runner");
            secondHost.SetActive(false);
            var secondContext = secondHost.AddComponent<GraphTestContext>();
            var second = secondHost.AddComponent<FyniteRunner>();
            second.Bind(LoadFixture(), secondContext);
            secondHost.SetActive(true);

            try
            {
                var asset = LoadFixture();
                first.Raise(new FyniteSignalReference(asset, asset.Signals.First(s => s.name == "Move").guid));

                yield return null;

                Assert.AreEqual("Moving", first.Machine.Definition.GetStateName(first.Machine.ActiveLeaf));
                Assert.AreEqual("Idle", second.Machine.Definition.GetStateName(second.Machine.ActiveLeaf));
                Assert.AreEqual("idleEnter#1", secondContext.TraceText, "its blocks are its own");
                Assert.AreSame(first.Machine.Definition, second.Machine.Definition);
            }
            finally
            {
                Object.DestroyImmediate(secondHost);
            }
        }
    }
}
