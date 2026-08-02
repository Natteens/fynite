using System.IO;
using System.Linq;
using Fynite.Authoring;
using Fynite.GraphEditor;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fynite.Tests
{
    public sealed class GraphTask1MigrationTests
    {
        [TearDown]
        public void TearDown() => TestGraphFactory.CleanUp();

        [Test]
        public void RootAndStateExposeOnlyTheirOwnStructure()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("root-shape"));
            var root = graph.FindRoot();
            var state = new FyniteStateNode();
            graph.AddNode(state);

            Assert.IsNotNull(root.GetOutputPortByName(FyniteRootStateNode.ChildrenPort));
            Assert.IsNull(root.GetInputPortByName(FyniteStateNode.ParentPort));
            Assert.IsNull(root.GetOutputPortByName(FyniteStateNode.ReactionsPort));
            Assert.IsNull(root.GetOutputPortByName(FyniteStateNode.IncomingPort));
            Assert.IsNull(root.GetNodeOptionByName(FyniteStateNode.InitialOption));
            Assert.IsFalse(typeof(ContextNode).IsAssignableFrom(root.GetType()), "Root cannot contain blocks");

            Assert.IsNotNull(state.GetInputPortByName(FyniteStateNode.ParentPort));
            Assert.IsNull(state.GetNodeOptionByName("isRoot"));
            Assert.IsNotNull(state.GetNodeOptionByName(FyniteStateNode.InitialOption));
        }

        [Test]
        public void ASecondStructuralRootIsDiagnosedAndNeverChosenSilently()
        {
            var graph = FyniteGraphCreation.Create(TestGraphFactory.NewPath("two-current-roots"));
            graph.AddNode(new FyniteRootStateNode());

            var logger = RecordingGraphLogger.Create(out var recording);
            graph.OnGraphChanged(logger);

            Assert.AreEqual(2, graph.FindRoots().Count);
            Assert.IsTrue(recording.Errors.Any(e => e.Message.Contains("one root")));
        }

        [Test]
        public void PlainLegacyRootAndSettingsMigrateWithoutChangingIdentityPositionOrChildren()
        {
            var graph = LoadLegacy("LegacyPlainRoot.fyn");
            var oldRoot = graph.GetNodes().OfType<FyniteStateNode>().Single(s => s.StateName == "Root");
            var guid = oldRoot.FyniteGuid;
            var position = oldRoot.Position;
            var childGuids = oldRoot.ResolveChildren().Select(c => c.FyniteGuid).OrderBy(x => x).ToArray();

            var first = FyniteGraphMigration.Apply(graph);
            var second = FyniteGraphMigration.Apply(graph);

            Assert.IsTrue(first.Changed);
            Assert.IsFalse(first.Blocked);
            Assert.AreEqual(typeof(GraphTestContext), graph.ResolveContextType());
            Assert.AreEqual(0, graph.GetNodes().OfType<FyniteConfigNode>().Count());
            Assert.AreEqual(guid, graph.FindRoot().FyniteGuid);
            Assert.AreEqual(position, graph.FindRoot().Position);
            CollectionAssert.AreEqual(childGuids, graph.FindRoot().ResolveChildren().Select(c => c.FyniteGuid).OrderBy(x => x).ToArray());
            Assert.IsFalse(second.Changed, "migration is idempotent");
        }

        [Test]
        public void BehaviouralLegacyRootIsPreservedBelowANewStructuralRoot()
        {
            var graph = LoadLegacy("LegacyBehaviourRoot.fyn");
            var oldRoot = graph.GetNodes().OfType<FyniteStateNode>().Single(s => s.StateName == "Machine");
            var guid = oldRoot.FyniteGuid;
            int blocks = oldRoot.BlockCount;
            var children = oldRoot.ResolveChildren().Select(c => c.FyniteGuid).OrderBy(x => x).ToArray();

            var result = FyniteGraphMigration.Apply(graph);

            Assert.IsFalse(result.Blocked);
            Assert.IsNotNull(graph.FindRoot());
            var preserved = graph.GetNodes().OfType<FyniteStateNode>().Single(s => s.FyniteGuid == guid);
            Assert.AreEqual(blocks, preserved.BlockCount);
            Assert.AreSame(graph.FindRoot(), preserved.ResolveParent());
            Assert.IsTrue(preserved.IsInitial);
            CollectionAssert.AreEqual(children, preserved.ResolveChildren().Select(c => c.FyniteGuid).OrderBy(x => x).ToArray());
            Assert.IsFalse(FyniteGraphMigration.Apply(graph).Changed);
        }

        [Test]
        public void ConflictingLegacySettingsAndMultipleLegacyRootsPreserveAllData()
        {
            var settings = LoadLegacy("LegacyTwoSettings.fyn");
            int settingsBefore = settings.GetNodes().OfType<FyniteConfigNode>().Count();
            var contextResult = FyniteGraphMigration.Apply(settings);

            Assert.IsTrue(contextResult.Blocked);
            Assert.AreEqual(settingsBefore, settings.GetNodes().OfType<FyniteConfigNode>().Count());
            Assert.IsTrue(contextResult.Diagnostics.Any(d => d.Code == FyniteDiagnosticCodes.LegacyContextConflict));

            TestGraphFactory.CleanUp();
            var roots = LoadLegacy("LegacyTwoRoots.fyn");
            var identities = roots.GetNodes().OfType<FyniteStateNode>().Select(s => s.FyniteGuid).OrderBy(x => x).ToArray();
            var rootResult = FyniteGraphMigration.Apply(roots);

            Assert.IsTrue(rootResult.Blocked);
            Assert.IsNull(roots.FindRoot());
            CollectionAssert.AreEqual(identities, roots.GetNodes().OfType<FyniteStateNode>().Select(s => s.FyniteGuid).OrderBy(x => x).ToArray());
            Assert.IsTrue(rootResult.Diagnostics.Any(d => d.Code == FyniteDiagnosticCodes.LegacyRootAmbiguous));
        }

        private static FyniteGraph LoadLegacy(string name)
        {
            var path = TestGraphFactory.NewPath(Path.GetFileNameWithoutExtension(name));
            File.Copy(
                Path.GetFullPath("Packages/com.natteens.fynite/Tests/EditorGraph/LegacyGraphs~/" + name),
                Path.GetFullPath(path));

            LogAssert.ignoreFailingMessages = true;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            LogAssert.ignoreFailingMessages = false;
            return GraphDatabase.LoadGraph<FyniteGraph>(path);
        }
    }
}
