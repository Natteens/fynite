using UnityEditor;
using UnityEngine;

namespace Fynite.Editor
{
    /// <summary>
    /// Inspector for a compiled graph: whether it compiled, and what it compiled to.
    /// </summary>
    /// <remarks>
    /// The first thing this has to answer is whether the asset is safe to use. A graph that failed to
    /// compile is left deliberately empty by the importer, and saying so plainly here is what stops
    /// someone assuming the previous version is still running.
    /// </remarks>
    [CustomEditor(typeof(FyniteGraphAsset))]
    public sealed class FyniteGraphAssetEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            var asset = (FyniteGraphAsset)target;

            if (asset.CompilationError != null)
            {
                EditorGUILayout.HelpBox(
                    "This graph did not compile, so it holds no machine and any runner using it will refuse " +
                    "to start.\n\n" + asset.CompilationError,
                    MessageType.Error);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Schema version", asset.SchemaVersion.ToString());
                EditorGUILayout.LabelField("Graph identity", asset.GraphGuid ?? "<none>");
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
            EditorGUILayout.LabelField("Context", asset.ContextType != null ? asset.ContextType.FullName : "<unknown>");
            EditorGUILayout.LabelField("Root", definition.GetStateName(definition.Root));
            EditorGUILayout.LabelField("States", definition.StateCount.ToString());
            EditorGUILayout.LabelField("Signals", definition.SignalCount.ToString());
            EditorGUILayout.LabelField("Depth", definition.MaxDepth.ToString());
            EditorGUILayout.LabelField("Schema version", asset.SchemaVersion.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Signals", EditorStyles.boldLabel);

            var signals = asset.Signals;
            if (signals.Count == 0)
            {
                EditorGUILayout.LabelField("  <none declared>");
            }

            for (int i = 0; i < signals.Count; i++)
            {
                var payload = definition.GetPayloadType(definition.GetSignal(i));
                EditorGUILayout.LabelField(
                    "  " + signals[i].name,
                    payload != null ? payload.Name : "no payload");
            }
        }
    }
}
