using UnityEditor;
using UnityEngine;

namespace Fynite.Editor
{
    /// <summary>
    /// Draws what a compiled graph turned out to be, read-only.
    /// </summary>
    /// <remarks>
    /// The first question this has to answer is whether the asset is safe to use. A graph that failed to
    /// compile is left deliberately empty by the importer, and saying so plainly is what stops someone
    /// assuming the previous version is still running. It is shared by the <c>.fyn</c> inspector and the
    /// compiled asset's own inspector so both tell the same story.
    /// </remarks>
    public static class FyniteGraphAssetSummary
    {
        /// <summary>Draws the summary for an asset, which may be null or may have failed to compile.</summary>
        public static void Draw(FyniteGraphAsset asset)
        {
            if (asset == null)
            {
                EditorGUILayout.HelpBox("This graph has not produced a compiled asset.", MessageType.Warning);
                return;
            }

            if (asset.CompilationError != null)
            {
                EditorGUILayout.HelpBox(
                    "This graph does not compile, so it holds no machine and any runner using it will refuse " +
                    "to start.\n\n" + asset.CompilationError,
                    MessageType.Error);

                EditorGUILayout.Space();
                DrawIdentity(asset);
                return;
            }

            if (!asset.IsExecutable)
            {
                EditorGUILayout.HelpBox("This asset holds no compiled graph yet.", MessageType.Info);
                return;
            }

            IFyniteDefinition definition;
            try
            {
                definition = asset.Definition;
            }
            catch (System.Exception exception)
            {
                EditorGUILayout.HelpBox(
                    "The compiled data could not be built into a machine:\n" + exception.Message,
                    MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Compiled graph", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField(
                    "Expected context",
                    asset.ContextType != null ? asset.ContextType.FullName : "<unknown>");
                EditorGUILayout.LabelField("Root", definition.GetStateName(definition.Root));
                EditorGUILayout.LabelField("States", definition.StateCount.ToString());
                EditorGUILayout.LabelField("Signals", definition.SignalCount.ToString());
                EditorGUILayout.LabelField("Depth", definition.MaxDepth.ToString());
            }

            DrawIdentity(asset);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Signals", EditorStyles.boldLabel);

            var signals = asset.Signals;
            if (signals.Count == 0)
            {
                EditorGUILayout.LabelField("  <none declared>");
            }

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < signals.Count; i++)
                {
                    var payload = definition.GetPayloadType(definition.GetSignal(i));
                    EditorGUILayout.LabelField("  " + signals[i].name, payload != null ? payload.Name : "no payload");
                }
            }
        }

        private static void DrawIdentity(FyniteGraphAsset asset)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField("Schema version", asset.SchemaVersion.ToString());
                EditorGUILayout.LabelField("Graph identity", asset.GraphGuid ?? "<none>");
            }
        }
    }
}
