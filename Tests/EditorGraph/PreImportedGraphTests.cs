using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>
    /// Runs a graph that was imported by an <em>earlier Unity process</em>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every other test in this assembly builds its graph and runs it in one session, so an
    /// implementation that quietly depended on something still being in memory would pass them all.
    /// These tests only load: the file was created, imported and compiled by a different process that
    /// has since exited, and nothing here touches Graph Toolkit or the compiler.
    /// </para>
    /// <para>
    /// The fixture is created by the validation run, not by a test, so these are ignored when it is not
    /// present rather than failing in an ordinary project.
    /// </para>
    /// </remarks>
    public sealed class PreImportedGraphTests
    {
        /// <summary>Where the validation run leaves a graph for the next process to pick up.</summary>
        public const string FixturePath = "Assets/FyniteReloadFixture/Machine.fyn";

        private FyniteGraphAsset _asset;

        [SetUp]
        public void SetUp()
        {
            _asset = AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(FixturePath);

            if (_asset == null)
            {
                Assert.Ignore(
                    "No pre-imported graph at '" + FixturePath + "'. This runs as part of the cross-process " +
                    "reload validation, which creates it in a previous Unity session.");
            }
        }

        [Test]
        public void TheAssetSurvivedTheProcessThatBuiltIt()
        {
            Assert.IsNull(_asset.CompilationError, _asset.CompilationError);
            Assert.IsTrue(_asset.IsExecutable, "the graph compiled in an earlier process is still runnable");
            Assert.IsNotNull(_asset.GraphGuid);
        }

        [Test]
        public void TheContextTypeSurvivedSerialization()
        {
            // The binder is a closed generic rebuilt by Unity's deserializer, not by reflection at run
            // time. If that had not survived, the context type would be gone.
            Assert.AreEqual(typeof(GraphTestContext), _asset.ContextType);
            Assert.AreEqual(typeof(GraphTestContext), _asset.Definition.ContextType);
        }

        [Test]
        public void TheFactoriesStillProduceWorkingBlocks()
        {
            var host = new GameObject("reloaded");
            var context = host.AddComponent<GraphTestContext>();

            try
            {
                var machine = _asset.CreateMachine(context);
                machine.Start();

                Assert.AreEqual("Idle", machine.Definition.GetStateName(machine.ActiveLeaf));
                Assert.AreEqual("idleEnter#1", context.TraceText, "the configured block still carries its data");

                machine.Tick(0.1f);
                machine.FixedTick(0.02f);
                Assert.AreEqual(1, context.Ticks);
                Assert.AreEqual(1, context.FixedTicks);

                var move = _asset.Signals.First(s => s.name == "Move").guid;
                Assert.IsTrue(_asset.TryResolveSignal(move, out var handle));

                machine.Raise(handle);
                machine.Tick(0f);
                Assert.AreEqual("Moving", machine.Definition.GetStateName(machine.ActiveLeaf));

                machine.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PayloadTypesSurvivedWithoutBeingLookedUpByName()
        {
            var notify = _asset.Signals.First(s => s.name == "Notify").guid;
            Assert.IsTrue(_asset.TryResolveSignal(notify, out var handle));
            Assert.AreEqual(typeof(string), _asset.Definition.GetPayloadType(handle));
        }

        [Test]
        public void TwoMachinesFromTheReloadedAssetStayIsolated()
        {
            var firstHost = new GameObject("first");
            var secondHost = new GameObject("second");

            try
            {
                var first = firstHost.AddComponent<GraphTestContext>();
                var second = secondHost.AddComponent<GraphTestContext>();

                var a = _asset.CreateMachine(first);
                var b = _asset.CreateMachine(second);

                a.Start();
                b.Start();

                var move = _asset.Signals.First(s => s.name == "Move").guid;
                _asset.TryResolveSignal(move, out var handle);

                a.Raise(handle);
                a.Tick(0f);

                Assert.AreEqual("Moving", a.Definition.GetStateName(a.ActiveLeaf));
                Assert.AreEqual("Idle", b.Definition.GetStateName(b.ActiveLeaf));
                Assert.AreSame(a.Definition, b.Definition);

                a.Dispose();
                b.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(firstHost);
                Object.DestroyImmediate(secondHost);
            }
        }
    }
}
