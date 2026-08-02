using System;
using UnityEditor;
using UnityEngine;

namespace Fynite.Editor
{
    /// <summary>
    /// Read-only summary of what a definition asset compiles to, so an asset can be inspected without
    /// entering play mode. The visual <c>.fyn</c> authoring tool will be built alongside this later.
    /// </summary>
    [CustomEditor(typeof(FyniteDefinitionAsset), true)]
    public sealed class FyniteDefinitionAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var asset = (FyniteDefinitionAsset)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Compiled definition", EditorStyles.boldLabel);

            var contextType = asset.ContextType;
            EditorGUILayout.LabelField("Context", contextType != null ? contextType.FullName : "<none>");

            IFyniteDefinition definition;
            try
            {
                definition = asset.Definition;
            }
            catch (Exception exception)
            {
                // A definition that cannot be built is exactly what this inspector should surface.
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                return;
            }

            if (definition == null)
            {
                EditorGUILayout.HelpBox("This asset has not produced a definition yet.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Root", definition.GetStateName(definition.Root));
            EditorGUILayout.LabelField("States", definition.StateCount.ToString());
            EditorGUILayout.LabelField("Signals", definition.SignalCount.ToString());
            EditorGUILayout.LabelField("Depth", definition.MaxDepth.ToString());
        }
    }
}
