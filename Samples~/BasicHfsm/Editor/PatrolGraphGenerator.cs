using System.Linq;
using Fynite.GraphEditor;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.Samples.BasicHfsm.Editor
{
    /// <summary>
    /// Rebuilds the sample's <c>Patrol.fyn</c> from code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sample ships the generated <c>.fyn</c>, so nothing has to be run to try it. This exists so
    /// the file can be regenerated after an edit, and because it is a worked example of building a graph
    /// programmatically — the same calls the graph window makes when a user draws one by hand.
    /// </para>
    /// <para>
    /// It produces exactly the structure the sample documents: Root with Idle and Moving beneath it,
    /// three signals, and reactions covering a guarded transition, a plain transition and a reaction
    /// with no target at all.
    /// </para>
    /// </remarks>
    public static class PatrolGraphGenerator
    {
        private const string DefaultPath = "Assets/Samples/Fynite/BasicHfsm/Patrol.fyn";

        [MenuItem("Assets/Create/Fynite/Samples/Regenerate Patrol Sample Graph")]
        private static void RegenerateFromMenu() => Generate(DefaultPath);

        /// <summary>Creates the sample graph at a path, replacing whatever is there.</summary>
        public static FyniteGraph Generate(string assetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            var graph = FyniteGraphCreation.Create(assetPath);

            var settings = graph.FindSettings();
            FyniteNodeOptions.Set(settings, FyniteConfigNode.ContextScriptOption, ScriptOf<PatrolContext>());

            // --- states -------------------------------------------------------------------------
            var root = graph.GetNodes().OfType<FyniteStateNode>().First();
            root.SetDisplayName("Root");
            root.Position = new Vector2(0f, 0f);
            FyniteNodeOptions.Set(root, FyniteStateNode.RootOption, true);

            var idle = AddState(graph, "Idle", new Vector2(320f, -120f), root, initial: true);
            var moving = AddState(graph, "Moving", new Vector2(320f, 120f), root, initial: false);

            // --- blocks -------------------------------------------------------------------------
            AddBlock<FyniteTickActionNode, AdvanceAction>(moving);
            AddBlock<FyniteFixedTickActionNode, FixedStepAction>(moving);

            AddLogBlock<FyniteEnterActionNode>(idle, "entered Idle");
            AddLogBlock<FyniteExitActionNode>(idle, "left Idle");
            AddLogBlock<FyniteEnterActionNode>(moving, "entered Moving");
            AddLogBlock<FyniteExitActionNode>(moving, "left Moving");

            // --- signals ------------------------------------------------------------------------
            var move = AddSignal(graph, "Move", new Vector2(-360f, 40f), FyniteSignalNode.PayloadKind.None);
            var stop = AddSignal(graph, "Stop", new Vector2(-360f, 160f), FyniteSignalNode.PayloadKind.None);
            var notify = AddSignal(graph, "Notify", new Vector2(-360f, 280f), FyniteSignalNode.PayloadKind.String);

            // --- reactions ----------------------------------------------------------------------
            var toMoving = AddReaction(graph, idle, move, moving, priority: 0, order: 0, new Vector2(700f, -120f));
            AddBlock<FyniteGuardBlockNode, CanMoveGuard>(toMoving);
            AddLogBlock<FyniteEffectBlockNode>(toMoving, "starting to move");

            AddReaction(graph, moving, stop, idle, priority: 0, order: 1, new Vector2(700f, 120f));

            // No target: the effect runs and the active path is left exactly as it was.
            var onNotify = AddReaction(graph, moving, notify, null, priority: 0, order: 2, new Vector2(700f, 300f));
            AddBlock<FyniteEffectBlockNode, NoteAction>(onNotify);

            FyniteGraphCreation.SaveAndReimport(graph);
            return graph;
        }

        private static FyniteStateNode AddState(FyniteGraph graph, string name, Vector2 position, FyniteStateNode parent, bool initial)
        {
            var state = new FyniteStateNode();
            graph.AddNode(state);
            state.SetDisplayName(name);
            state.Position = position;

            graph.Connect(
                parent.GetOutputPortByName(FyniteStateNode.ChildrenPort),
                state.GetInputPortByName(FyniteStateNode.ParentPort));

            if (initial)
            {
                FyniteNodeOptions.Set(state, FyniteStateNode.InitialOption, true);
            }

            return state;
        }

        private static FyniteSignalNode AddSignal(FyniteGraph graph, string name, Vector2 position, FyniteSignalNode.PayloadKind payload)
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

        private static FyniteReactionNode AddReaction(
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

        private static TBlockNode AddBlock<TBlockNode, TBlock>(ContextNode owner)
            where TBlockNode : FyniteBlockNodeBase, new()
        {
            owner.CreateBlockNode<TBlockNode>();
            var node = (TBlockNode)owner.GetBlock(owner.BlockCount - 1);
            FyniteNodeOptions.Set(node, FyniteBlockNodeBase.BlockScriptOption, ScriptOf<TBlock>());
            return node;
        }

        private static void AddLogBlock<TBlockNode>(ContextNode owner, string message)
            where TBlockNode : FyniteBlockNodeBase, new()
        {
            var node = AddBlock<TBlockNode, LogAction>(owner);

            // The configuration options only exist once the block type is known, so the node has to be
            // redefined before the configured message can be written into it.
            node.DefineNode();
            FyniteNodeOptions.Set(node, FyniteBlockNodeBase.ConfigOptionPrefix + "message", message);
        }

        private static MonoScript ScriptOf<T>()
        {
            var name = typeof(T).Name;

            foreach (var guid in AssetDatabase.FindAssets(name + " t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != name)
                {
                    continue;
                }

                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == typeof(T))
                {
                    return script;
                }
            }

            throw new System.InvalidOperationException(
                "No script asset declares '" + typeof(T).FullName + "'. A block type is selected through its " +
                "script, so it has to live in a file named after the class.");
        }
    }
}
