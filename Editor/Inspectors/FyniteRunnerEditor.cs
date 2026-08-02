using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fynite.Editor
{
    /// <summary>
    /// The runner's inspector: what it is configured to run, whether that configuration is coherent,
    /// and what the machine is doing right now.
    /// </summary>
    /// <remarks>
    /// The point of the extra sections is that a misconfigured runner should be obvious while editing
    /// rather than at the first frame of play mode. Context type mismatches, a graph that failed to
    /// compile and a machine that faulted are all reported here, next to the fields that cause them.
    /// </remarks>
    [CustomEditor(typeof(FyniteRunner))]
    public sealed class FyniteRunnerEditor : UnityEditor.Editor
    {
        private SerializedProperty _definition;
        private SerializedProperty _context;
        private SerializedProperty _contextOverride;
        private SerializedProperty _startOnEnable;
        private SerializedProperty _tickInUpdate;
        private SerializedProperty _tickInFixedUpdate;
        private SerializedProperty _logDiagnostics;

        private void OnEnable()
        {
            _definition = serializedObject.FindProperty("definition");
            _context = serializedObject.FindProperty("context");
            _contextOverride = serializedObject.FindProperty("contextOverride");
            _startOnEnable = serializedObject.FindProperty("startOnEnable");
            _tickInUpdate = serializedObject.FindProperty("tickInUpdate");
            _tickInFixedUpdate = serializedObject.FindProperty("tickInFixedUpdate");
            _logDiagnostics = serializedObject.FindProperty("logDiagnostics");
        }

        /// <inheritdoc />
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // A .fyn file resolves to its compiled asset as the file's main object, so the plain object
            // field already accepts the graph being dragged in and no custom drop handling is needed.
            EditorGUILayout.PropertyField(_definition, new GUIContent("Graph", "The compiled .fyn graph to run."));

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();

            var runner = (FyniteRunner)target;
            DrawContext(runner);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_startOnEnable);
            EditorGUILayout.PropertyField(_tickInUpdate);
            EditorGUILayout.PropertyField(_tickInFixedUpdate);
            EditorGUILayout.PropertyField(_logDiagnostics);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawConfiguration(runner);

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                DrawRuntime(runner);
            }
        }

        private void DrawContext(FyniteRunner runner)
        {
            var expected = runner.Definition != null ? runner.Definition.ContextType : null;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    new GUIContent("Expected Context", "The Context Type declared by the assigned .fyn graph."),
                    expected != null ? expected.FullName : "<not available>");
            }

            int mode = EditorGUILayout.Popup(
                new GUIContent("Context Mode", "Auto uses this GameObject; Override permits an explicit component elsewhere."),
                _contextOverride.boolValue ? 1 : 0,
                new[] { "Auto", "Override" });
            _contextOverride.boolValue = mode == 1;

            if (_contextOverride.boolValue)
            {
                DrawOverrideContext(expected);
                return;
            }

            DrawAutomaticContext(runner, expected);
        }

        private void DrawOverrideContext(Type expected)
        {
            EditorGUI.BeginChangeCheck();
            var selected = EditorGUILayout.ObjectField(
                new GUIContent("Context", "Override may select a compatible component on another GameObject."),
                _context.objectReferenceValue,
                typeof(MonoBehaviour),
                true) as MonoBehaviour;

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            if (selected == null || expected == null || expected.IsInstanceOfType(selected))
            {
                _context.objectReferenceValue = selected;
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Incompatible Context",
                    "The graph expects '" + expected.FullName + "', but '" + selected.GetType().FullName +
                    "' was selected.",
                    "OK");
            }
        }

        private void DrawAutomaticContext(FyniteRunner runner, Type expected)
        {
            var candidates = CompatibleComponents(runner.gameObject, expected);
            var assigned = _context.objectReferenceValue as MonoBehaviour;

            if (assigned != null && (assigned.gameObject != runner.gameObject ||
                                     expected == null || !expected.IsInstanceOfType(assigned)))
            {
                _context.objectReferenceValue = null;
                assigned = null;
            }

            if (candidates.Count == 1 && assigned != candidates[0])
            {
                _context.objectReferenceValue = candidates[0];
                assigned = candidates[0];
            }
            else if (candidates.Count == 0 && assigned != null)
            {
                _context.objectReferenceValue = null;
                assigned = null;
            }

            if (candidates.Count > 1)
            {
                int selectedIndex = Math.Max(0, candidates.IndexOf(assigned));
                var labels = new string[candidates.Count];
                for (int i = 0; i < candidates.Count; i++)
                {
                    labels[i] = candidates[i].GetType().Name + "  (" + i + ")";
                }

                int next = EditorGUILayout.Popup(
                    new GUIContent("Context", "Multiple local components are compatible; choose the serialized instance."),
                    selectedIndex,
                    labels);
                _context.objectReferenceValue = candidates[next];
                EditorGUILayout.HelpBox("Multiple compatible components were found on this GameObject. Choose one explicitly.", MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    new GUIContent("Context", "Auto resolves and serializes the compatible component on this GameObject."),
                    assigned,
                    typeof(MonoBehaviour),
                    true);
            }

            if (expected == null || candidates.Count != 0)
            {
                return;
            }

            EditorGUILayout.HelpBox("Missing " + expected.Name, MessageType.Warning);
            if (CanAddComponent(expected) && GUILayout.Button("Add Component"))
            {
                var component = Undo.AddComponent(runner.gameObject, expected) as MonoBehaviour;
                _context.objectReferenceValue = component;
            }
        }

        private static List<MonoBehaviour> CompatibleComponents(GameObject gameObject, Type expected)
        {
            var compatible = new List<MonoBehaviour>();
            if (gameObject == null || expected == null)
            {
                return compatible;
            }

            var local = gameObject.GetComponents<MonoBehaviour>();
            for (int i = 0; i < local.Length; i++)
            {
                if (local[i] != null && expected.IsInstanceOfType(local[i]))
                {
                    compatible.Add(local[i]);
                }
            }

            return compatible;
        }

        private static bool CanAddComponent(Type expected) =>
            expected != null && typeof(MonoBehaviour).IsAssignableFrom(expected) &&
            !expected.IsAbstract && !expected.IsInterface && !expected.ContainsGenericParameters;

        private void DrawConfiguration(FyniteRunner runner)
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            var definition = runner.Definition;
            if (definition == null)
            {
                EditorGUILayout.HelpBox(
                    "No graph assigned. Create one with Assets → Create → Fynite → Fynite Graph and drag " +
                    "the .fyn file here.",
                    MessageType.Info);
                return;
            }

            if (definition is FyniteGraphAsset graphAsset && graphAsset.CompilationError != null)
            {
                EditorGUILayout.HelpBox(
                    "The assigned graph did not compile, so this runner cannot start:\n" + graphAsset.CompilationError,
                    MessageType.Error);
                return;
            }

            var expected = definition.ContextType;
            EditorGUILayout.LabelField("Expected context", expected != null ? expected.FullName : "<unknown>");

            var context = runner.Context;
            EditorGUILayout.LabelField("Assigned context", context != null ? context.GetType().FullName : "<none>");

            if (context == null)
            {
                EditorGUILayout.HelpBox(
                    "No context assigned. The runner needs a component of type '" +
                    (expected != null ? expected.FullName : "?") + "'.",
                    MessageType.Warning);
                return;
            }

            if (expected != null && !expected.IsInstanceOfType(context))
            {
                EditorGUILayout.HelpBox(
                    "The assigned context is a '" + context.GetType().FullName + "', which is not a '" +
                    expected.FullName + "'. The machine will refuse to start.",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox("Graph and context are compatible.", MessageType.Info);
        }

        private void DrawRuntime(FyniteRunner runner)
        {
            EditorGUILayout.LabelField("Machine", EditorStyles.boldLabel);

            var machine = runner.Machine;
            if (machine == null)
            {
                EditorGUILayout.LabelField("State", "not created");
                return;
            }

            EditorGUILayout.LabelField("Lifecycle", machine.Lifecycle.ToString());

            if (machine.IsRunning)
            {
                EditorGUILayout.LabelField("Active path", DescribeActivePath(machine));
                EditorGUILayout.LabelField("Active leaf", machine.Definition.GetStateName(machine.ActiveLeaf));
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var observation = runner.Observation;
            if (observation == null)
            {
                return;
            }

            var definition = machine.Definition;

            if (observation.LastSignal.IsValid)
            {
                EditorGUILayout.LabelField(
                    "Last signal",
                    definition.GetSignalName(observation.LastSignal) + (observation.LastSignalUnhandled ? "  (unhandled)" : string.Empty));
            }

            if (observation.HasReaction)
            {
                EditorGUILayout.LabelField(
                    "Last reaction",
                    definition.GetStateName(observation.LastReactionSource) + " on " +
                    definition.GetSignalName(observation.LastReactionSignal) +
                    (observation.LastReactionTarget.IsValid
                        ? " → " + definition.GetStateName(observation.LastReactionTarget)
                        : "  (no transition)"));
            }

            if (observation.HasTransition)
            {
                EditorGUILayout.LabelField(
                    "Last transition",
                    definition.GetStateName(observation.LastTransitionSource) + " → " +
                    definition.GetStateName(observation.LastTransitionTarget));
            }

            if (observation.Fault != null)
            {
                EditorGUILayout.HelpBox(
                    "The machine faulted in '" +
                    (observation.FaultState.IsValid ? definition.GetStateName(observation.FaultState) : "?") + "':\n" +
                    observation.Fault.Message,
                    MessageType.Error);
            }
#endif
        }

        private static string DescribeActivePath(IFyniteMachine machine)
        {
            var text = new StringBuilder();
            var definition = machine.Definition;

            for (int depth = 0; depth < machine.ActiveDepth; depth++)
            {
                if (depth > 0)
                {
                    text.Append(" → ");
                }

                text.Append(definition.GetStateName(machine.GetActiveState(depth)));
            }

            return text.Length == 0 ? "<empty>" : text.ToString();
        }
    }
}
