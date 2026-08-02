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
    /// Findings that belong to a block are raised on the block node itself, and findings about the graph
    /// as a whole land on the settings node so they are visible on the canvas rather than only in a log.
    /// </para>
    /// </remarks>
    public static class FyniteGraphValidation
    {
        /// <summary>Validates a graph and reports through Graph Toolkit's logger.</summary>
        public static void Report(FyniteGraph graph, GraphLogger logger)
        {
            var document = FyniteGraphProjection.ToDocument(graph);
            var result = FyniteGraphCompiler.Compile(document);

            var byGuid = IndexNodes(graph);
            var settings = graph.FindSettings();

            ReportStructuralProblems(graph, settings, logger);

            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                var diagnostic = result.Diagnostics[i];
                object context = null;

                if (!string.IsNullOrEmpty(diagnostic.ElementGuid))
                {
                    byGuid.TryGetValue(diagnostic.ElementGuid, out context);
                }

                context = context ?? settings;

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
        }

        /// <summary>
        /// Reports the problems that only exist at the visual layer, which the document cannot express.
        /// </summary>
        private static void ReportStructuralProblems(FyniteGraph graph, FyniteConfigNode settings, GraphLogger logger)
        {
            FyniteConfigNode first = null;
            int settingsCount = 0;

            foreach (var node in graph.GetNodes())
            {
                if (node is FyniteConfigNode config)
                {
                    settingsCount++;
                    if (first == null)
                    {
                        first = config;
                    }
                    else
                    {
                        logger.LogError(
                            "A graph has one settings node. Delete this one and use '" + first.Title + "'.",
                            config);
                    }

                    if (config.HasUnresolvedContextScript())
                    {
                        logger.LogError(
                            "The selected context script declares no type Unity can load. A script defines a " +
                            "type only when the file is named after the class it contains.",
                            config);
                    }
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

            if (settingsCount == 0)
            {
                logger.LogError(
                    "This graph has no settings node, so it declares no context type and cannot compile. " +
                    "Add one from the create menu under Fynite / Graph Settings.");
            }
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
