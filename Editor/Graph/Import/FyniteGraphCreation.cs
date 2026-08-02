using System.IO;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
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
            // Graph Toolkit's prompt creates and saves an empty graph directly. That bypasses
            // InitializeNewGraph(), so the menu used to produce a different file from Create(). Keep
            // Unity's familiar in-project rename interaction, but route the completed name through the
            // same public factory every other caller uses.
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                EntityId.None,
                ScriptableObject.CreateInstance<CreateFyniteGraphEndNameEditAction>(),
                "New Fynite Graph" + FyniteGraph.DottedExtension,
                EditorGUIUtility.IconContent("ScriptableObject Icon").image as Texture2D,
                null);
        }

        /// <summary>
        /// Creates a graph file at a path, already carrying an identity, the current schema and one
        /// structural root.
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

            FyniteGraph graph = null;
            AssetDatabase.StartAssetEditing();
            try
            {
                // Defer Asset Database observation until the Graph Toolkit container already carries
                // its required metadata and Root. Without this scope CreateGraph imports the empty
                // container once before InitializeNewGraph gets a chance to run.
                graph = GraphDatabase.CreateGraph<FyniteGraph>(assetPath);
                if (graph != null)
                {
                    graph.InitializeNewGraph();
                    GraphDatabase.SaveGraph(graph);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (graph == null)
            {
                return null;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return graph;
        }

        /// <summary>Finishes the Assets/Create rename operation through <see cref="Create"/>.</summary>
        private sealed class CreateFyniteGraphEndNameEditAction : AssetCreationEndAction
        {
            public override void Action(EntityId entityId, string pathName, string resourceFile)
            {
                var graph = Create(pathName);
                if (graph != null)
                {
                    ProjectWindowUtil.ShowCreatedAsset(AssetDatabase.LoadMainAssetAtPath(pathName));
                }
            }
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
