using System;
using System.Linq;
using Fynite.GraphEditor;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>
    /// A state's name: where it is edited, what it defaults to, and what it must never disturb.
    /// </summary>
    /// <remarks>
    /// The name is edited in Graph Toolkit's own Graph Inspector, through a node option called Name.
    /// Nothing in the package draws that field, so what these tests can reach is the model behind it:
    /// the option's definition and default, and <see cref="FyniteStateNode.SyncDisplayName"/>, which is
    /// what turns an edit into a title and a stored name. Typing into the field itself is not
    /// reachable from a test — Graph Toolkit exposes no way to write a node option — so step 4 of the
    /// manual pass is the only place the typed path is exercised.
    /// </remarks>
    public sealed class StateNameTests
    {
        [TearDown]
        public void TearDown() => TestGraphFactory.CleanUp();

        private static FyniteGraph NewGraph(string name)
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath(name));
            graph.SetContextType(typeof(GraphTestContext));
            graph.TouchForFieldChange();
            return graph;
        }

        private static FyniteStateNode AddState(FyniteGraph graph, IFyniteIdentifiedNode parent) =>
            FyniteStateOperations.CreateChild(graph, parent, Vector2.zero);

        // ── the default ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void ANewStateIsCalledState()
        {
            var graph = NewGraph("name-default");
            var state = AddState(graph, graph.FindRoot());

            Assert.AreEqual(FyniteStateNode.DefaultName, state.StateName);
            Assert.AreEqual("State", state.StateName);
            Assert.AreEqual("State", state.Title, "the header shows the same name the inspector will");
        }

        [Test]
        public void TheGraphInspectorGetsAStringNameFieldDefaultingToTheStateName()
        {
            var graph = NewGraph("name-option");
            var state = AddState(graph, graph.FindRoot());
            state.DefineNode();

            var option = state.GetNodeOptionByName(FyniteStateNode.NameOption);

            Assert.IsNotNull(option, "the Graph Inspector needs an option to draw");
            Assert.AreEqual("Name", option.DisplayName);
            Assert.AreEqual(typeof(string), option.DataType);

            Assert.IsTrue(option.TryGetValue<string>(out var value));
            Assert.AreEqual("State", value, "the field opens on the name the state already has");
        }

        [Test]
        public void ARenamedStateOffersItsCurrentNameAsTheFieldsDefault()
        {
            var graph = NewGraph("name-option-renamed");
            var state = AddState(graph, graph.FindRoot());

            Assert.IsTrue(FyniteStateOperations.Rename(graph, state, "Idle"));
            state.DefineNode();

            Assert.IsTrue(state.GetNodeOptionByName(FyniteStateNode.NameOption).TryGetValue<string>(out var value));
            Assert.AreEqual("Idle", value, "reopening the inspector shows the name in force, not the default");
        }

        // ── renaming ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void RenamingUpdatesTheHeaderWithoutTouchingTheAssetDatabase()
        {
            var graph = NewGraph("name-immediate");
            var state = AddState(graph, graph.FindRoot());

            state.SetDisplayName("Idle");

            Assert.AreEqual("Idle", state.Title, "the title follows the name in the same call");
            Assert.AreEqual("Idle", state.StateName);
        }

        [Test]
        public void ATitleEditedOnTheCanvasIsAdoptedAsTheName()
        {
            var graph = NewGraph("name-from-title");
            var state = AddState(graph, graph.FindRoot());

            // Renaming a node header on the canvas writes Title and nothing else. OnGraphChanged then
            // runs SyncDisplayName, which is what makes that edit stick.
            state.Title = "Moving";
            state.SyncDisplayName();

            Assert.AreEqual("Moving", state.StateName);
        }

        [Test]
        public void AnEmptyNameFallsBackToTheDefaultRatherThanAStateWithNoName()
        {
            var graph = NewGraph("name-empty");
            var state = AddState(graph, graph.FindRoot());

            state.SetDisplayName("   ");
            Assert.AreEqual("State", state.StateName, "whitespace is not a name");

            Assert.IsFalse(FyniteStateOperations.Rename(graph, state, "  "), "and it is not accepted as one");
        }

        // ── what a rename must not disturb ──────────────────────────────────────────────────────

        [Test]
        public void RenameKeepsTheGuidTheHierarchyTheReactionsAndThePosition()
        {
            var path = TestGraphFactory.NewPath("name-preserves");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var idle = graph.GetNodes().OfType<FyniteStateNode>().Single(s => s.StateName == "Idle");
            var guid = idle.FyniteGuid;
            var parent = idle.ResolveParent().FyniteGuid;
            var position = idle.Position;
            var blocks = idle.BlockCount;
            var reactions = idle.ResolveReactions().Select(r => r.FyniteGuid).ToList();
            var incoming = idle.ResolveIncoming().Select(r => r.FyniteGuid).ToList();
            var initial = idle.IsInitial;

            Assert.IsTrue(FyniteStateOperations.Rename(graph, idle, "Waiting"));

            Assert.AreEqual("Waiting", idle.StateName);
            Assert.AreEqual(guid, idle.FyniteGuid, "identity");
            Assert.AreEqual(parent, idle.ResolveParent().FyniteGuid, "parent");
            Assert.AreEqual(position, idle.Position, "position");
            Assert.AreEqual(blocks, idle.BlockCount, "blocks");
            Assert.AreEqual(initial, idle.IsInitial, "initial mark");
            CollectionAssert.AreEqual(reactions, idle.ResolveReactions().Select(r => r.FyniteGuid).ToList());
            CollectionAssert.AreEqual(incoming, idle.ResolveIncoming().Select(r => r.FyniteGuid).ToList());
        }

        // ── persistence and the compiler ────────────────────────────────────────────────────────

        [Test]
        public void ARenameSurvivesSaveAndReload()
        {
            var path = TestGraphFactory.NewPath("name-persist");
            var graph = FyniteGraphCreation.Create(path);
            graph.SetContextType(typeof(GraphTestContext));
            var state = AddState(graph, graph.FindRoot());
            var guid = state.FyniteGuid;

            Assert.IsTrue(FyniteStateOperations.Rename(graph, state, "Idle"));
            FyniteGraphCreation.SaveAndReimport(graph);

            var reloaded = GraphDatabase.LoadGraph<FyniteGraph>(path);
            var same = FyniteStateOperations.FindState(reloaded, guid);

            Assert.IsNotNull(same, "the state comes back under the same identity");
            Assert.AreEqual("Idle", same.StateName, "and under the name it was given");
            Assert.AreEqual("Idle", same.Title, "with the header already showing it");
        }

        [Test]
        public void TheCompiledMachineKnowsTheStateByItsNewName()
        {
            var path = TestGraphFactory.NewPath("name-compiles");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var idle = graph.GetNodes().OfType<FyniteStateNode>().Single(s => s.StateName == "Idle");
            Assert.IsTrue(FyniteStateOperations.Rename(graph, idle, "Waiting"));
            FyniteGraphCreation.SaveAndReimport(graph);

            var asset = AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path);
            Assert.IsNotNull(asset);
            Assert.IsNull(asset.CompilationError, asset.CompilationError);

            var definition = asset.Definition;
            var names = Enumerable.Range(0, definition.StateCount)
                .Select(i => definition.GetStateName(definition.GetState(i)))
                .ToList();

            CollectionAssert.Contains(names, "Waiting", "the compiler reads the new name");
            CollectionAssert.DoesNotContain(names, "Idle", "and nothing kept the old one");
            Assert.AreEqual(
                "Waiting",
                definition.GetStateName(definition.GetInitialChild(definition.Root)),
                "renaming the initial child does not change which child is initial");
        }

        // ── the surface that was removed ────────────────────────────────────────────────────────

        [Test]
        public void TheAssetInspectorHasNoStateAuthoringSurface()
        {
            // Editing states belongs on the canvas, not in the inspector of the file. These types were
            // that mistake; a guard here is cheaper than noticing the second surface has grown back.
            foreach (var removed in new[]
                     {
                         "Fynite.GraphEditor.FyniteStateAuthoringSession",
                         "Fynite.GraphEditor.FyniteStateAuthoringGUI",
                         "Fynite.GraphEditor.FyniteStateScopeWindow",
                         "Fynite.GraphEditor.FyniteStateScopeSession",
                     })
            {
                Assert.IsNull(
                    Type.GetType(removed + ", Fynite.Editor.Graph"),
                    removed + " is back; state editing must stay on the canvas");
            }
        }
    }
}
