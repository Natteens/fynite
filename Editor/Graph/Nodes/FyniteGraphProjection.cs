using System;
using System.Collections.Generic;
using Fynite.Authoring;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// Reads a Graph Toolkit graph into the authoring document the compiler consumes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the only bridge between the visual layer and everything behind it. Above it, nodes,
    /// ports and wires; below it, a plain document of GUIDs and lists. Keeping the seam here is what
    /// lets the compiler and all of its validation run in tests that never open a window.
    /// </para>
    /// <para>
    /// Positions are carried across because the document is meant to describe the graph completely, but
    /// the compiler never reads them — ordering comes from the hierarchy walk and from explicit order
    /// fields, so a node's coordinates cannot change what the machine does.
    /// </para>
    /// </remarks>
    public static class FyniteGraphProjection
    {
        /// <summary>Projects a graph into a document.</summary>
        public static FyniteGraphDocument ToDocument(FyniteGraph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var document = new FyniteGraphDocument
            {
                schemaVersion = graph.SchemaVersion > 0 ? graph.SchemaVersion : FyniteGraphDocument.CurrentSchemaVersion,
                graphGuid = graph.GraphGuid,
                contextType = FyniteTypeReference.From(graph.ResolveContextType())
            };

            var reactionNodes = new List<FyniteReactionNode>();

            foreach (var node in graph.GetNodes())
            {
                switch (node)
                {
                    case FyniteStateNode state:
                        document.states.Add(ProjectState(state));
                        break;
                    case FyniteSignalNode signal:
                        document.signals.Add(ProjectSignal(signal));
                        break;
                    case FyniteReactionNode reaction:
                        reactionNodes.Add(reaction);
                        break;
                }
            }

            // Reactions are emitted after every state and signal exists, so their wires resolve to
            // identities that are already in the document.
            for (int i = 0; i < reactionNodes.Count; i++)
            {
                document.reactions.Add(ProjectReaction(reactionNodes[i]));
            }

            return document;
        }

        private static FyniteStateDocument ProjectState(FyniteStateNode node)
        {
            var parent = node.ResolveParent();

            var state = new FyniteStateDocument
            {
                guid = node.FyniteGuid,
                name = node.StateName,
                position = node.Position,
                parentGuid = parent?.FyniteGuid,
                isRoot = node.IsRoot,
                isInitial = node.IsInitial
            };

            ProjectBlocks(node.BlocksOfPhase(FynitePhase.Enter), state.enter);
            ProjectBlocks(node.BlocksOfPhase(FynitePhase.Tick), state.tick);
            ProjectBlocks(node.BlocksOfPhase(FynitePhase.FixedTick), state.fixedTick);
            ProjectBlocks(node.BlocksOfPhase(FynitePhase.Exit), state.exit);

            return state;
        }

        private static FyniteSignalDocument ProjectSignal(FyniteSignalNode node) =>
            new FyniteSignalDocument
            {
                guid = node.FyniteGuid,
                name = node.SignalName,
                position = node.Position,
                payloadType = FyniteTypeReference.From(node.ResolvePayloadType())
            };

        private static FyniteReactionDocument ProjectReaction(FyniteReactionNode node)
        {
            var source = node.ResolveSource();
            var target = node.ResolveTarget();
            var signal = node.ResolveSignal();

            var reaction = new FyniteReactionDocument
            {
                guid = node.FyniteGuid,
                sourceGuid = source?.FyniteGuid,
                signalGuid = signal?.FyniteGuid,
                targetGuid = target?.FyniteGuid,
                position = node.Position,
                priority = node.Priority,
                order = node.Order
            };

            ProjectBlocks(node.ResolveGuards(), reaction.guards);
            ProjectBlocks(node.ResolveEffects(), reaction.effects);

            return reaction;
        }

        private static void ProjectBlocks<T>(List<T> nodes, List<FyniteBlockDocument> target)
            where T : FyniteBlockNodeBase
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var blockType = node.ResolveBlockType();

                target.Add(new FyniteBlockDocument
                {
                    guid = node.FyniteGuid,
                    blockType = FyniteTypeReference.From(blockType),
                    configJson = blockType != null ? node.ReadConfiguration(blockType) : null
                });
            }
        }
    }
}
