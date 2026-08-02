using System.Linq;
using Fynite.Authoring;
using Fynite.GraphEditor;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>
    /// How the visual layer behaves as a model: identity, renaming, duplication and the projection into
    /// the authoring document.
    /// </summary>
    public sealed class GraphAuthoringTests
    {
        [TearDown]
        public void TearDown() => TestGraphFactory.CleanUp();

        [Test]
        public void ANewGraphStartsValidExceptForItsContext()
        {
            var path = TestGraphFactory.NewPath("fresh");
            var graph = FyniteGraphCreation.Create(path);

            Assert.IsNotNull(graph.GraphGuid, "a new graph has an identity immediately");
            Assert.AreEqual(FyniteGraphDocument.CurrentSchemaVersion, graph.SchemaVersion);
            Assert.IsFalse(graph.ContextTypeReference.IsSet, "context starts pending in graph metadata");
            Assert.AreEqual(1, graph.GetNodes().OfType<FyniteRootStateNode>().Count(), "one structural root is created");
            Assert.AreEqual(0, graph.GetNodes().OfType<FyniteStateNode>().Count(), "no executable state is invented");
            Assert.AreEqual(0, graph.GetNodes().OfType<FyniteConfigNode>().Count(), "settings are not represented on the canvas");

            var document = FyniteGraphProjection.ToDocument(graph);
            var result = FyniteGraphCompiler.Compile(document);

            Assert.IsFalse(result.Succeeded, "it cannot compile until a context is chosen");
            Assert.IsTrue(
                result.Diagnostics.Any(d => d.Code == FyniteDiagnosticCodes.ContextMissing),
                string.Join("\n", result.Diagnostics.Select(d => d.ToString())));
        }

        [Test]
        public void EveryNodeGetsItsOwnPersistentIdentity()
        {
            var path = TestGraphFactory.NewPath("identities");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var identities = graph.GetNodes()
                .OfType<IFyniteIdentifiedNode>()
                .Select(n => n.FyniteGuid)
                .ToList();

            Assert.IsFalse(identities.Any(string.IsNullOrEmpty), "no node is left without an identity");
            CollectionAssert.AllItemsAreUnique(identities);
        }

        [Test]
        public void BlockOccurrencesAlsoCarryTheirOwnIdentities()
        {
            var path = TestGraphFactory.NewPath("block-identities");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var blockIdentities = graph.GetNodes()
                .OfType<ContextNode>()
                .SelectMany(c => c.BlockNodes)
                .OfType<IFyniteIdentifiedNode>()
                .Select(b => b.FyniteGuid)
                .ToList();

            Assert.IsNotEmpty(blockIdentities);
            Assert.IsFalse(blockIdentities.Any(string.IsNullOrEmpty));
            CollectionAssert.AllItemsAreUnique(blockIdentities);
        }

        [Test]
        public void DuplicatedIdentitiesAreReissuedSoPastedNodesAreDistinct()
        {
            var path = TestGraphFactory.NewPath("paste");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var original = graph.GetNodes().OfType<FyniteStateNode>().First(s => s.StateName == "Idle");

            // Pasting clones a node's serialized fields, identity included. This reproduces that.
            var pasted = new FyniteStateNode();
            graph.AddNode(pasted);
            pasted.SetDisplayName("Idle");

            var clashing = typeof(FyniteStateNode)
                .GetField("m_FyniteGuid", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            clashing.SetValue(pasted, original.FyniteGuid);

            Assert.AreEqual(original.FyniteGuid, pasted.FyniteGuid, "the clash exists before the graph reconciles");

            graph.OnGraphChanged(RecordingGraphLogger.Create(out _));

            Assert.AreNotEqual(original.FyniteGuid, pasted.FyniteGuid, "the duplicate was given a new identity");
            Assert.IsNotEmpty(pasted.FyniteGuid);
        }

        [Test]
        public void RenamingAStateDoesNotDisturbTheHierarchyOrItsReactions()
        {
            var path = TestGraphFactory.NewPath("rename-state");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var moving = graph.GetNodes().OfType<FyniteStateNode>().First(s => s.StateName == "Moving");
            var identityBefore = moving.FyniteGuid;

            moving.SetDisplayName("Running");
            graph.OnGraphChanged(RecordingGraphLogger.Create(out _));

            var document = FyniteGraphProjection.ToDocument(graph);
            var result = FyniteGraphCompiler.Compile(document);

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Diagnostics.Select(d => d.ToString())));
            Assert.AreEqual(identityBefore, moving.FyniteGuid, "renaming never changes identity");
            Assert.IsTrue(result.Graph.states.Any(s => s.name == "Running"));
            Assert.AreEqual(3, result.Graph.reactions.Length, "every reaction still resolves");
        }

        [Test]
        public void RenamingASignalKeepsItsListenersAndItsExternalIdentity()
        {
            var path = TestGraphFactory.NewPath("rename-signal");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var move = graph.GetNodes().OfType<FyniteSignalNode>().First(s => s.SignalName == "Move");
            var identityBefore = move.FyniteGuid;

            Assert.AreEqual(1, move.ResolveListeners().Count);

            move.SetDisplayName("Advance");
            graph.OnGraphChanged(RecordingGraphLogger.Create(out _));

            Assert.AreEqual(identityBefore, move.FyniteGuid);
            Assert.AreEqual(1, move.ResolveListeners().Count, "the wire to its reaction is untouched");

            var result = FyniteGraphCompiler.Compile(FyniteGraphProjection.ToDocument(graph));
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(identityBefore, result.Graph.signals.Single(s => s.name == "Advance").guid);
        }

        [Test]
        public void TheProjectionCarriesPositionsWithoutLettingThemMatter()
        {
            var path = TestGraphFactory.NewPath("positions");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var idle = graph.GetNodes().OfType<FyniteStateNode>().First(s => s.StateName == "Idle");
            var document = FyniteGraphProjection.ToDocument(graph);

            Assert.AreEqual(idle.Position, document.FindState(idle.FyniteGuid).position);

            var before = FyniteGraphCompiler.Compile(document);

            idle.Position = new Vector2(-4000f, 4000f);
            var after = FyniteGraphCompiler.Compile(FyniteGraphProjection.ToDocument(graph));

            Assert.IsTrue(before.Succeeded && after.Succeeded);
            CollectionAssert.AreEqual(
                before.Graph.states.Select(s => s.name).ToArray(),
                after.Graph.states.Select(s => s.name).ToArray(),
                "moving a node cannot change what the graph compiles to");
        }

        [Test]
        public void PortTypesMakeInvalidWiringImpossibleToDraw()
        {
            var path = TestGraphFactory.NewPath("ports");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var idle = graph.GetNodes().OfType<FyniteStateNode>().First(s => s.StateName == "Idle");
            var signal = graph.GetNodes().OfType<FyniteSignalNode>().First();

            // A signal wire into a hierarchy port is refused by Graph Toolkit itself, because the two
            // ports carry different data types.
            Assert.Throws<System.ArgumentException>(() => graph.Connect(
                signal.GetOutputPortByName(FyniteSignalNode.SignalPort),
                idle.GetInputPortByName(FyniteStateNode.ParentPort)));
        }

        [Test]
        public void AStateCannotBeGivenTwoParents()
        {
            var path = TestGraphFactory.NewPath("one-parent");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var states = graph.GetNodes().OfType<FyniteStateNode>().ToList();
            var idle = states.First(s => s.StateName == "Idle");
            var moving = states.First(s => s.StateName == "Moving");

            Assert.Throws<System.ArgumentException>(() => graph.Connect(
                moving.GetOutputPortByName(FyniteStateNode.ChildrenPort),
                idle.GetInputPortByName(FyniteStateNode.ParentPort)));
        }

        [Test]
        public void ValidationReportsThroughGraphToolkitOnTheOffendingNode()
        {
            var path = TestGraphFactory.NewPath("validation");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            graph.RemoveNode(graph.FindRoot());

            var document = FyniteGraphProjection.ToDocument(graph);
            var result = FyniteGraphCompiler.Compile(document);

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Diagnostics.Any(d => d.Code == FyniteDiagnosticCodes.RootMissing));

            // The same call the graph window makes; it must not throw whatever the graph looks like.
            var logger = RecordingGraphLogger.Create(out var recorder);
            Assert.DoesNotThrow(() => graph.OnGraphChanged(logger));

            Assert.IsTrue(
                recorder.Errors.Any(e => e.Message.Contains(FyniteDiagnosticCodes.RootMissing)),
                "the graph reports the missing root through Graph Toolkit's logger");
        }

        [Test]
        public void ABlockAddedWithoutConfiguringItKeepsItsOwnDefaults()
        {
            var path = TestGraphFactory.NewPath("block-defaults");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var moving = graph.GetNodes().OfType<FyniteStateNode>().First(s => s.StateName == "Moving");

            // Added and never touched: the option must start at the field's own initialiser, not at
            // default(T), or every block would silently lose its defaults the moment it was used.
            TestGraphFactory.AddBlock<FyniteEnterActionNode, GraphTestEnterAction>(moving);

            var document = FyniteGraphProjection.ToDocument(graph);
            var block = document.FindState(moving.FyniteGuid).enter.Single();

            StringAssert.Contains("enter", block.configJson,
                "the block's declared default survived being added to the graph");
        }

        [Test]
        public void TheProjectionReadsBlockConfigurationBackOutOfTheNodes()
        {
            var path = TestGraphFactory.NewPath("config");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var idle = graph.GetNodes().OfType<FyniteStateNode>().First(s => s.StateName == "Idle");
            var document = FyniteGraphProjection.ToDocument(graph);
            var state = document.FindState(idle.FyniteGuid);

            Assert.AreEqual(1, state.enter.Count);
            StringAssert.Contains("idleEnter", state.enter[0].configJson);
            Assert.AreEqual(FyniteTypeReference.From(typeof(GraphTestEnterAction)), state.enter[0].blockType);
        }
    }
}
