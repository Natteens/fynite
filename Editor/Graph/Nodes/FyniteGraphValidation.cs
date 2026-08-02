using System.Collections.Generic;
using Fynite.Authoring;
using Unity.GraphToolkit.Editor;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// Runs the compiler's validation over a live graph and puts each finding on the node that caused it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no second set of rules here. The graph is projected into a document, the real compiler
    /// validates it, and the resulting diagnostics are mapped back from element GUIDs to the nodes they
    /// came from. What the user sees while editing is therefore exactly what the importer will decide.
    /// </para>
    /// <para>
    /// Findings that belong to a block are raised on the block node itself. Findings about the graph as a
    /// whole — a missing context type, a schema that cannot be read — are raised on the graph, because
    /// that is where the thing they are about actually lives now: attaching them to some node would point
    /// the user at a node they cannot fix them from.
    /// </para>
    /// </remarks>
    public static class FyniteGraphValidation
    {
        /// <summary>Validates a graph and reports through Graph Toolkit's logger.</summary>
        public static void Report(FyniteGraph graph, GraphLogger logger) =>
            Report(graph, logger, FyniteGraphMigrationResult.None);

        /// <summary>Validates a graph, also reporting what a migration pass had to say.</summary>
        public static void Report(FyniteGraph graph, GraphLogger logger, FyniteGraphMigrationResult migration)
        {
            var byGuid = IndexNodes(graph);

            if (migration != null)
            {
                for (int i = 0; i < migration.Diagnostics.Count; i++)
                {
                    Emit(migration.Diagnostics[i], byGuid, logger);
                }
            }

            ReportStructuralProblems(graph, logger);

            var document = FyniteGraphProjection.ToDocument(graph);
            var result = FyniteGraphCompiler.Compile(document);

            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                Emit(result.Diagnostics[i], byGuid, logger);
            }
        }

        private static void Emit(FyniteDiagnostic diagnostic, Dictionary<string, object> byGuid, GraphLogger logger)
        {
            object context = null;

            if (!string.IsNullOrEmpty(diagnostic.ElementGuid))
            {
                byGuid.TryGetValue(diagnostic.ElementGuid, out context);
            }

            var message = diagnostic.Code + ": " + diagnostic.Message;

            if (diagnostic.IsError)
            {
                logger.LogError(message, context);
            }
            else
            {
                logger.LogWarning(message, context);
            }
        }

        /// <summary>
        /// Reports the problems that only exist at the visual layer, which the document cannot express.
        /// </summary>
        private static void ReportStructuralProblems(FyniteGraph graph, GraphLogger logger)
        {
            FyniteRootStateNode firstRoot = null;

            foreach (var node in graph.GetNodes())
            {
                if (node is FyniteRootStateNode root)
                {
                    // The compiler reports two roots as well, from the document. Saying it here too is
                    // what puts the message on the extra node rather than only on the graph, so the user
                    // can see which one to delete.
                    if (firstRoot == null)
                    {
                        firstRoot = root;
                    }
                    else
                    {
                        logger.LogError(
                            "A machine has one root. Delete this node and connect its children to the " +
                            "other root instead.",
                            root);
                    }
                }

                if (node is FyniteConfigNode legacy)
                {
                    logger.LogError(
                        "This is a settings node from an older Fynite. The context type is now a property " +
                        "of the graph, edited in the .fyn inspector. Set it there and delete this node.",
                        legacy);
                }

                if (node is FyniteSignalNode signal && signal.HasUnresolvedScriptPayload())
                {
                    logger.LogError(
                        "This signal is set to carry a scripted payload but the selected script declares no " +
                        "usable type. Pick a script named after the class it contains, or change the payload " +
                        "kind.",
                        signal);
                }

                if (node is FyniteBlockNodeBase || !(node is ContextNode container))
                {
                    continue;
                }

                foreach (var block in container.BlockNodes)
                {
                    if (block is FyniteBlockNodeBase fyniteBlock && fyniteBlock.ResolveBlockType() == null)
                    {
                        logger.LogError(
                            "This block has no script selected, or the selected script declares no type. " +
                            "Nothing will run until one is chosen.",
                            block);
                    }
                }
            }

            // Root absence is reported once by the compiler as FYN0401. Reporting it here as well used
            // to turn one structural defect into an unaddressed message plus the canonical diagnostic.
        }

        private static Dictionary<string, object> IndexNodes(FyniteGraph graph)
        {
            var byGuid = new Dictionary<string, object>();

            foreach (var node in graph.GetNodes())
            {
                if (node is IFyniteIdentifiedNode identified && !string.IsNullOrEmpty(identified.FyniteGuid))
                {
                    byGuid[identified.FyniteGuid] = node;
                }

                if (!(node is ContextNode container))
                {
                    continue;
                }

                foreach (var block in container.BlockNodes)
                {
                    if (block is IFyniteIdentifiedNode identifiedBlock && !string.IsNullOrEmpty(identifiedBlock.FyniteGuid))
                    {
                        byGuid[identifiedBlock.FyniteGuid] = block;
                    }
                }
            }

            return byGuid;
        }
    }
}
