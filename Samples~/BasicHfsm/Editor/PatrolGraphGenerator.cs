using Fynite.GraphEditor;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.Samples.BasicHfsm.Editor
{
    /// <summary>Rebuilds the tracked sample with Fynite's current public authoring operations.</summary>
    public static class PatrolGraphGenerator
    {
        public static FyniteGraph Generate(string assetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            var graph = FyniteGraphCreation.Create(assetPath);
            graph.SetContextType(typeof(PatrolContext));
            var root = graph.FindRoot();
            var idle = AddState(graph, root, "Idle", new Vector2(320f, -120f));
            var moving = AddState(graph, root, "Moving", new Vector2(320f, 120f));

            AddBlock<FyniteTickActionNode, AdvanceAction>(moving);
            AddBlock<FyniteFixedTickActionNode, FixedStepAction>(moving);
            AddConfiguredBlock<FyniteEnterActionNode, LogAction>(idle, "entered Idle");
            AddConfiguredBlock<FyniteExitActionNode, LogAction>(idle, "left Idle");
            AddConfiguredBlock<FyniteEnterActionNode, LogAction>(moving, "entered Moving");
            AddConfiguredBlock<FyniteExitActionNode, LogAction>(moving, "left Moving");

            var move = AddSignal(graph, "Move", new Vector2(-360f, 40f), FyniteSignalNode.PayloadKind.None);
            var stop = AddSignal(graph, "Stop", new Vector2(-360f, 160f), FyniteSignalNode.PayloadKind.None);
            var notify = AddSignal(graph, "Notify", new Vector2(-360f, 280f), FyniteSignalNode.PayloadKind.String);

            var toMoving = AddReaction(graph, idle, move, moving, 0, 0, new Vector2(700f, -120f));
            AddBlock<FyniteGuardBlockNode, CanMoveGuard>(toMoving);
            AddConfiguredBlock<FyniteEffectBlockNode, LogAction>(toMoving, "starting to move");
            AddReaction(graph, moving, stop, idle, 0, 1, new Vector2(700f, 120f));
            var onNotify = AddReaction(graph, moving, notify, null, 0, 2, new Vector2(700f, 300f));
            AddBlock<FyniteEffectBlockNode, NoteAction>(onNotify);

            FyniteGraphCreation.SaveAndReimport(graph);
            return graph;
        }

        private static FyniteStateNode AddState(
            FyniteGraph graph, IFyniteIdentifiedNode parent, string name, Vector2 position)
        {
            var state = FyniteStateOperations.CreateChild(graph, parent, position);
            FyniteStateOperations.Rename(graph, state, name);
            return state;
        }

        private static FyniteSignalNode AddSignal(
            FyniteGraph graph, string name, Vector2 position, FyniteSignalNode.PayloadKind payload)
        {
            var signal = new FyniteSignalNode();
            graph.AddNode(signal);
            signal.SetDisplayName(name);
            signal.SetPayload(payload);
            signal.Position = position;
            return signal;
        }

        private static FyniteReactionNode AddReaction(
            FyniteGraph graph, FyniteStateNode source, FyniteSignalNode signal, FyniteStateNode target,
            int priority, int order, Vector2 position)
        {
            var reaction = new FyniteReactionNode();
            graph.AddNode(reaction);
            reaction.Position = position;
            reaction.SetResolution(priority, order);
            graph.Connect(source.GetOutputPortByName(FyniteStateNode.ReactionsPort),
                reaction.GetInputPortByName(FyniteReactionNode.SourcePort));
            graph.Connect(signal.GetOutputPortByName(FyniteSignalNode.SignalPort),
                reaction.GetInputPortByName(FyniteReactionNode.SignalPort));
            if (target != null)
            {
                graph.Connect(target.GetOutputPortByName(FyniteStateNode.IncomingPort),
                    reaction.GetInputPortByName(FyniteReactionNode.TargetPort));
            }
            return reaction;
        }

        private static TNode AddBlock<TNode, TBlock>(ContextNode owner)
            where TNode : FyniteBlockNodeBase, new()
        {
            owner.CreateBlockNode<TNode>();
            var node = (TNode)owner.GetBlock(owner.BlockCount - 1);
            node.SetBlockScript(ScriptOf<TBlock>());
            return node;
        }

        private static void AddConfiguredBlock<TNode, TBlock>(ContextNode owner, string message)
            where TNode : FyniteBlockNodeBase, new()
        {
            var node = AddBlock<TNode, TBlock>(owner);
            if (!node.SetConfiguration("message", message))
            {
                throw new System.InvalidOperationException("The sample block has no message field.");
            }
        }

        private static MonoScript ScriptOf<T>()
        {
            foreach (var guid in AssetDatabase.FindAssets(typeof(T).Name + " t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == typeof(T)) return script;
            }
            throw new System.InvalidOperationException("No script asset declares " + typeof(T).FullName + ".");
        }
    }
}
