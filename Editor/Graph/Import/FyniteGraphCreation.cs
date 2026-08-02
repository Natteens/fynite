using System.IO;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// Creating and opening <c>.fyn</c> files.
    /// </summary>
    /// <remarks>
    /// Graph Toolkit already handles opening a graph on double-click and drives the whole save flow, so
    /// what is left is the create menu and a programmatic entry point the tests and the sample use.
    /// </remarks>
    public static class FyniteGraphCreation
    {
        [MenuItem("Assets/Create/Fynite/Fynite Graph", false, 80)]
        private static void CreateFromMenu()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<FyniteGraph>("New Fynite Graph");
        }

        /// <summary>
        /// Creates a graph file at a path, already carrying an identity, a schema version, a settings
        /// node and a root state.
        /// </summary>
        /// <param name="assetPath">Project-relative path ending in <c>.fyn</c>.</param>
        /// <returns>The created graph, still open for further edits.</returns>
        public static FyniteGraph Create(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new System.ArgumentException("A project-relative path is required.", nameof(assetPath));
            }

            if (!assetPath.EndsWith(FyniteGraph.DottedExtension, System.StringComparison.OrdinalIgnoreCase))
            {
                assetPath += FyniteGraph.DottedExtension;
            }

            var directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var graph = GraphDatabase.CreateGraph<FyniteGraph>(assetPath);
            if (graph == null)
            {
                return null;
            }

            graph.InitializeNewGraph();
            GraphDatabase.SaveGraph(graph);

            // The file was just rewritten with the initial content, so the compiled asset has to come
            // from that content rather than from the empty graph the create call first wrote.
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return graph;
        }

        /// <summary>Saves a graph and recompiles the asset it produces.</summary>
        public static void SaveAndReimport(FyniteGraph graph)
        {
            if (graph == null)
            {
                return;
            }

            var path = GraphDatabase.GetGraphAssetPath(graph);
            GraphDatabase.SaveGraph(graph);

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
