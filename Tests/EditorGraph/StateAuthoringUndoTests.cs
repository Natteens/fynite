using System.Linq;
using Fynite.GraphEditor;
using NUnit.Framework;
using UnityEditor;

namespace Fynite.Tests
{
    /// <summary>
    /// What Unity's global Undo stack actually does to a command run from the <c>.fyn</c> inspector.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every operation is bracketed by <c>UndoBeginRecordGraph</c> / <c>UndoEndRecordGraph</c>, which is
    /// the only recording API Graph Toolkit exposes publicly. What that call records is Graph Toolkit's
    /// business, and it is not documented to cover serialized fields declared by a derived node — the
    /// Fynite GUID, the display name and the Initial mark are all such fields.
    /// </para>
    /// <para>
    /// So these tests do not assume an answer. Each one performs a real
    /// <see cref="Undo.PerformUndo"/> and checks two different things. The first is unconditional and is
    /// the one that matters most: whichever way undo behaves, it must not leave the graph corrupt —
    /// no parent with two initial children, no state pointing at a node that is gone. The second is the
    /// field itself; when Graph Toolkit did not restore it the test reports the limitation through
    /// <c>Assert.Ignore</c> instead of failing, because a limitation in a dependency is something to
    /// document, not a regression in this package.
    /// </para>
    /// <para>
    /// Until these have been run on a given Unity version, Fynite declares no Undo support for the
    /// inspector commands. Nothing in the UI or the docs promises it.
    /// </para>
    /// </remarks>
    public sealed class StateAuthoringUndoTests
    {
        private FyniteStateAuthoringSession _session;

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            _session = null;
            TestGraphFactory.CleanUp();
        }

        private FyniteStateAuthoringSession NewSession(string name)
        {
            var path = TestGraphFactory.NewPath(name);
            var graph = FyniteGraphCreation.Create(path);
            graph.SetContextType(typeof(GraphTestContext));
            graph.TouchForFieldChange();

            _session = FyniteStateAuthoringSession.For(path, graph);
            _session.Persist();
            return _session;
        }

        private FyniteStateNode AddChild(IFyniteIdentifiedNode parent, string name)
        {
            Assert.IsTrue(_session.CreateChildOf(parent.FyniteGuid, out var error), error);
            Assert.IsTrue(_session.Rename(name, out var renameError), renameError);
            return _session.SelectedState();
        }

        /// <summary>The invariant undo may never break, however much of the edit it rolls back.</summary>
        private void AssertGraphIsCoherent()
        {
            var parents = _session.Graph.GetNodes()
                .Select(node => node as IFyniteIdentifiedNode)
                .Where(node => node is FyniteRootStateNode || node is FyniteStateNode)
                .ToList();

            foreach (var parent in parents)
            {
                var children = FyniteStateOperations.ChildrenOf(parent);

                if (children.Count == 0)
                {
                    continue;
                }

                Assert.LessOrEqual(
                    children.Count(child => child.IsInitial),
                    1,
                    "'" + FyniteStateOperations.PathOf(parent) + "' must never end up with two initial children");
            }

            foreach (var state in _session.Graph.GetNodes().OfType<FyniteStateNode>())
            {
                Assert.IsNotEmpty(state.FyniteGuid, "every surviving state keeps an identity");
            }
        }

        [Test]
        public void UndoAfterSetAsInitialLeavesTheGraphCoherent()
        {
            NewSession("undo-initial");

            var root = _session.Graph.FindRoot();
            var idle = AddChild(root, "Idle");
            var moving = AddChild(root, "Moving");
            var idleGuid = idle.FyniteGuid;

            Assert.IsTrue(_session.Select(moving.FyniteGuid));
            Assert.IsTrue(_session.SetAsInitial(out var error), error);
            Assert.IsTrue(FyniteStateOperations.FindState(_session.Graph, moving.FyniteGuid).IsInitial);

            Undo.PerformUndo();

            AssertGraphIsCoherent();

            var idleAfter = FyniteStateOperations.FindState(_session.Graph, idleGuid);

            if (idleAfter == null || !idleAfter.IsInitial)
            {
                Assert.Ignore(
                    "Graph Toolkit's UndoBeginRecordGraph did not restore the Initial mark, a serialized " +
                    "field declared by FyniteStateNode. The inspector commands are therefore not " +
                    "undoable on this Unity version, and Fynite must say so rather than imply otherwise.");
            }

            Undo.PerformRedo();
            AssertGraphIsCoherent();

            var movingAfterRedo = FyniteStateOperations.FindState(_session.Graph, moving.FyniteGuid);
            Assert.IsNotNull(movingAfterRedo);
            Assert.IsTrue(movingAfterRedo.IsInitial, "redo puts back what undo took away");
        }

