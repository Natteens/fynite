using System;
using System.Collections.Generic;
using Fynite.Authoring;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>What a migration pass found and did.</summary>
    public sealed class FyniteGraphMigrationResult
    {
        /// <summary>Everything the pass wants to tell the user about.</summary>
        public List<FyniteDiagnostic> Diagnostics { get; } = new List<FyniteDiagnostic>();

        /// <summary>True when the graph was rewritten and should be saved to make that stick.</summary>
        public bool Changed { get; internal set; }

        /// <summary>True when something was found that migration refused to resolve on its own.</summary>
        public bool Blocked { get; internal set; }

        /// <summary>Nothing to migrate.</summary>
        public static FyniteGraphMigrationResult None => new FyniteGraphMigrationResult();
    }

    /// <summary>
    /// Brings a graph written by an older Fynite up to the current arrangement of nodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things moved. The context type used to live in a settings node on the canvas and now lives on
    /// the graph; the root used to be an ordinary state with a tick box and is now its own node kind.
    /// Neither change can be expressed in the authoring document — both are about which nodes exist —
    /// so neither belongs in <see cref="FyniteSchemaMigrator"/>. This is the only place that can see
    /// nodes, so this is where it happens.
    /// </para>
    /// <para>
    /// Every step is idempotent and every step is conservative. Nothing is deleted before what it held
    /// has been carried somewhere else, and anything ambiguous is reported and left exactly as it is
    /// rather than resolved by guessing — a graph that migration refuses to touch still holds all of its
    /// data, which is not true of one that picked wrong.
    /// </para>
    /// <para>
    /// Running it does not write the file. The importer runs it on the copy it loaded so that an old
    /// graph compiles correctly without its file being rewritten behind the user's back; the graph
    /// window runs it too, and there the result is persisted by the next save like any other edit.
    /// </para>
    /// </remarks>
    public static class FyniteGraphMigration
    {
        /// <summary>Migrates a graph in memory.</summary>
        public static FyniteGraphMigrationResult Apply(FyniteGraph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var result = new FyniteGraphMigrationResult();

            MigrateContext(graph, result);
            MigrateRoot(graph, result);

            return result;
        }

        // --- context ------------------------------------------------------------------------------

        private static void MigrateContext(FyniteGraph graph, FyniteGraphMigrationResult result)
        {
            var legacy = new List<FyniteConfigNode>();

            foreach (var node in graph.GetNodes())
            {
                if (node is FyniteConfigNode settings)
                {
                    legacy.Add(settings);
                }
            }

            if (legacy.Count == 0)
            {
                return;
            }

            // A script that no longer declares a usable type cannot have its selection carried anywhere,
            // so the node stays put. Losing it would turn "the context type is this missing script" into
            // "there never was one", which reads like the user forgot rather than like something broke.
            for (int i = 0; i < legacy.Count; i++)
            {
                if (!legacy[i].HasUnresolvedContextScript())
                {
                    continue;
                }

                result.Blocked = true;
                result.Diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.LegacyContextConflict,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Context,
                    "This graph still carries a settings node whose context script declares no usable type, " +
                    "so the type it meant cannot be recovered. The node has been left in place with its " +
                    "selection intact; set the graph's Context Type in the .fyn inspector and then delete it.",
                    legacy[i].FyniteGuid,
                    "settings",
                    "legacy settings node"));
                return;
            }

            var selected = new List<Type>();

            for (int i = 0; i < legacy.Count; i++)
            {
                var type = legacy[i].ResolveContextType();
                if (type != null && !selected.Contains(type))
                {
                    selected.Add(type);
                }
            }

            if (selected.Count > 1)
            {
                result.Blocked = true;
                result.Diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.LegacyContextConflict,
                    FyniteDiagnosticSeverity.Error,
                    FyniteDiagnosticCategory.Context,
                    "This graph carries " + legacy.Count + " settings nodes and they disagree about the " +
                    "context type (" + Describe(selected) + "). Picking one would silently change what the " +
                    "graph compiles against, so nothing has been changed: choose the Context Type in the " +
                    ".fyn inspector and delete the settings nodes."));
                return;
            }

            if (selected.Count == 1 && !graph.ContextTypeReference.IsSet)
            {
                graph.SetContextType(selected[0]);
                result.Changed = true;
                result.Diagnostics.Add(new FyniteDiagnostic(
                    FyniteDiagnosticCodes.LegacyContextMigrated,
                    FyniteDiagnosticSeverity.Warning,
                    FyniteDiagnosticCategory.Context,
                    "The context type '" + selected[0].FullName + "' was moved out of this graph's settings " +
                    "node and onto the graph itself, where it is edited in the .fyn inspector. Save the " +
                    "graph to write the change to disk."));
            }

            // Only now, with the value either carried over or already present, is the node redundant.
            for (int i = 0; i < legacy.Count; i++)
            {
                graph.RemoveNode(legacy[i]);
                result.Changed = true;
            }
        }

        private static string Describe(List<Type> types)
        {
            var text = new System.Text.StringBuilder();

            for (int i = 0; i < types.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }

                text.Append('\'').Append(types[i].FullName).Append('\'');
            }

            return text.ToString();
        }

        // --- root ---------------------------------------------------------------------------------

        private static void MigrateRoot(FyniteGraph graph, FyniteGraphMigrationResult result)
        {
            // A graph that already has a structural root has been through this, so there is nothing to
            // decide. This is what makes running the migration twice a no-op.
            if (graph.FindRoot() != null)
            {
                return;
            }

            var candidates = FindLegacyRoots(graph);

            if (candidates.Count == 0)
            {
                return;
            }

            if (candidates.Count > 1)
            {
                result.Blocked = true;

                for (int i = 0; i < candidates.Count; i++)
                {
                    result.Diagnostics.Add(new FyniteDiagnostic(
                        FyniteDiagnosticCodes.LegacyRootAmbiguous,
                        FyniteDiagnosticSeverity.Error,
                        FyniteDiagnosticCategory.Hierarchy,
                        "This graph declares " + candidates.Count + " roots, so which one becomes the " +
                        "machine's root cannot be decided here. Nothing has been changed and no state has " +
                        "been deleted: add a Root node, parent the states that belong under it, and the " +
                        "rest will follow.",
                        candidates[i].FyniteGuid,
                        candidates[i].StateName,
                        "state"));
                }

                return;
            }

            var legacyRoot = candidates[0];

            if (legacyRoot.HasExecutableContent())
            {
                PreserveBehaviourUnderNewRoot(graph, legacyRoot, result);
            }
            else
            {
                ConvertToStructuralRoot(graph, legacyRoot, result);
            }
        }

        /// <summary>
        /// Finds the states an older graph treated as its root.
        /// </summary>
        /// <remarks>
        /// The <c>Is Root</c> flag is asked for first, but it is no longer a declared option, so Graph
        /// Toolkit drops it as soon as it redefines the node and it is usually already gone by the time
        /// anything can read it. What is always true of a legacy root is that it is the one state with
        /// nothing connected to its Parent port — the compiler refused any other parentless state as an
        /// orphan, so a graph that ever compiled has exactly one. That is the signal actually relied on,
        /// and a graph with several is reported rather than guessed at.
        /// </remarks>
        private static List<FyniteStateNode> FindLegacyRoots(FyniteGraph graph)
        {
            var flagged = new List<FyniteStateNode>();
            var parentless = new List<FyniteStateNode>();

            foreach (var node in graph.GetNodes())
            {
                if (!(node is FyniteStateNode state))
                {
                    continue;
                }

                if (state.TryReadLegacyRootFlag(out bool isRoot) && isRoot)
                {
                    flagged.Add(state);
                }

                if (state.ResolveParent() == null)
                {
                    parentless.Add(state);
                }
            }

            return flagged.Count > 0 ? flagged : parentless;
        }

        /// <summary>
        /// Replaces a legacy root that ran nothing with a structural root, keeping its identity.
        /// </summary>
        private static void ConvertToStructuralRoot(
            FyniteGraph graph, FyniteStateNode legacyRoot, FyniteGraphMigrationResult result)
        {
            var identity = legacyRoot.FyniteGuid;
            var position = legacyRoot.Position;
            var children = legacyRoot.ResolveChildren();

            var root = new FyniteRootStateNode();
            graph.AddNode(root);
            root.AdoptFyniteGuid(identity);
            root.Position = position;

            // The children are rewired before the old node goes, so nothing is ever detached from the
            // hierarchy at a moment when the graph could be read.
            for (int i = 0; i < children.Count; i++)
            {
                graph.Disconnect(
                    legacyRoot.GetOutputPortByName(FyniteStateNode.ChildrenPort),
                    children[i].GetInputPortByName(FyniteStateNode.ParentPort));
                graph.Connect(
                    root.GetOutputPortByName(FyniteRootStateNode.ChildrenPort),
                    children[i].GetInputPortByName(FyniteStateNode.ParentPort));
            }

            graph.RemoveNode(legacyRoot);

            result.Changed = true;
            result.Diagnostics.Add(new FyniteDiagnostic(
                FyniteDiagnosticCodes.LegacyRootConverted,
                FyniteDiagnosticSeverity.Warning,
                FyniteDiagnosticCategory.Hierarchy,
                "The root state was replaced by a Root node, keeping its identity, its position and its " +
                "children. Save the graph to write the change to disk.",
                identity,
                FyniteRootStateNode.RootName,
                "state"));
        }

        /// <summary>
        /// Keeps a legacy root that ran something, and puts a structural root above it.
        /// </summary>
        /// <remarks>
        /// The old node is not rebuilt, not copied and not renamed — it keeps its identity, its blocks,
        /// its reactions and its own children, and simply gains a parent. Marking it as that parent's
        /// initial child is what keeps it entered as soon as the machine starts, which is what being the
        /// root meant.
        /// </remarks>
        private static void PreserveBehaviourUnderNewRoot(
            FyniteGraph graph, FyniteStateNode legacyRoot, FyniteGraphMigrationResult result)
        {
            var root = new FyniteRootStateNode();
            graph.AddNode(root);
            root.Position = legacyRoot.Position + new Vector2(-320f, 0f);

            graph.Connect(
                root.GetOutputPortByName(FyniteRootStateNode.ChildrenPort),
                legacyRoot.GetInputPortByName(FyniteStateNode.ParentPort));

            bool marked = FyniteNodeOptions.TrySet(legacyRoot, FyniteStateNode.InitialOption, true);

            result.Changed = true;
            result.Diagnostics.Add(new FyniteDiagnostic(
                FyniteDiagnosticCodes.LegacyRootPreserved,
                FyniteDiagnosticSeverity.Warning,
                FyniteDiagnosticCategory.Hierarchy,
                marked
                    ? "The root state '" + legacyRoot.StateName + "' runs blocks or reactions, which a Root " +
                      "node does not. It has been kept exactly as it was, with its identity, and a Root node " +
                      "was added above it as its parent; it is now that root's initial child, so it is still " +
                      "entered when the machine starts. Save the graph to write the change to disk."
                    : "The root state '" + legacyRoot.StateName + "' runs blocks or reactions, which a Root " +
                      "node does not. It has been kept as it was and a Root node was added above it, but it " +
                      "could not be marked as the initial child — tick 'Is Initial Child' on it so the " +
                      "machine still enters it when it starts.",
                legacyRoot.FyniteGuid,
                legacyRoot.StateName,
                "state"));

            if (!marked)
            {
                result.Blocked = true;
            }
        }
    }
}
