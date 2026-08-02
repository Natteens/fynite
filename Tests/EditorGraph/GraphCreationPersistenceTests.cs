using System.IO;
using System.Linq;
using Fynite.Authoring;
using Fynite.GraphEditor;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>Regression coverage for the persisted file produced by Assets/Create.</summary>
    public sealed class GraphCreationPersistenceTests
    {
        [TearDown]
        public void TearDown() => TestGraphFactory.CleanUp();

        [Test]
        public void PublicCreationFactoryImmediatelyPersistsExactlyOneStructuralRoot()
        {
            var path = TestGraphFactory.NewPath("menu-create");
            var graph = FyniteGraphCreation.Create(path);
            var root = graph.FindRoot();

            Assert.IsNotNull(root);
            Assert.IsNotEmpty(root.FyniteGuid);
            Assert.AreEqual(Vector2.zero, root.Position);
            Assert.AreEqual(FyniteGraphDocument.CurrentSchemaVersion, graph.SchemaVersion);
            Assert.IsNotEmpty(graph.GraphGuid);
            Assert.IsFalse(graph.ContextTypeReference.IsSet);
            Assert.AreEqual(1, graph.GetNodes().OfType<FyniteRootStateNode>().Count());
            Assert.AreEqual(0, graph.GetNodes().OfType<FyniteStateNode>().Count());
            Assert.AreEqual(0, graph.GetNodes().OfType<FyniteConfigNode>().Count());
            Assert.AreEqual(0, graph.GetNodes().OfType<FyniteReactionNode>().Count());

            var serialized = File.ReadAllText(Path.GetFullPath(path));
            StringAssert.Contains("FyniteRootStateNode", serialized, "the Root must exist in the .fyn file itself");
            StringAssert.Contains(root.FyniteGuid, serialized, "the persisted Root carries its Fynite GUID");
            StringAssert.Contains("m_SchemaVersion: " + FyniteGraphDocument.CurrentSchemaVersion, serialized);
            StringAssert.Contains("m_GraphGuid: " + graph.GraphGuid, serialized);
        }

        [Test]
        public void RepeatedReimportAndReloadPreserveTheOnlyRootAndItsGuid()
        {
            var path = TestGraphFactory.NewPath("menu-reimport");
            var created = FyniteGraphCreation.Create(path);
            var expectedGuid = created.FindRoot().FyniteGuid;

            for (int i = 0; i < 2; i++)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var reloaded = GraphDatabase.LoadGraph<FyniteGraph>(path);
                Assert.AreEqual(1, reloaded.FindRoots().Count, "reimport " + (i + 1));
                Assert.AreEqual(expectedGuid, reloaded.FindRoot().FyniteGuid, "reimport " + (i + 1));
                Assert.AreEqual(expectedGuid, FyniteGraphProjection.ToDocument(reloaded).states.Single(s => s.isRoot).guid);
            }
        }

        [Test]
        public void FreshGraphHasNoMissingRootDiagnostic()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("menu-diagnostics"));
            var result = FyniteGraphCompiler.Compile(FyniteGraphProjection.ToDocument(graph));

            Assert.IsFalse(result.Diagnostics.Any(d => d.Code == FyniteDiagnosticCodes.RootMissing));
            Assert.AreEqual(1, result.Diagnostics.Count(d => d.Code == FyniteDiagnosticCodes.ContextMissing));
        }

        [Test]
        public void InspectorSessionAppliesAndRevertsWithoutWritingFromThePicker()
        {
            var path = TestGraphFactory.NewPath("inspector-apply-revert");
            FyniteGraphCreation.Create(path);
            var absolute = Path.GetFullPath(path);
            var original = File.ReadAllText(absolute);
            var session = FyniteGraphInspectorSession.Load(path);

            session.Select(typeof(GraphTestContext));
            Assert.IsTrue(session.HasChanges);
            Assert.AreEqual(original, File.ReadAllText(absolute), "selecting is only staged");

            session.Revert();
            Assert.IsFalse(session.HasChanges);
            Assert.AreEqual(original, File.ReadAllText(absolute), "Revert never writes or imports");
            Assert.IsFalse(GraphDatabase.LoadGraph<FyniteGraph>(path).ContextTypeReference.IsSet);

            session.Select(typeof(GraphTestContext));
            session.Apply();
            Assert.IsFalse(session.HasChanges);
            Assert.AreEqual(typeof(GraphTestContext), GraphDatabase.LoadGraph<FyniteGraph>(path).ResolveContextType());
            var compiled = AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path);
            Assert.AreEqual(typeof(GraphTestContext), compiled.ContextType, compiled.CompilationError);
        }

        [Test]
        public void RepeatedSelectionsProduceOnePendingChangeAndOnePersistedValue()
        {
            var path = TestGraphFactory.NewPath("inspector-repeat");
            FyniteGraphCreation.Create(path);
            var session = FyniteGraphInspectorSession.Load(path);

            session.Select(typeof(GraphTestContext));
            session.Select(typeof(AlternateGraphTestContext));
            session.Select(typeof(GraphTestContext));

            Assert.IsTrue(session.HasChanges);
            Assert.IsFalse(GraphDatabase.LoadGraph<FyniteGraph>(path).ContextTypeReference.IsSet);

            session.Apply();
            Assert.AreEqual(typeof(GraphTestContext), GraphDatabase.LoadGraph<FyniteGraph>(path).ResolveContextType());

            var after = File.ReadAllText(Path.GetFullPath(path));
            session.Apply();
            Assert.AreEqual(after, File.ReadAllText(Path.GetFullPath(path)), "Apply with no pending change is a no-op");
        }
    }
}
