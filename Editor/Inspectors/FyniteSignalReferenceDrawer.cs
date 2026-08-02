using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fynite.Editor
{
    /// <summary>
    /// Draws a <see cref="FyniteSignalReference"/> as a graph field plus a dropdown of that graph's
    /// signals.
    /// </summary>
    /// <remarks>
    /// What is stored is the signal's persistent GUID, but nobody should ever have to see one. The
    /// dropdown shows names and writes identities, and a reference whose signal has since been deleted
    /// says so instead of quietly resolving to whichever signal now sits at that position.
    /// </remarks>
    [CustomPropertyDrawer(typeof(FyniteSignalReference))]
    public sealed class FyniteSignalReferenceDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        /// <inheritdoc />
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            var graph = GraphOf(property);

            // Graph row, signal row, and a help box only when there is something wrong to say.
            float height = line * 2f + Spacing;

            if (graph != null && MissingSignal(property, graph))
            {
                height += line * 2f + Spacing;
            }

            return height;
        }

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var graphProperty = property.FindPropertyRelative("graph");
            var guidProperty = property.FindPropertyRelative("signalGuid");

            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight;
            var row = new Rect(position.x, position.y, position.width, line);

            EditorGUI.PropertyField(row, graphProperty, label);

            row.y += line + Spacing;
            var graph = graphProperty.objectReferenceValue as FyniteGraphAsset;

            using (new EditorGUI.DisabledScope(graph == null))
            {
                DrawSignalPopup(row, graph, guidProperty);
            }

            if (graph != null && MissingSignal(property, graph))
            {
                row.y += line + Spacing;
                row.height = line * 2f;
                EditorGUI.HelpBox(
                    row,
                    "This signal no longer exists in '" + graph.name + "'. Pick another one.",
                    MessageType.Error);
            }

            EditorGUI.EndProperty();
        }

        private static void DrawSignalPopup(Rect row, FyniteGraphAsset graph, SerializedProperty guidProperty)
        {
            var indented = EditorGUI.IndentedRect(row);
            var labelRect = new Rect(row.x, row.y, EditorGUIUtility.labelWidth, row.height);
            var fieldRect = new Rect(indented.x + EditorGUIUtility.labelWidth - (indented.x - row.x), row.y,
                row.width - EditorGUIUtility.labelWidth, row.height);

            EditorGUI.LabelField(labelRect, " ");

            if (graph == null)
            {
                EditorGUI.LabelField(fieldRect, "Signal", "assign a graph first");
                return;
            }

            var names = new List<string> { "<none>" };
            var guids = new List<string> { string.Empty };

            var signals = graph.Signals;
            for (int i = 0; i < signals.Count; i++)
            {
                names.Add(signals[i].name);
                guids.Add(signals[i].guid);
            }

            int current = guids.IndexOf(guidProperty.stringValue ?? string.Empty);
            if (current < 0)
            {
                names.Add("<missing>");
                guids.Add(guidProperty.stringValue);
                current = names.Count - 1;
            }

            int selected = EditorGUI.Popup(fieldRect, "Signal", current, names.ToArray());
            if (selected != current)
            {
                guidProperty.stringValue = guids[selected];
            }
        }

        private static FyniteGraphAsset GraphOf(SerializedProperty property) =>
            property.FindPropertyRelative("graph").objectReferenceValue as FyniteGraphAsset;

        private static bool MissingSignal(SerializedProperty property, FyniteGraphAsset graph)
        {
            var guid = property.FindPropertyRelative("signalGuid").stringValue;
            return !string.IsNullOrEmpty(guid) && graph.GetSignalName(guid) == null;
        }
    }
}
