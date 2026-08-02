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
    /// The <c>.fyn</c> file as an asset: creating it, importing it, reimporting it, and what happens
    /// when it stops compiling.
    /// </summary>
    public sealed class GraphImporterTests
    {
        [TearDown]
        public void TearDown() => TestGraphFactory.CleanUp();

        [Test]
        public void ANewGraphImportsIntoARunnableAsset()
        {
            var path = TestGraphFactory.NewPath("reference");
            TestGraphFactory.CreateReferenceGraph(path);

            var asset = AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path);

            Assert.IsNotNull(asset, "the compiled asset is the main object of the .fyn file");
            Assert.IsNull(asset.CompilationError, asset.CompilationError);
            Assert.IsTrue(asset.IsExecutable);
            Assert.AreEqual(typeof(GraphTestContext), asset.ContextType);
            Assert.AreEqual(3, asset.Definition.StateCount);
            Assert.AreEqual(3, asset.Definition.SignalCount);
        }

        [Test]
        public void TheCompiledAssetKeepsItsLocalIdAcrossReimports()
        {
            var path = TestGraphFactory.NewPath("stable-id");
            TestGraphFactory.CreateReferenceGraph(path);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path), out string guidBefore, out long idBefore);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path), out string guidAfter, out long idAfter);

            // A changing local id is what breaks every scene and prefab reference on recompile.
            Assert.AreEqual(guidBefore, guidAfter, "asset guid");
            Assert.AreEqual(idBefore, idAfter, "local file id");
        }

        [Test]
        public void ReimportingPreservesIdentitiesNamesAndPositions()
        {
            var path = TestGraphFactory.NewPath("reimport");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var before = graph.GetNodes()
                .OfType<FyniteStateNode>()
                .ToDictionary(s => s.FyniteGuid, s => new { s.StateName, s.Position });

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var reloaded = GraphDatabase.LoadGraph<FyniteGraph>(path);
            var after = reloaded.GetNodes()
                .OfType<FyniteStateNode>()
                .ToDictionary(s => s.FyniteGuid, s => new { s.StateName, s.Position });

            CollectionAssert.AreEquivalent(before.Keys, after.Keys, "state identities survive a reimport");

            foreach (var pair in before)
            {
                Assert.AreEqual(pair.Value.StateName, after[pair.Key].StateName, "name of " + pair.Key);
                Assert.AreEqual(pair.Value.Position, after[pair.Key].Position, "position of " + pair.Key);
            }
        }

        [Test]
        public void ReimportingWithoutChangesProducesTheSameCompiledStructure()
        {
            var path = TestGraphFactory.NewPath("determinism");
            TestGraphFactory.CreateReferenceGraph(path);

            var first = Describe(AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path));

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var second = Describe(AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path));

            Assert.AreEqual(first, second);
        }

        [Test]
        public void AGraphThatStopsCompilingReportsAndStopsBeingRunnable()
        {
            var path = TestGraphFactory.NewPath("breakable");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            Assert.IsTrue(AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path).IsExecutable);

            // Un-mark the root, which is a graph the compiler must refuse.
            var root = graph.GetNodes().OfType<FyniteStateNode>().First(s => s.IsRoot);
            FyniteNodeOptions.Set(root, FyniteStateNode.RootOption, false);

            LogAssert.ignoreFailingMessages = true;
            FyniteGraphCreation.SaveAndReimport(graph);
            LogAssert.ignoreFailingMessages = false;

            var asset = AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path);

            Assert.IsNotNull(asset, "the asset still exists so references do not break");
            Assert.IsNotNull(asset.CompilationError, "the failure is recorded on the asset");
            Assert.IsFalse(asset.IsExecutable, "a failed compile must not leave a runnable graph behind");
            Assert.Throws<System.InvalidOperationException>(() => _ = asset.Definition);
        }

        [Test]
        public void FixingAGraphMakesItCompileAgain()
        {
            var path = TestGraphFactory.NewPath("fixable");
            var graph = TestGraphFactory.CreateReferenceGraph(path);

            var root = graph.GetNodes().OfType<FyniteStateNode>().First(s => s.IsRoot);

            LogAssert.ignoreFailingMessages = true;
            FyniteNodeOptions.Set(root, FyniteStateNode.RootOption, false);
            FyniteGraphCreation.SaveAndReimport(graph);
            LogAssert.ignoreFailingMessages = false;

            Assert.IsFalse(AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path).IsExecutable);

            FyniteNodeOptions.Set(root, FyniteStateNode.RootOption, true);
            FyniteGraphCreation.SaveAndReimport(graph);

            var asset = AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path);
            Assert.IsNull(asset.CompilationError, asset.CompilationError);
            Assert.IsTrue(asset.IsExecutable);
        }

        [Test]
        public void DeletingTheGraphRemovesEverythingItProduced()
        {
            var path = TestGraphFactory.NewPath("deletable");
            TestGraphFactory.CreateReferenceGraph(path);

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path));

            Assert.IsTrue(AssetDatabase.DeleteAsset(path));
            AssetDatabase.Refresh();

            // The compiled asset lives inside the imported file, so deleting the file takes it with it —
            // there is no separate artefact that could be left orphaned.
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path));
            Assert.IsEmpty(AssetDatabase.LoadAllAssetsAtPath(path));
        }

        [Test]
        public void ImportingDoesNotRewriteTheGraphFileAndSoCannotLoop()
        {
            var path = TestGraphFactory.NewPath("no-loop");
            TestGraphFactory.CreateReferenceGraph(path);

            var absolute = System.IO.Path.GetFullPath(path);
            var contentBefore = System.IO.File.ReadAllText(absolute);
            var writtenBefore = System.IO.File.GetLastWriteTimeUtc(absolute);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            // An importer that writes its own input reimports forever. This one only reads.
            Assert.AreEqual(contentBefore, System.IO.File.ReadAllText(absolute));
            Assert.AreEqual(writtenBefore, System.IO.File.GetLastWriteTimeUtc(absolute));
        }

        [Test]
        public void TheGraphKeepsItsIdentityAndSchemaVersionOnDisk()
        {
            var path = TestGraphFactory.NewPath("identity");
            var graph = TestGraphFactory.CreateReferenceGraph(path);
            var expectedGuid = graph.GraphGuid;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var asset = AssetDatabase.LoadAssetAtPath<FyniteGraphAsset>(path);
            Assert.AreEqual(expectedGuid, asset.GraphGuid);
            Assert.AreEqual(Fynite.Authoring.FyniteGraphDocument.CurrentSchemaVersion, asset.SchemaVersion);
        }

        private static string Describe(FyniteGraphAsset asset)
        {
            var definition = asset.Definition;
            var text = new System.Text.StringBuilder();

            for (int i = 0; i < definition.StateCount; i++)
            {
                var state = definition.GetState(i);
                text.Append(definition.GetStateName(state))
                    .Append('/')
                    .Append(definition.GetDepth(state))
                    .Append(';');
            }

            for (int i = 0; i < definition.SignalCount; i++)
            {
                var signal = definition.GetSignal(i);
                text.Append(definition.GetSignalName(signal))
                    .Append(':')
                    .Append(definition.GetPayloadType(signal)?.Name ?? "-")
                    .Append(';');
            }

            return text.ToString();
        }
    }
}
