using System.Collections.Generic;
using System.Linq;
using Fynite.GraphEditor;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fynite.Tests
{
    /// <summary>
    /// The state authoring commands, driven exactly as the <c>.fyn</c> inspector drives them.
    /// </summary>
    /// <remarks>
    /// These tests call <see cref="FyniteStateAuthoringSession"/>, which is the object behind the
    /// inspector's popups and buttons — not the operations underneath it. A test that called
    /// <see cref="FyniteStateOperations"/> directly would pass while the surface a user actually touches
    /// was wired to nothing, so the entry point here is the same one the GUI has.
    /// </remarks>
    public sealed class StateAuthoringSessionTests
    {
        private FyniteStateAuthoringSession _session;

        [TearDown]
        public void TearDown()
        {
            // A test that failed between the two assignments would otherwise leave the suppression on
            // and hide a real error in whatever runs next.
            LogAssert.ignoreFailingMessages = false;
            _session = null;
            TestGraphFactory.CleanUp();
        }

        /// <summary>A compilable empty graph, opened through the session the inspector uses.</summary>
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

        private FyniteRootStateNode Root => _session.Graph.FindRoot();

        /// <summary>Adds a child under a node and returns it, the way the Add Child button does.</summary>
        private FyniteStateNode AddChild(IFyniteIdentifiedNode parent, string name)
        {
            Assert.IsTrue(_session.CreateChildOf(parent.FyniteGuid, out var error), error);
            var created = _session.SelectedState();
            Assert.IsNotNull(created, "CreateChild selects what it created");

            if (name != null)
            {
                Assert.IsTrue(_session.Rename(name, out var renameError), renameError);
            }

            return _session.SelectedState();
        }

        private FyniteStateNode State(string name) =>
            _session.States().Single(s => s.StateName == name);

        /// <summary>
        /// The invariant every command has to leave standing: no parent with two initial children, and
        /// no composite without one.
        /// </summary>
        private void AssertInitialInvariant()
        {
            var parents = new List<IFyniteIdentifiedNode> { Root };
            parents.AddRange(_session.States().Cast<IFyniteIdentifiedNode>());

            foreach (var parent in parents)
            {
                var children = FyniteStateOperations.ChildrenOf(parent);

                if (children.Count == 0)
                {
                    continue;
                }

                var initial = children.Where(c => c.IsInitial).ToList();
                Assert.AreEqual(
                    1,
                    initial.Count,
                    "'" + FyniteStateOperations.PathOf(parent) + "' has " + children.Count +
                    " children and " + initial.Count + " marked initial");
            }
        }

        private FyniteGraphAsset Compiled()
        {
            var asset = _session.CompiledAsset();
            Assert.IsNotNull(asset, "every command reimports the file");
            return asset;
        }

        // ── creating ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void FirstChildBecomesInitialAndTheSecondDoesNot()
        {
            NewSession("authoring-create");

            var first = AddChild(Root, "Idle");
            Assert.IsTrue(first.IsInitial, "a parent's first child is what it enters");

            var second = AddChild(Root, "Moving");
            Assert.IsFalse(second.IsInitial, "a later child never displaces the initial one");
            Assert.IsTrue(State("Idle").IsInitial, "the first child keeps the mark");

            AssertInitialInvariant();
        }

        [Test]
        public void AChildAddedDeeperDoesNotDisturbTheLevelAbove()
        {
            NewSession("authoring-nested");

            var idle = AddChild(Root, "Idle");
            AddChild(Root, "Moving");
            var nested = AddChild(idle, "Waiting");

            Assert.IsTrue(nested.IsInitial, "the first child of Idle is Idle's initial child");
            Assert.IsTrue(State("Idle").IsInitial, "Idle is still the root's initial child");
            AssertInitialInvariant();
        }

        // ── the initial mark ────────────────────────────────────────────────────────────────────

        [Test]
        public void SetAsInitialMovesTheMarkAndClearsOnlyDirectSiblings()
        {
            NewSession("authoring-initial");

            var idle = AddChild(Root, "Idle");
            var moving = AddChild(Root, "Moving");
            var nested = AddChild(idle, "Waiting");

            Assert.IsTrue(_session.Select(moving.FyniteGuid));
            Assert.IsTrue(_session.SetAsInitial(out var error), error);

            Assert.IsTrue(State("Moving").IsInitial);
            Assert.IsFalse(State("Idle").IsInitial, "the sibling loses the mark");
            Assert.IsTrue(
                State("Waiting").IsInitial,
                "a state deeper in the tree holds the mark for its own parent and must keep it");

            AssertInitialInvariant();
        }

        [Test]
        public void TheRootIsNeverOfferedAsSomethingToRenameOrMarkInitial()
        {
            NewSession("authoring-root");

            AddChild(Root, "Idle");

            CollectionAssert.DoesNotContain(
                _session.States().Select(s => s.FyniteGuid).ToList(),
                Root.FyniteGuid,
                "the root is not a state, so it never appears in the state picker");

            Assert.IsFalse(_session.Select(Root.FyniteGuid), "selecting the root by guid selects nothing");
            Assert.IsNull(_session.SelectedState());
        }

        // ── moving ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void MovingTheInitialOutElectsASuccessorAmongTheRemainingSiblings()
        {
            NewSession("authoring-move-initial");

            var group = AddChild(Root, "Grounded");
            var idle = AddChild(group, "Idle");
            var moving = AddChild(group, "Moving");
            var airborne = AddChild(Root, "Airborne");

            Assert.IsTrue(idle.IsInitial, "Idle starts as Grounded's initial child");

            Assert.IsTrue(_session.Select(idle.FyniteGuid));
            Assert.IsTrue(_session.MoveTo(airborne.FyniteGuid, out var error), error);

            Assert.IsTrue(
                State("Moving").IsInitial,
                "Grounded still has a child, so it must still have an initial one");
            Assert.AreEqual(
                "Root / Airborne / Idle",
                FyniteStateOperations.PathOf(State("Idle")),
                "the moved state landed where it was sent");

            AssertInitialInvariant();
            Assert.IsTrue(Compiled().IsExecutable, "the graph still compiles after the move");
        }

        [Test]
        public void TheElectedSuccessorIsTheSameOneEveryTime()
        {
            NewSession("authoring-move-deterministic");

            var group = AddChild(Root, "Grounded");
            var idle = AddChild(group, "Idle");
            AddChild(group, "Walking");
            AddChild(group, "Running");
            var elsewhere = AddChild(Root, "Airborne");

            var remaining = FyniteStateOperations.ChildrenOf(group).Where(child => child != idle).ToList();
            FyniteStateNode expected = null;

            foreach (var node in _session.Graph.GetNodes())
            {
                if (node is FyniteStateNode candidate && remaining.Contains(candidate))
                {
                    expected = candidate;
                    break;
                }
            }

            Assert.IsNotNull(expected, "two siblings remain behind");

            Assert.IsTrue(_session.Select(idle.FyniteGuid));
            Assert.IsTrue(_session.MoveTo(elsewhere.FyniteGuid, out var error), error);

            Assert.IsTrue(
                expected.IsInitial,
                "the successor is the first remaining child in graph order, not an arbitrary one");
            AssertInitialInvariant();
        }

        [Test]
        public void MovingTheLastChildLeavesTheOldParentALeaf()
        {
            NewSession("authoring-move-last");

            var group = AddChild(Root, "Grounded");
            var only = AddChild(group, "Idle");
            var elsewhere = AddChild(Root, "Airborne");

            Assert.IsTrue(_session.Select(only.FyniteGuid));
            Assert.IsTrue(_session.MoveTo(elsewhere.FyniteGuid, out var error), error);

            Assert.AreEqual(0, FyniteStateOperations.ChildrenOf(State("Grounded")).Count);
            AssertInitialInvariant();
            Assert.IsTrue(Compiled().IsExecutable, "a leaf needs no initial child");
        }

        [Test]
        public void MovingIntoAnEmptyParentMakesTheMovedStateInitial()
        {
            NewSession("authoring-move-empty");

            var idle = AddChild(Root, "Idle");
            AddChild(Root, "Moving");
            var empty = AddChild(Root, "Airborne");

            Assert.IsTrue(_session.Select(idle.FyniteGuid));
            Assert.IsTrue(_session.MoveTo(empty.FyniteGuid, out var error), error);

            Assert.IsTrue(State("Idle").IsInitial, "an only child is what its parent enters");
            AssertInitialInvariant();
        }

        [Test]
        public void MovingIntoAParentThatAlreadyHasAnInitialDoesNotCreateASecond()
        {
            NewSession("authoring-move-occupied");

            var group = AddChild(Root, "Grounded");
            var occupant = AddChild(group, "Idle");
            var mover = AddChild(Root, "Airborne");

            Assert.IsTrue(_session.Select(mover.FyniteGuid));
            Assert.IsTrue(_session.MoveTo(group.FyniteGuid, out var error), error);

            Assert.IsTrue(State("Idle").IsInitial, "the occupant keeps the mark");
            Assert.IsFalse(State("Airborne").IsInitial, "the arrival does not take it");
            Assert.AreSame(occupant, State("Idle"));
            AssertInitialInvariant();
        }

        [Test]
        public void SelfParentIsRejected()
        {
            NewSession("authoring-self-parent");

            var idle = AddChild(Root, "Idle");

            Assert.IsTrue(_session.Select(idle.FyniteGuid));
            Assert.IsFalse(_session.MoveTo(idle.FyniteGuid, out var error));
            StringAssert.Contains("own Parent", error);

            CollectionAssert.DoesNotContain(
                _session.ParentChoices().Select(p => p.FyniteGuid).ToList(),
                idle.FyniteGuid,
                "a state is not offered as its own parent in the first place");

            AssertInitialInvariant();
        }

        [Test]
        public void AnIndirectCycleIsRejected()
        {
            NewSession("authoring-cycle");

            var grandparent = AddChild(Root, "Grounded");
            var parent = AddChild(grandparent, "Idle");
            var grandchild = AddChild(parent, "Waiting");

            Assert.IsTrue(_session.Select(grandparent.FyniteGuid));
            Assert.IsFalse(_session.MoveTo(grandchild.FyniteGuid, out var error));
            StringAssert.Contains("cycle", error);

            var offered = _session.ParentChoices().Select(p => p.FyniteGuid).ToList();
            CollectionAssert.DoesNotContain(offered, parent.FyniteGuid, "a descendant is never offered");
            CollectionAssert.DoesNotContain(offered, grandchild.FyniteGuid);
            CollectionAssert.Contains(offered, Root.FyniteGuid, "the root is always a legal parent");

            Assert.AreEqual("Root / Grounded", FyniteStateOperations.PathOf(State("Grounded")));
            AssertInitialInvariant();
        }

        [Test]
        public void MoveRefusesToGuessAParent()
        {
            NewSession("authoring-move-nochoice");

            var idle = AddChild(Root, "Idle");
            Assert.IsTrue(_session.Select(idle.FyniteGuid));

            Assert.IsFalse(_session.MoveTo(null, out var error), "no parent chosen, no move");
            StringAssert.Contains("Choose the parent", error);
            Assert.AreEqual("Root / Idle", FyniteStateOperations.PathOf(State("Idle")));
        }

        // ── duplicating and renaming ────────────────────────────────────────────────────────────

        [Test]
        public void DuplicateGetsItsOwnIdentityAndIsNeverInitial()
        {
            NewSession("authoring-duplicate");

            var idle = AddChild(Root, "Idle");
            var sourceGuid = idle.FyniteGuid;
            Assert.IsTrue(idle.IsInitial);

            Assert.IsTrue(_session.Select(sourceGuid));
            Assert.IsTrue(_session.Duplicate(out var error), error);

            var copy = _session.SelectedState();
            Assert.IsNotNull(copy);
            Assert.AreNotEqual(sourceGuid, copy.FyniteGuid, "the copy is a different state, not the same one twice");
            Assert.IsFalse(copy.IsInitial, "duplicating an initial state never duplicates the mark");
            Assert.IsTrue(FyniteStateOperations.FindState(_session.Graph, sourceGuid).IsInitial);

            AssertInitialInvariant();
        }

        [Test]
        public void RenameKeepsTheIdentityAndEveryReactionAttachedToIt()
        {
            var path = TestGraphFactory.NewPath("authoring-rename");
            var graph = TestGraphFactory.CreateReferenceGraph(path);
            _session = FyniteStateAuthoringSession.For(path, graph);

            var idle = State("Idle");
            var guid = idle.FyniteGuid;
            var reactionsBefore = idle.ResolveReactions().Select(r => r.FyniteGuid).ToList();
            var incomingBefore = idle.ResolveIncoming().Select(r => r.FyniteGuid).ToList();
            var parentBefore = idle.ResolveParent().FyniteGuid;
            var positionBefore = idle.Position;

            Assert.IsTrue(_session.Select(guid));
            Assert.IsTrue(_session.Rename("Waiting", out var error), error);

            var renamed = FyniteStateOperations.FindState(_session.Graph, guid);
            Assert.IsNotNull(renamed, "the identity survives the rename");
            Assert.AreEqual("Waiting", renamed.StateName);
            Assert.AreEqual("Waiting", renamed.Title);
            Assert.AreEqual(parentBefore, renamed.ResolveParent().FyniteGuid);
            Assert.AreEqual(positionBefore, renamed.Position);
            CollectionAssert.AreEqual(reactionsBefore, renamed.ResolveReactions().Select(r => r.FyniteGuid).ToList());
            CollectionAssert.AreEqual(incomingBefore, renamed.ResolveIncoming().Select(r => r.FyniteGuid).ToList());
        }

        [Test]
        public void RenameChangesTheLabelAndNothingElseInTheCompiledMachine()
        {
            var path = TestGraphFactory.NewPath("authoring-rename-semantics");
            var graph = TestGraphFactory.CreateReferenceGraph(path);
            _session = FyniteStateAuthoringSession.For(path, graph);

            var before = Describe(Compiled());

            Assert.IsTrue(_session.Select(State("Idle").FyniteGuid));
            Assert.IsTrue(_session.Rename("Waiting", out var error), error);

            var after = Describe(Compiled());

            Assert.AreEqual(
                before.Replace("Idle", "Waiting"),
                after,
                "a rename is a label change: the tree, the initial children and the signals are untouched");
        }

        /// <summary>The compiled machine's whole shape as text, so a diff is readable when it changes.</summary>
        private static string Describe(FyniteGraphAsset asset)
        {
            Assert.IsNull(asset.CompilationError, asset.CompilationError);
            var definition = asset.Definition;
            var text = new System.Text.StringBuilder();

            void Walk(StateHandle state, int depth)
            {
                text.Append(new string(' ', depth * 2)).Append(definition.GetStateName(state));

                var initial = definition.GetInitialChild(state);
                if (initial.IsValid)
                {
                    text.Append(" -> ").Append(definition.GetStateName(initial));
                }

                text.AppendLine();

                for (int i = 0; i < definition.GetChildCount(state); i++)
                {
                    Walk(definition.GetChild(state, i), depth + 1);
                }
            }

            Walk(definition.Root, 0);

            for (int i = 0; i < definition.SignalCount; i++)
            {
                text.Append("signal ").AppendLine(definition.GetSignalName(definition.GetSignal(i)));
            }

            return text.ToString();
        }

        // ── orphans ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void AnUnparentedStateIsFoundAndCanBeGivenAParent()
        {
            NewSession("authoring-orphan");

            var idle = AddChild(Root, "Idle");

            // A state wired to nothing is what dragging a State node onto the canvas produces, so build
            // the orphan the same way rather than through a command that would parent it.
            var orphan = new FyniteStateNode();
            _session.Graph.AddNode(orphan);
            orphan.SetDisplayName("Stray");

            // Importing this state of the file is supposed to report FYN0404, FYN0408 and FYN0411. That
            // is the behaviour being set up, not a failure, so the console errors are expected here.
            LogAssert.ignoreFailingMessages = true;
            _session.Persist();
            LogAssert.ignoreFailingMessages = false;

            var found = _session.Orphans();
            Assert.AreEqual(1, found.Count, "the stray state is the only one without a parent");
            Assert.AreEqual("Stray", found[0].StateName);

            Assert.IsFalse(
                _session.AdoptOrphan(found[0].FyniteGuid, null, out var refused),
                "no parent chosen, no adoption");
            StringAssert.Contains("Choose the parent", refused);

            Assert.IsTrue(_session.AdoptOrphan(found[0].FyniteGuid, idle.FyniteGuid, out var error), error);

            Assert.AreEqual(0, _session.Orphans().Count, "the state now belongs somewhere");
            Assert.AreEqual("Root / Idle / Stray", FyniteStateOperations.PathOf(State("Stray")));
            Assert.IsTrue(State("Stray").IsInitial, "it is the only child of Idle, so Idle enters it");

            AssertInitialInvariant();
            Assert.IsTrue(Compiled().IsExecutable, "recovering the orphan made the graph compile");
        }

        // ── persistence ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void EveryCommandSurvivesSaveAndReload()
        {
            var session = NewSession("authoring-persist");
            var path = session.AssetPath;

            var grounded = AddChild(Root, "Grounded");
            var idle = AddChild(grounded, "Idle");
            AddChild(grounded, "Moving");
            var airborne = AddChild(Root, "Airborne");

            Assert.IsTrue(_session.Select(State("Moving").FyniteGuid));
            Assert.IsTrue(_session.SetAsInitial(out var initialError), initialError);

            Assert.IsTrue(_session.Select(idle.FyniteGuid));
            Assert.IsTrue(_session.MoveTo(airborne.FyniteGuid, out var moveError), moveError);
            Assert.IsTrue(_session.Rename("Waiting", out var renameError), renameError);

            var reloaded = GraphDatabase.LoadGraph<FyniteGraph>(path);
            Assert.IsNotNull(reloaded, "the file reloads");

            var reloadedSession = FyniteStateAuthoringSession.For(path, reloaded);
            var names = reloadedSession.States().Select(s => FyniteStateOperations.PathOf(s)).OrderBy(s => s).ToList();

            CollectionAssert.AreEqual(
                new[] { "Root / Airborne", "Root / Airborne / Waiting", "Root / Grounded", "Root / Grounded / Moving" },
                names,
                "the hierarchy on disk is the one the commands built");

            var reloadedWaiting = reloadedSession.States().Single(s => s.StateName == "Waiting");
            Assert.AreEqual(idle.FyniteGuid, reloadedWaiting.FyniteGuid, "identities round-trip");
            Assert.IsTrue(reloadedWaiting.IsInitial, "so does the initial mark");
        }

        [Test]
        public void TheImporterCompilesWhatTheCommandsWrote()
        {
            NewSession("authoring-import");

            var grounded = AddChild(Root, "Grounded");
            AddChild(grounded, "Idle");
            AddChild(grounded, "Moving");

            var asset = Compiled();

            Assert.IsNull(asset.CompilationError, asset.CompilationError);
            Assert.IsTrue(asset.IsExecutable);
            Assert.AreEqual(typeof(GraphTestContext), asset.ContextType);

            var definition = asset.Definition;
            Assert.AreEqual(4, definition.StateCount, "Root, Grounded, Idle and Moving");

            var root = definition.Root;
            Assert.AreEqual("Root", definition.GetStateName(root));
            Assert.AreEqual("Grounded", definition.GetStateName(definition.GetInitialChild(root)));

            var compiledGrounded = definition.GetInitialChild(root);
            Assert.AreEqual("Idle", definition.GetStateName(definition.GetInitialChild(compiledGrounded)));
        }

        [Test]
        public void ACompositeWithoutAnInitialChildIsReportedRatherThanGuessed()
        {
            NewSession("authoring-uncompilable");

            var grounded = AddChild(Root, "Grounded");

            // Wire a second child by hand, the way the canvas does, and strip the mark the controlled
            // path put there. This is the state the compiler must refuse.
            var stray = new FyniteStateNode();
            _session.Graph.AddNode(stray);
            stray.SetDisplayName("Stray");
            _session.Graph.Connect(
                grounded.GetOutputPortByName(FyniteStateNode.ChildrenPort),
                stray.GetInputPortByName(FyniteStateNode.ParentPort));

            foreach (var child in FyniteStateOperations.ChildrenOf(grounded))
            {
                child.SetInitial(false);
            }

            _session.Graph.TouchForFieldChange();

            // FYN0409 on the console is the point of this test, not an accident of it.
            LogAssert.ignoreFailingMessages = true;
            _session.Persist();
            LogAssert.ignoreFailingMessages = false;

            var asset = Compiled();
            Assert.IsFalse(asset.IsExecutable, "a composite with no initial child cannot run");
            StringAssert.Contains("initial", asset.CompilationError);

            // And the command surface is what puts it right, without inventing a choice.
            Assert.IsTrue(_session.Select(stray.FyniteGuid));
            Assert.IsTrue(_session.SetAsInitial(out var error), error);

            Assert.IsTrue(Compiled().IsExecutable, "choosing one explicitly is what fixes it");
            AssertInitialInvariant();
        }
    }
}
