using System;
using System.IO;
using Fynite.Authoring;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// Compiles a <c>.fyn</c> file into the runtime asset a scene can reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Graph Toolkit registers the extension and owns the file's container format, but leaves the import
    /// to whoever wants it, so Fynite takes it. The compiled <see cref="FyniteGraphAsset"/> becomes the
    /// file's main object, which is what lets a user drag the <c>.fyn</c> itself onto a
    /// <see cref="FyniteRunner"/> and have Unity resolve the runtime asset with no custom drag handling.
    /// </para>
    /// <para>
    /// Objects are added with fixed identifier strings rather than generated ones, so the local file IDs
    /// stay the same across reimports and references from scenes and prefabs survive every recompile.
    /// </para>
    /// <para>
    /// A failed compile still produces an asset — an empty one carrying the error. That is deliberate:
    /// leaving the previous graph in place would let a broken edit keep running while the console says
    /// otherwise, and leaving no asset at all would break every reference in the project.
    /// </para>
    /// </remarks>
    [ScriptedImporter(ImporterVersion, FyniteGraph.Extension)]
    public sealed class FyniteGraphImporter : ScriptedImporter
    {
        /// <summary>
        /// Bumping this reimports every graph in the project, and must be bumped whenever a change to
        /// the compiler alters what a given file compiles to.
        /// </summary>
        private const int ImporterVersion = 5;

        /// <summary>Identifier of the compiled asset inside the imported file. Never change it.</summary>
        public const string MainObjectIdentifier = "FyniteGraphAsset";

        /// <inheritdoc />
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var asset = ScriptableObject.CreateInstance<FyniteGraphAsset>();
            asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

            // A stable identifier keeps the local file ID constant, so scene and prefab references to
            // this asset survive reimports instead of breaking on every recompile.
            ctx.AddObjectToAsset(MainObjectIdentifier, asset);
            ctx.SetMainObject(asset);

            FyniteGraph graph;
            try
            {
                graph = GraphDatabase.LoadGraphForImporter<FyniteGraph>(ctx.assetPath);
            }
            catch (Exception exception)
            {
                Fail(ctx, asset, null, 0, "The graph file could not be read: " + exception.Message);
                return;
            }

            if (graph == null)
            {
                Fail(ctx, asset, null, 0, "The graph file could not be read as a Fynite graph.");
                return;
            }

            FyniteGraphDocument document;
            try
            {
                document = FyniteGraphProjection.ToDocument(graph);
            }
            catch (Exception exception)
            {
                Fail(ctx, asset, graph.GraphGuid, graph.SchemaVersion,
                    "The graph could not be read into an authoring model: " + exception.Message);
                return;
            }

            // A file that was just created, or whose context has not been chosen yet, is a normal step on
            // the way to a graph rather than a mistake. It still produces a deliberately non-runnable
            // asset carrying the reason, but it does not fill the console with import errors for a file
            // the user is halfway through setting up. Every other problem is reported below.
            if (IsUnauthored(document))
            {
                asset.SetCompilationFailed(
                    "This graph is not ready to run yet: open it and give it a root state and a context type.",
                    document.graphGuid,
                    document.schemaVersion);
                return;
            }

            var result = FyniteGraphCompiler.Compile(document);

            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                var diagnostic = result.Diagnostics[i];
                var message = Describe(diagnostic);

                if (diagnostic.IsError)
                {
                    ctx.LogImportError(message, asset);
                }
                else
                {
                    ctx.LogImportWarning(message, asset);
                }
            }

            if (!result.Succeeded)
            {
                asset.SetCompilationFailed(result.DescribeErrors(), document.graphGuid, document.schemaVersion);
                return;
            }

            asset.SetCompiled(result.Graph, result.Binder, result.Blocks, result.Payloads, result.SchemaVersion);

            // The compiled asset depends on the shape of the block and context types, not only on the
            // file. Unity reimports on script recompilation anyway, and the importer version above is
            // what forces a rebuild when the compiler itself changes.
            ctx.DependsOnCustomDependency(FyniteImportDependencies.ScriptsDependency);
        }

        /// <summary>True when the graph has not been set up far enough to be worth compiling.</summary>
        private static bool IsUnauthored(FyniteGraphDocument document) =>
            !document.contextType.IsSet ||
            (document.states.Count == 0 && document.signals.Count == 0 && document.reactions.Count == 0);

        private static void Fail(AssetImportContext ctx, FyniteGraphAsset asset, string graphGuid, int schemaVersion, string message)
        {
            asset.SetCompilationFailed(message, graphGuid, schemaVersion);
            ctx.LogImportError(message, asset);
        }

        private static string Describe(FyniteDiagnostic diagnostic)
        {
            var where = string.IsNullOrEmpty(diagnostic.ElementName)
                ? diagnostic.ElementKind
                : diagnostic.ElementKind + " '" + diagnostic.ElementName + "'";

            var location = string.IsNullOrEmpty(where) ? string.Empty : " [" + where + "]";
            var identity = string.IsNullOrEmpty(diagnostic.ElementGuid) ? string.Empty : " (" + diagnostic.ElementGuid + ")";

            return "Fynite " + diagnostic.Code + location + identity + ": " + diagnostic.Message;
        }
    }
}
