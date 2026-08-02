using System.Linq;
using Fynite.Authoring;
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
        public void RenamePersistsAndPreservesGuidHierarchyAndReaction()
        {
            var path = TestGraphFactory.NewPath("task2-rename");
            var graph = TestGraphFactory.CreateReferenceGraph(path);
            var state = graph.GetNodes().OfType<FyniteStateNode>().Single(s => s.StateName == "Idle");
            var guid = state.FyniteGuid;
            var parent = state.ResolveParent();
            var target = state.ResolveReactions().Single().ResolveTarget();

            Assert.IsFalse(FyniteStateOperations.Rename(graph, state, "  "));
            Assert.IsTrue(FyniteStateOperations.Rename(graph, state, " Waiting "));
            FyniteGraphCreation.SaveAndReimport(graph);

            var loaded = GraphDatabase.LoadGraph<FyniteGraph>(path);
            var renamed = loaded.GetNodes().OfType<FyniteStateNode>().Single(s => s.FyniteGuid == guid);
            Assert.AreEqual("Waiting", renamed.StateName);
            Assert.AreEqual("Waiting", renamed.Title);
            Assert.AreEqual(parent.FyniteGuid, renamed.ResolveParent().FyniteGuid);
            Assert.AreEqual(target.FyniteGuid, renamed.ResolveReactions().Single().ResolveTarget().FyniteGuid);
        }

        [Test]
        public void RootHasFixedNameAndNoRenameSurface()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-root-name"));
            var root = graph.FindRoot();

            Assert.AreEqual(FyniteRootStateNode.RootName, root.Title);
            Assert.IsFalse(root is FyniteStateNode);
        }

        [Test]
        public void ControlledCreationNamesPredictablyAndKeepsOneInitialPerParent()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-defaults"));
            var root = graph.FindRoot();
            var first = FyniteStateOperations.CreateChild(graph, root, Vector2.zero);
            var second = FyniteStateOperations.CreateChild(graph, root, Vector2.right);
            var nested = FyniteStateOperations.CreateChild(graph, first, Vector2.down);

            Assert.AreEqual("State", first.StateName);
            Assert.AreEqual("State 2", second.StateName);
            Assert.AreEqual("State", nested.StateName);
            Assert.IsTrue(first.IsInitial);
            Assert.IsFalse(second.IsInitial);
            Assert.IsTrue(nested.IsInitial);
            Assert.IsFalse(root.GetNodeOptionByName("isInitial") != null);
            Assert.IsNull(first.GetNodeOptionByName("isInitial"), "Initial is not an editable checkbox");
        }

        [Test]
        public void SetAsInitialAtomicallyChangesOnlyDirectSiblings()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-initial"));
            var root = graph.FindRoot();
            var a = FyniteStateOperations.CreateChild(graph, root, Vector2.zero);
            var b = FyniteStateOperations.CreateChild(graph, root, Vector2.right);
            var nested = FyniteStateOperations.CreateChild(graph, a, Vector2.down);

            Assert.IsTrue(FyniteStateOperations.SetAsInitial(graph, b));
            Assert.IsFalse(a.IsInitial);
            Assert.IsTrue(b.IsInitial);
            Assert.IsTrue(nested.IsInitial);
            Assert.AreEqual(1, root.ResolveChildren().Count(s => s.IsInitial));
        }

        [Test]
        public void ProgrammaticDuplicateOfInitialDoesNotDuplicateInitialState()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-duplicate"));
            var root = graph.FindRoot();
            var initial = FyniteStateOperations.CreateChild(graph, root, Vector2.zero);
            var copy = FyniteStateOperations.Duplicate(graph, initial, Vector2.right);

            Assert.IsTrue(initial.IsInitial);
            Assert.IsFalse(copy.IsInitial);
            Assert.AreNotEqual(initial.FyniteGuid, copy.FyniteGuid);
            Assert.AreEqual(1, root.ResolveChildren().Count(s => s.IsInitial));
        }

        [Test]
        public void MoveRejectsSelfAndIndirectCycleAndReparentsWithInitialRules()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("task2-move"));
            var root = graph.FindRoot();
            var parent = FyniteStateOperations.CreateChild(graph, root, Vector2.zero);
            var destination = FyniteStateOperations.CreateChild(graph, root, Vector2.right);
            var child = FyniteStateOperations.CreateChild(graph, parent, Vector2.down);
            var grandchild = FyniteStateOperations.CreateChild(graph, child, Vector2.down * 2f);

            Assert.IsFalse(FyniteStateOperations.MoveToParent(graph, parent, parent, out var self));
            StringAssert.Contains("own Parent", self);
            Assert.IsFalse(FyniteStateOperations.MoveToParent(graph, parent, grandchild, out var cycle));
            StringAssert.Contains("cycle", cycle);
            Assert.IsTrue(FyniteStateOperations.MoveToParent(graph, child, destination, out _));
            Assert.AreSame(destination, child.ResolveParent());
            Assert.IsTrue(child.IsInitial, "the first child of an empty destination becomes Initial");
            Assert.AreSame(child, grandchild.ResolveParent());
        }

        [Test]
        public void RenameDoesNotChangeCompiledSemanticsOrGuidReactionResolution()
        {
            var graph = TestGraphFactory.CreateReferenceGraph(TestGraphFactory.NewPath("task2-compile"));
            var before = FyniteGraphCompiler.Compile(FyniteGraphProjection.ToDocument(graph));
            var idle = graph.GetNodes().OfType<FyniteStateNode>().Single(s => s.StateName == "Idle");
            var targetGuid = idle.ResolveReactions().Single().ResolveTarget().FyniteGuid;

            FyniteStateOperations.Rename(graph, idle, "Waiting");
            var after = FyniteGraphCompiler.Compile(FyniteGraphProjection.ToDocument(graph));

            Assert.IsTrue(before.Succeeded, before.DescribeErrors());
            Assert.IsTrue(after.Succeeded, after.DescribeErrors());
            Assert.AreEqual(before.SchemaVersion, after.SchemaVersion);
            Assert.AreEqual(targetGuid, idle.ResolveReactions().Single().ResolveTarget().FyniteGuid);
        }
    }
}
