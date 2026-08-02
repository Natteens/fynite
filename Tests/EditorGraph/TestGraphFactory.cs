using System;
using System.IO;
using System.Linq;
using Fynite.GraphEditor;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.Tests
{
    /// <summary>
    /// Builds real <c>.fyn</c> files for the tests that need one.
    /// </summary>
    /// <remarks>
    /// These tests deliberately go through the actual file: a graph is created on disk, saved by Graph
    /// Toolkit, imported by the Fynite importer and loaded back as a compiled asset. Anything that only
    /// worked because an object happened to still be in memory fails here.
    /// </remarks>
    internal static class TestGraphFactory
    {
        /// <summary>Folder the tests create their graphs in.</summary>
        internal const string Folder = "Assets/FyniteTestGraphs";

        /// <summary>Creates a unique path inside the test folder.</summary>
        internal static string NewPath(string name)
        {
            if (!Directory.Exists(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            return Folder + "/" + name + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + FyniteGraph.DottedExtension;
        }

        /// <summary>Deletes everything the tests created.</summary>
        internal static void CleanUp()
        {
            if (AssetDatabase.LoadMainAssetAtPath(Folder) != null || Directory.Exists(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// The reference graph, as a real file: Root with Idle and Moving, three signals, and reactions
        /// covering a guarded transition, a plain transition and one without a target.
        /// </summary>
        internal static FyniteGraph CreateReferenceGraph(string path)
        {
            var graph = FyniteGraphCreation.Create(path);

            FyniteNodeOptions.Set(
                graph.FindSettings(), FyniteConfigNode.ContextScriptOption, ScriptOf<GraphTestContext>());

            var root = graph.GetNodes().OfType<FyniteStateNode>().First();
            root.SetDisplayName("Root");
            FyniteNodeOptions.Set(root, FyniteStateNode.RootOption, true);

            var idle = AddState(graph, root, "Idle", new Vector2(320f, -120f), initial: true);
            var moving = AddState(graph, root, "Moving", new Vector2(320f, 120f), initial: false);

            AddBlock<FyniteTickActionNode, GraphTestTickAction>(root);
            AddBlock<FyniteFixedTickActionNode, GraphTestFixedAction>(root);

            AddConfiguredBlock<FyniteEnterActionNode>(idle, "label", "idleEnter");
            AddConfiguredBlock<FyniteExitActionNode>(idle, "label", "idleExit");

            var move = AddSignal(graph, "Move", new Vector2(-360f, 0f), FyniteSignalNode.PayloadKind.None);
            var stop = AddSignal(graph, "Stop", new Vector2(-360f, 120f), FyniteSignalNode.PayloadKind.None);
            var notify = AddSignal(graph, "Notify", new Vector2(-360f, 240f), FyniteSignalNode.PayloadKind.String);

            var toMoving = AddReaction(graph, idle, move, moving, priority: 0, order: 0, new Vector2(700f, -120f));
            AddBlock<FyniteGuardBlockNode, GraphTestGuard>(toMoving);
            AddConfiguredEffect(toMoving, "moveEffect");

            AddReaction(graph, moving, stop, idle, priority: 0, order: 1, new Vector2(700f, 120f));

            var onNotify = AddReaction(graph, moving, notify, null, priority: 0, order: 2, new Vector2(700f, 280f));
            AddBlock<FyniteEffectBlockNode, GraphTestPayloadAction>(onNotify);

            FyniteGraphCreation.SaveAndReimport(graph);
            return graph;
        }

        internal static FyniteStateNode AddState(
            FyniteGraph graph, FyniteStateNode parent, string name, Vector2 position, bool initial)
        {
            var state = new FyniteStateNode();
            graph.AddNode(state);
            state.SetDisplayName(name);
            state.Position = position;

            if (parent != null)
            {
                graph.Connect(
                    parent.GetOutputPortByName(FyniteStateNode.ChildrenPort),
                    state.GetInputPortByName(FyniteStateNode.ParentPort));
            }

            if (initial)
            {
                FyniteNodeOptions.Set(state, FyniteStateNode.InitialOption, true);
            }

            return state;
        }

        internal static FyniteSignalNode AddSignal(
            FyniteGraph graph, string name, Vector2 position, FyniteSignalNode.PayloadKind payload)
        {
            var signal = new FyniteSignalNode();
            graph.AddNode(signal);
            signal.SetDisplayName(name);
            signal.Position = position;

            if (payload != FyniteSignalNode.PayloadKind.None)
            {
                FyniteNodeOptions.Set(signal, FyniteSignalNode.PayloadKindOption, payload);
            }

            return signal;
        }

        internal static FyniteReactionNode AddReaction(
            FyniteGraph graph,
            FyniteStateNode source,
            FyniteSignalNode signal,
            FyniteStateNode target,
            int priority,
            int order,
            Vector2 position)
        {
            var reaction = new FyniteReactionNode();
            graph.AddNode(reaction);
            reaction.Position = position;

            graph.Connect(
                source.GetOutputPortByName(FyniteStateNode.ReactionsPort),
                reaction.GetInputPortByName(FyniteReactionNode.SourcePort));

            graph.Connect(
                signal.GetOutputPortByName(FyniteSignalNode.SignalPort),
                reaction.GetInputPortByName(FyniteReactionNode.SignalPort));

            if (target != null)
            {
                graph.Connect(
                    target.GetOutputPortByName(FyniteStateNode.IncomingPort),
                    reaction.GetInputPortByName(FyniteReactionNode.TargetPort));
            }

            FyniteNodeOptions.Set(reaction, FyniteReactionNode.PriorityOption, priority);
            FyniteNodeOptions.Set(reaction, FyniteReactionNode.OrderOption, order);
            return reaction;
        }

        internal static TNode AddBlock<TNode, TBlock>(ContextNode owner)
            where TNode : FyniteBlockNodeBase, new()
        {
            owner.CreateBlockNode<TNode>();
            var node = (TNode)owner.GetBlock(owner.BlockCount - 1);
            FyniteNodeOptions.Set(node, FyniteBlockNodeBase.BlockScriptOption, ScriptOf<TBlock>());
            return node;
        }

        internal static void AddConfiguredBlock<TNode>(ContextNode owner, string field, string value)
            where TNode : FyniteBlockNodeBase, new()
        {
            var node = typeof(TNode) == typeof(FyniteExitActionNode)
                ? AddBlock<TNode, GraphTestExitAction>(owner)
                : AddBlock<TNode, GraphTestEnterAction>(owner);

            // Configuration options only exist once the block type is known, so the node has to be
            // redefined before a value can be written into one.
            node.DefineNode();
            FyniteNodeOptions.Set(node, FyniteBlockNodeBase.ConfigOptionPrefix + field, value);
        }

        internal static void AddConfiguredEffect(ContextNode owner, string label)
        {
            var node = AddBlock<FyniteEffectBlockNode, GraphTestEffectAction>(owner);
            node.DefineNode();
            FyniteNodeOptions.Set(node, FyniteBlockNodeBase.ConfigOptionPrefix + "label", label);
        }

        /// <summary>Finds the script asset declaring a type, the way a block picker would.</summary>
        internal static MonoScript ScriptOf<T>()
        {
            var name = typeof(T).Name;

            foreach (var guid in AssetDatabase.FindAssets(name + " t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != name)
                {
                    continue;
                }

                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == typeof(T))
                {
                    return script;
                }
            }

            throw new InvalidOperationException("No script asset declares '" + typeof(T).FullName + "'.");
        }
    }
}
