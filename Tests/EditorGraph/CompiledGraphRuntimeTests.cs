using System.Linq;
using Fynite.GraphEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>
    /// The whole pipeline end to end: a real <c>.fyn</c> on disk, imported, and executed.
    /// </summary>
    /// <remarks>
    /// These are the tests that would catch a graph which compiles but does not behave — ordering, LCA,
    /// guards, payloads and isolation are all asserted against a machine built from the file rather than
    /// from a hand-written definition.
    /// </remarks>
    public sealed class CompiledGraphRuntimeTests
    {
        private FyniteGraphAsset _asset;
        private GameObject _object;
        private GraphTestContext _context;
        private IFyniteMachine _machine;

        [SetUp]
        public void SetUp()
        {
            var path = TestGraphFactory.NewPath("runtime");
            TestGraphFactory.CreateReferenceGraph(path);

            _asset = AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path);
            Assert.IsNotNull(_asset, "the graph must import before it can be run");
            Assert.IsNull(_asset.CompilationError, _asset.CompilationError);

            _object = new GameObject("runtime-test");
            _context = _object.AddComponent<GraphTestContext>();
            _machine = _asset.CreateMachine(_context);
        }

        [TearDown]
        public void TearDown()
        {
            _machine?.Dispose();

            if (_object != null)
            {
                Object.DestroyImmediate(_object);
            }

            TestGraphFactory.CleanUp();
        }

        private SignalHandle Signal(string name)
        {
            var guid = _asset.Signals.First(s => s.name == name).guid;
            Assert.IsTrue(_asset.TryResolveSignal(guid, out var handle), "signal '" + name + "' resolves");
            return handle;
        }

        private string StateName(StateHandle state) => _machine.Definition.GetStateName(state);

        [Test]
        public void StartEntersRootThenTheInitialChild()
        {
            _machine.Start();

            Assert.AreEqual(2, _machine.ActiveDepth);
            Assert.AreEqual("Root", StateName(_machine.GetActiveState(0)));
            Assert.AreEqual("Idle", StateName(_machine.ActiveLeaf));
            Assert.AreEqual("idleEnter#1", _context.TraceText, "the configured enter block ran");
        }

        [Test]
        public void TickAndFixedTickRunTheBlocksOfTheActivePath()
        {
            _machine.Start();

            _machine.Tick(0.1f);
            _machine.Tick(0.1f);
            _machine.FixedTick(0.02f);

            Assert.AreEqual(2, _context.Ticks);
            Assert.AreEqual(1, _context.FixedTicks);
        }

        [Test]
        public void ASignalTransitionsAndRunsExitThenEffect()
        {
            _machine.Start();
            _context.Trace.Clear();

            _machine.Raise(Signal("Move"));
            _machine.Tick(0f);

            Assert.AreEqual("Moving", StateName(_machine.ActiveLeaf));
            Assert.AreEqual("idleExit,moveEffect", _context.TraceText, "effects run between the exits and the entries");
        }

        [Test]
        public void TheTransitionKeepsTheCommonAncestorActive()
        {
            _machine.Start();

            var rootBefore = _machine.GetActiveState(0);
            _machine.Raise(Signal("Move"));
            _machine.Tick(0f);

            Assert.AreEqual(rootBefore, _machine.GetActiveState(0), "Root is the LCA and is never re-entered");
            Assert.AreEqual(2, _machine.ActiveDepth);
        }

        [Test]
        public void TransitioningBackWorks()
        {
            _machine.Start();
            _machine.Raise(Signal("Move"));
            _machine.Tick(0f);

            _machine.Raise(Signal("Stop"));
            _machine.Tick(0f);

            Assert.AreEqual("Idle", StateName(_machine.ActiveLeaf));
        }

        [Test]
        public void AFalseGuardLeavesTheActivePathAlone()
        {
            _context.Allow = false;
            _machine.Start();

            _machine.Raise(Signal("Move"));
            _machine.Tick(0f);

            Assert.AreEqual("Idle", StateName(_machine.ActiveLeaf), "the guard rejected the only candidate");
        }

        [Test]
        public void AReactionWithoutATargetRunsItsEffectAndChangesNothing()
        {
            _machine.Start();
            _machine.Raise(Signal("Move"));
            _machine.Tick(0f);

            var leafBefore = _machine.ActiveLeaf;
            int depthBefore = _machine.ActiveDepth;

            _machine.Raise(Signal("Notify"), "carried");
            _machine.Tick(0f);

            Assert.AreEqual(leafBefore, _machine.ActiveLeaf, "no transition happened");
            Assert.AreEqual(depthBefore, _machine.ActiveDepth);
            Assert.AreEqual("carried", _context.LastPayload, "the payload reached the effect");
        }

        [Test]
        public void APersistentSignalReferenceResolvesOnceAndThenUsesTheHandle()
        {
            var guid = _asset.Signals.First(s => s.name == "Move").guid;
            var reference = new FyniteSignalReference(_asset, guid);

            Assert.IsTrue(reference.TryResolve(out var first, out _));
            Assert.IsTrue(reference.TryResolve(out var second, out _));
            Assert.AreEqual(first, second, "resolution is cached");
            Assert.AreEqual("Move", reference.SignalName);

            _machine.Start();
            reference.Raise(_machine);
            _machine.Tick(0f);

            Assert.AreEqual("Moving", StateName(_machine.ActiveLeaf));
        }

        [Test]
        public void ASignalReferenceToAMissingSignalReportsInsteadOfResolving()
        {
            var reference = new FyniteSignalReference(_asset, "a-signal-that-was-deleted");

            Assert.IsFalse(reference.TryResolve(out _, out string error));
            StringAssert.Contains("deleted", error);
        }

        [Test]
        public void ASignalReferenceFromAnotherGraphIsRefused()
        {
            var otherPath = TestGraphFactory.NewPath("other");
            TestGraphFactory.CreateReferenceGraph(otherPath);
            var other = AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(otherPath);

            var reference = new FyniteSignalReference(other, other.Signals.First(s => s.name == "Move").guid);

            _machine.Start();

            // The handle is valid, but for a different definition — raising it here must be refused
            // rather than dispatched against whatever sits at the same index.
            Assert.Throws<System.InvalidOperationException>(() => reference.Raise(_machine));
        }

        [Test]
        public void AnIncompatiblePayloadIsRefusedWithBothTypesNamed()
        {
            _machine.Start();
            _machine.Raise(Signal("Move"));
            _machine.Tick(0f);

            var exception = Assert.Throws<System.ArgumentException>(
                () => _machine.Raise(Signal("Notify"), 42));

            StringAssert.Contains("String", exception.Message);
            StringAssert.Contains("Int32", exception.Message);
        }

        [Test]
        public void TwoMachinesShareTheDefinitionButNotTheirState()
        {
            var otherObject = new GameObject("second");
            var otherContext = otherObject.AddComponent<GraphTestContext>();

            try
            {
                var other = _asset.CreateMachine(otherContext);

                Assert.AreSame(_machine.Definition, other.Definition, "the definition is shared");

                _machine.Start();
                other.Start();

                _machine.Raise(Signal("Move"));
                _machine.Tick(0f);

                Assert.AreEqual("Moving", StateName(_machine.ActiveLeaf));
                Assert.AreEqual("Idle", StateName(other.ActiveLeaf), "the second machine is untouched");

                other.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
            }
        }

        [Test]
        public void MutableBlocksAreNotSharedBetweenMachines()
        {
            var otherObject = new GameObject("second");
            var otherContext = otherObject.AddComponent<GraphTestContext>();

            try
            {
                var other = _asset.CreateMachine(otherContext);

                _machine.Start();
                _machine.Stop();
                _machine.Start();
                other.Start();

                // The enter block counts its own runs, so a shared instance would show "#3" here.
                Assert.AreEqual("idleEnter#1,idleExit,idleEnter#2", _context.TraceText);
                Assert.AreEqual("idleEnter#1", otherContext.TraceText);

                other.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(otherObject);
            }
        }

        [Test]
        public void StopAndDisposeBehave()
        {
            _machine.Start();
            Assert.IsTrue(_machine.IsRunning);

            _machine.Stop();
            Assert.IsFalse(_machine.IsRunning);
            Assert.AreEqual(FyniteLifecycle.Stopped, _machine.Lifecycle);

            _machine.Dispose();
            Assert.AreEqual(FyniteLifecycle.Disposed, _machine.Lifecycle);

            _machine = null;
        }

        [Test]
        public void ResolvingASignalDoesNotDependOnTheEditorHavingTheGraphOpen()
        {
            // Everything below goes through the compiled asset only; no Graph Toolkit object is touched.
            var guid = _asset.Signals.First(s => s.name == "Notify").guid;

            Assert.IsTrue(_asset.TryResolveSignal(guid, out var handle));
            Assert.AreEqual(typeof(string), _asset.Definition.GetPayloadType(handle));
            Assert.AreEqual("Notify", _asset.GetSignalName(guid));
        }
    }
}