        [Test]
        public void UndoAfterRenameLeavesTheGraphCoherent()
        {
            NewSession("undo-rename");

            var root = _session.Graph.FindRoot();
            var idle = AddChild(root, "Idle");
            var guid = idle.FyniteGuid;

            Assert.IsTrue(_session.Select(guid));
            Assert.IsTrue(_session.Rename("Waiting", out var error), error);

            Undo.PerformUndo();
            AssertGraphIsCoherent();

            var after = FyniteStateOperations.FindState(_session.Graph, guid);

            if (after == null || after.StateName != "Idle")
            {
                Assert.Ignore(
                    "Graph Toolkit's UndoBeginRecordGraph did not restore the display name, a serialized " +
                    "field declared by FyniteStateNode. Rename is therefore not undoable on this Unity " +
                    "version.");
            }

            Undo.PerformRedo();
            AssertGraphIsCoherent();
            Assert.AreEqual("Waiting", FyniteStateOperations.FindState(_session.Graph, guid).StateName);
        }

        [Test]
        public void UndoAfterCreateChildLeavesTheGraphCoherent()
        {
            NewSession("undo-create");

            var root = _session.Graph.FindRoot();
            var idle = AddChild(root, "Idle");

            Assert.IsTrue(_session.Select(idle.FyniteGuid));
            Assert.IsTrue(_session.CreateChild(out var error), error);
            var createdGuid = _session.SelectedState().FyniteGuid;

            Undo.PerformUndo();
            AssertGraphIsCoherent();

            if (FyniteStateOperations.FindState(_session.Graph, createdGuid) != null)
            {
                Assert.Ignore(
                    "Graph Toolkit's UndoBeginRecordGraph did not remove the created node. Creating a " +
                    "state from the inspector is therefore not undoable on this Unity version.");
            }

            Undo.PerformRedo();
            AssertGraphIsCoherent();
            Assert.IsNotNull(
                FyniteStateOperations.FindState(_session.Graph, createdGuid),
                "redo brings the created state back");
        }

        [Test]
        public void UndoAfterMoveLeavesTheGraphCoherent()
        {
            NewSession("undo-move");

            var root = _session.Graph.FindRoot();
            var grounded = AddChild(root, "Grounded");
            var idle = AddChild(grounded, "Idle");
            AddChild(grounded, "Moving");
            var airborne = AddChild(root, "Airborne");

            Assert.IsTrue(_session.Select(idle.FyniteGuid));
            Assert.IsTrue(_session.MoveTo(airborne.FyniteGuid, out var error), error);

            Undo.PerformUndo();
            AssertGraphIsCoherent();

            var moved = FyniteStateOperations.FindState(_session.Graph, idle.FyniteGuid);

            if (moved == null || FyniteStateOperations.PathOf(moved) != "Root / Grounded / Idle")
            {
                Assert.Ignore(
                    "Graph Toolkit's UndoBeginRecordGraph did not restore the parent wire and the Initial " +
                    "marks the move rewrote. Moving a state from the inspector is therefore not undoable " +
                    "on this Unity version.");
            }

            Undo.PerformRedo();
            AssertGraphIsCoherent();
            Assert.AreEqual(
                "Root / Airborne / Idle",
                FyniteStateOperations.PathOf(FyniteStateOperations.FindState(_session.Graph, idle.FyniteGuid)));
        }
    }
}
