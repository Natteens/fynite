using System.IO;
using System.Linq;
using Fynite.GraphEditor;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.Tests
{
    public sealed class GraphTask2AuthoringTests
    {
        [TearDown]
        public void TearDown() => TestGraphFactory.CleanUp();

        [Test]
        public void ExistingNameIsTheTitleAndReloadPlaceholderCannotOverwriteIt()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-title"));
            var state = FyniteStateOperations.CreateChild(graph, graph.FindRoot(), Vector2.zero);
            state.SetDisplayName("Locomotion");
            var guid = state.FyniteGuid;

            state.Title = FyniteStateNode.DefaultName;
            state.SyncDisplayName();

            Assert.AreEqual("Locomotion", state.StateName);
            Assert.AreEqual("Locomotion", state.Title);
            Assert.AreEqual(guid, state.FyniteGuid);
            Assert.AreNotEqual(state.StateName, state.Subtitle);
        }

        [Test]
        public void RenameRejectsEmptyAndPreservesIdentityHierarchyAndReaction()
        {
            var graph = TestGraphFactory.CreateReferenceGraph(TestGraphFactory.NewPath("task2-rename"));
            var state = graph.GetNodes().OfType<FyniteStateNode>().Single(s => s.StateName == "Idle");
            var guid = state.FyniteGuid;
            var parent = state.ResolveParent();
            var reaction = state.ResolveReactions().Single();
            var target = reaction.ResolveTarget();

            Assert.IsFalse(FyniteStateOperations.Rename(graph, state, "  "));
            Assert.AreEqual("Idle", state.StateName);
            Assert.IsTrue(FyniteStateOperations.Rename(graph, state, "  Waiting  "));
            Assert.AreEqual("Waiting", state.StateName);
            Assert.AreEqual("Waiting", state.Title);
            Assert.AreEqual(guid, state.FyniteGuid);
            Assert.AreSame(parent, state.ResolveParent());
            Assert.AreSame(target, reaction.ResolveTarget());
        }

        [Test]
        public void RenamePersistsAcrossSaveReloadAndReimport()
        {
            var path = TestGraphFactory.NewPath("task2-rename-persist");
            var graph = FyniteGraphCreation.Create(path);
            var state = FyniteStateOperations.CreateChild(graph, graph.FindRoot(), Vector2.zero);
            var guid = state.FyniteGuid;
            FyniteStateOperations.Rename(graph, state, "Combat");
            FyniteGraphCreation.SaveAndReimport(graph);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var loaded = GraphDatabase.LoadGraph<FyniteGraph>(path);
            var reloaded = loaded.GetNodes().OfType<FyniteStateNode>().Single();
            Assert.AreEqual(guid, reloaded.FyniteGuid);
            Assert.AreEqual("Combat", reloaded.StateName);
            Assert.AreEqual("Combat", reloaded.Title);
        }

        [Test]
        public void CreationUsesPredictableSiblingNamesAndFirstChildInitialPerParent()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-defaults"));
            var root = graph.FindRoot();
            var first = FyniteStateOperations.CreateChild(graph, root, Vector2.zero);
            var second = FyniteStateOperations.CreateChild(graph, root, Vector2.right);
            var nested = FyniteStateOperations.CreateChild(graph, first, Vector2.down);

            Assert.AreEqual("State", first.StateName);
            Assert.AreEqual("State 2", second.StateName);
            Assert.AreEqual("State", nested.StateName, "names are allocated within the direct Parent");
            Assert.IsTrue(first.IsInitial);
            Assert.IsFalse(second.IsInitial);
            Assert.IsTrue(nested.IsInitial);
            Assert.IsFalse(root.GetNodeOptionByName(FyniteStateNode.InitialOption) != null);
        }

        [Test]
        public void SetAsInitialIsAtomicWithinDirectSiblingsOnly()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-initial"));
            var root = graph.FindRoot();
            var a = FyniteStateOperations.CreateChild(graph, root, Vector2.zero);
            var b = FyniteStateOperations.CreateChild(graph, root, Vector2.right);
            var nestedA = FyniteStateOperations.CreateChild(graph, a, Vector2.down);
            var nestedB = FyniteStateOperations.CreateChild(graph, a, Vector2.one);

            Assert.IsTrue(FyniteStateOperations.SetAsInitial(graph, b));
            Assert.IsFalse(a.IsInitial);
            Assert.IsTrue(b.IsInitial);
            Assert.IsTrue(nestedA.IsInitial, "another Parent's group is untouched");
            Assert.IsFalse(nestedB.IsInitial);
        }

        [Test]
        public void ExplicitInitialNormalizesLegacyMultipleButLoadingDoesNot()
        {
            var path = TestGraphFactory.NewPath("task2-multiple-initial");
            var graph = FyniteGraphCreation.Create(path);
            var root = graph.FindRoot();
            var a = TestGraphFactory.AddState(graph, root, "A", Vector2.zero, true);
            var b = TestGraphFactory.AddState(graph, root, "B", Vector2.right, true);
            FyniteGraphCreation.SaveAndReimport(graph);

            var loaded = GraphDatabase.LoadGraph<FyniteGraph>(path);
            Assert.AreEqual(2, loaded.FindRoot().ResolveChildren().Count(s => s.IsInitial));
            var selected = loaded.GetNodes().OfType<FyniteStateNode>().Single(s => s.StateName == "B");
            Assert.IsTrue(FyniteStateOperations.SetAsInitial(loaded, selected));
            Assert.AreEqual(1, loaded.FindRoot().ResolveChildren().Count(s => s.IsInitial));
            Assert.IsTrue(selected.IsInitial);
        }

        [Test]
        public void MoveRejectsSelfAndIndirectCyclesAndPreservesStateIdentityAndChildren()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-move"));
            var root = graph.FindRoot();
            var parent = FyniteStateOperations.CreateChild(graph, root, Vector2.zero);
            parent.SetDisplayName("Parent");
            var child = FyniteStateOperations.CreateChild(graph, parent, Vector2.right);
            child.SetDisplayName("Child");
            var grandchild = FyniteStateOperations.CreateChild(graph, child, Vector2.one);
            var guid = child.FyniteGuid;

            Assert.IsFalse(FyniteStateOperations.MoveToParent(graph, parent, parent, out var selfError));
            StringAssert.Contains("own Parent", selfError);
            Assert.IsFalse(FyniteStateOperations.MoveToParent(graph, parent, grandchild, out var cycleError));
            StringAssert.Contains("cycle", cycleError);
            Assert.IsTrue(FyniteStateOperations.MoveToParent(graph, child, root, out _));
            Assert.AreEqual(guid, child.FyniteGuid);
            Assert.AreSame(root, child.ResolveParent());
            Assert.AreSame(child, grandchild.ResolveParent());
            Assert.IsFalse(child.IsInitial, "the destination already had an Initial sibling");
        }

        [Test]
        public void MoveToEmptyParentMakesMovedStateInitial()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-move-empty"));
            var root = graph.FindRoot();
            var source = FyniteStateOperations.CreateChild(graph, root, Vector2.zero);
            var destination = FyniteStateOperations.CreateChild(graph, root, Vector2.right);
            var moved = FyniteStateOperations.CreateChild(graph, source, Vector2.down);

            Assert.IsTrue(FyniteStateOperations.MoveToParent(graph, moved, destination, out _));
            Assert.IsTrue(moved.IsInitial);
            Assert.AreSame(destination, moved.ResolveParent());
        }

        [Test]
        public void ScopeShowsOneLevelAndBreadcrumbNavigationDoesNotMutateGraph()
        {
            var path = TestGraphFactory.NewPath("task2-scope");
            var graph = FyniteGraphCreation.Create(path);
            var root = graph.FindRoot();
            var locomotion = FyniteStateOperations.CreateChild(graph, root, Vector2.zero);
            locomotion.SetDisplayName("Locomotion");
            var combat = FyniteStateOperations.CreateChild(graph, root, Vector2.right);
            combat.SetDisplayName("Combat");
            var idle = FyniteStateOperations.CreateChild(graph, locomotion, Vector2.down);
            idle.SetDisplayName("Idle");
            FyniteGraphCreation.SaveAndReimport(graph);
            var before = File.ReadAllText(Path.GetFullPath(path));

            var session = new FyniteStateScopeSession(graph);
            CollectionAssert.AreEquivalent(new[] { "Locomotion", "Combat" }, session.VisibleChildren.Select(s => s.StateName));
            session.Open(locomotion);
            Assert.AreEqual("Root / Locomotion", session.Breadcrumb);
            CollectionAssert.AreEqual(new[] { "Idle" }, session.VisibleChildren.Select(s => s.StateName));
            Assert.IsFalse(session.VisibleChildren.Contains(combat));
            Assert.IsFalse(session.VisibleChildren.SelectMany(s => s.ResolveChildren()).Any());
            Assert.IsTrue(session.Back());
            Assert.IsInstanceOf<FyniteRootStateNode>(session.Owner);
            Assert.AreEqual(before, File.ReadAllText(Path.GetFullPath(path)));
        }

        [Test]
        public void ScopeStateIsPerSessionAndInvalidOwnerFallsBackToRoot()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-window-scope"));
            var child = FyniteStateOperations.CreateChild(graph, graph.FindRoot(), Vector2.zero);
            var firstWindow = new FyniteStateScopeSession(graph);
            var secondWindow = new FyniteStateScopeSession(graph);
            firstWindow.Open(child);

            Assert.AreSame(child, firstWindow.Owner);
            Assert.IsInstanceOf<FyniteRootStateNode>(secondWindow.Owner);
            graph.RemoveNode(child);
            firstWindow.Repair();
            Assert.IsInstanceOf<FyniteRootStateNode>(firstWindow.Owner);
        }

        [Test]
        public void OrphansRemainUnchangedAndAvailableForRecovery()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-orphan"));
            var orphan = new FyniteStateNode();
            graph.AddNode(orphan);
            orphan.SetDisplayName("Orphan");
            FyniteGraphCreation.SaveAndReimport(graph);

            var loaded = GraphDatabase.LoadGraph<FyniteGraph>(GraphDatabase.GetGraphAssetPath(graph));
            var persisted = loaded.GetNodes().OfType<FyniteStateNode>().Single();
            Assert.AreEqual("Orphan", persisted.StateName);
            Assert.IsNull(persisted.ResolveParent());
            Assert.IsFalse(persisted.IsInitial);
        }

        [Test]
        public void CrossScopeReactionRemainsGuidBasedAndNavigationDoesNotChangeCompilation()
        {
            var path = TestGraphFactory.NewPath("task2-reaction-scope");
            var graph = TestGraphFactory.CreateReferenceGraph(path);
            var idle = graph.GetNodes().OfType<FyniteStateNode>().Single(s => s.StateName == "Idle");
            var moving = graph.GetNodes().OfType<FyniteStateNode>().Single(s => s.StateName == "Moving");
            var reaction = idle.ResolveReactions().Single();
            var before = FyniteGraphProjection.ToDocument(graph);
            var sourceGuid = before.states.Single(s => s.name == "Idle").guid;
            var targetGuid = before.states.Single(s => s.name == "Moving").guid;

            var session = new FyniteStateScopeSession(graph);
            session.Open(idle);
            session.Open(moving);
            var after = FyniteGraphProjection.ToDocument(graph);

            Assert.AreSame(moving, reaction.ResolveTarget());
            Assert.AreEqual("Root / Moving", FyniteStateOperations.PathOf(reaction.ResolveTarget()));
            Assert.AreEqual(sourceGuid, after.states.Single(s => s.name == "Idle").guid);
            Assert.AreEqual(targetGuid, after.states.Single(s => s.name == "Moving").guid);
            Assert.AreEqual(before.reactions.Count, after.reactions.Count);
        }

        [Test]
        public void EqualNamesDoNotChangeGuidIdentity()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-equal-names"));
            var root = graph.FindRoot();
            var a = FyniteStateOperations.CreateChild(graph, root, Vector2.zero);
            var b = FyniteStateOperations.CreateChild(graph, root, Vector2.right);
            FyniteStateOperations.Rename(graph, a, "Same");
            FyniteStateOperations.Rename(graph, b, "Same");

            Assert.AreEqual("Same", a.StateName);
            Assert.AreEqual("Same", b.StateName);
            Assert.AreNotEqual(a.FyniteGuid, b.FyniteGuid);
        }
    }
}
