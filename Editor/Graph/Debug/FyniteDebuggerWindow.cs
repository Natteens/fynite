using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// Shows what a running <see cref="FyniteRunner"/> is doing, without touching it or its graph.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything shown comes from the runner's <see cref="FyniteRunnerObservation"/>, which is an
    /// ordinary diagnostics observer the runner installs in the editor and in development builds. The
    /// window reads handles and turns them into names here, so no name or GUID is ever looked up while
    /// the machine runs.
    /// </para>
    /// <para>
    /// It is a window rather than decoration drawn on the graph canvas. Graph Toolkit 0.5 has no API for
    /// transient overlays, and the only public way to change a node's appearance is
    /// <c>INode.DefaultColor</c> — which is graph state, and writing play mode highlights into it would
    /// risk baking them into the saved file. Observing must not modify the asset, so the observation
    /// lives here instead.
    /// </para>
    /// <para>
    /// Each runner owns its own machine and its own observation, so selecting between several runners of
    /// the same graph shows genuinely separate state rather than a merged view.
    /// </para>
    /// </remarks>
    public sealed class FyniteDebuggerWindow : EditorWindow
    {
        private FyniteRunner _observed;
        private bool _followSelection = true;
        private Vector2 _scroll;

        [MenuItem("Window/Analysis/Fynite Debugger")]
        private static void Open()
        {
            var window = GetWindow<FyniteDebuggerWindow>();
            window.titleContent = new GUIContent("Fynite Debugger");
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            // Leaving play mode destroys every machine, so anything remembered about one is stale.
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                _observed = null;
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawRunnerPicker();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter play mode to observe a running machine.", MessageType.Info);
                return;
            }

            var runner = ResolveObserved();
            if (runner == null)
            {
                EditorGUILayout.HelpBox(
                    "No Fynite runner selected. Select one in the hierarchy, or pick one above.",
                    MessageType.Info);
                return;
            }

            var machine = runner.Machine;
            if (machine == null)
            {
                EditorGUILayout.HelpBox("'" + runner.name + "' has not created its machine yet.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawMachine(runner, machine);
            EditorGUILayout.EndScrollView();
        }

        private void DrawRunnerPicker()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _followSelection = GUILayout.Toggle(
                    _followSelection,
                    new GUIContent("Follow Selection", "Observe whichever FyniteRunner is selected in the hierarchy."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(120f));

                using (new EditorGUI.DisabledScope(_followSelection || !Application.isPlaying))
                {
                    var runners = FindRunners();
                    var names = new string[runners.Count + 1];
                    names[0] = "<none>";

                    int current = 0;
                    for (int i = 0; i < runners.Count; i++)
                    {
                        names[i + 1] = runners[i].name + "  (" + DescribeGraph(runners[i]) + ")";
                        if (ReferenceEquals(runners[i], _observed))
                        {
                            current = i + 1;
                        }
                    }

                    int selected = EditorGUILayout.Popup(current, names, EditorStyles.toolbarPopup);
                    if (selected != current)
                    {
                        _observed = selected == 0 ? null : runners[selected - 1];
                    }
                }
            }
        }

        private FyniteRunner ResolveObserved()
        {
            if (!_followSelection)
            {
                return _observed;
            }

            var active = Selection.activeGameObject;
            var selected = active != null ? active.GetComponent<FyniteRunner>() : null;
            if (selected != null)
            {
                _observed = selected;
            }

            return _observed;
        }

        private static void DrawMachine(FyniteRunner runner, IFyniteMachine machine)
        {
            var definition = machine.Definition;

            EditorGUILayout.LabelField("Observing", runner.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Lifecycle", machine.Lifecycle.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active path", EditorStyles.boldLabel);

            if (!machine.IsRunning)
            {
                EditorGUILayout.LabelField("  <not running>");
            }
            else
            {
                for (int depth = 0; depth < machine.ActiveDepth; depth++)
                {
                    var state = machine.GetActiveState(depth);
                    bool isLeaf = depth == machine.ActiveDepth - 1;

                    var style = isLeaf ? EditorStyles.boldLabel : EditorStyles.label;
                    var marker = isLeaf ? "▶ " : "· ";

                    EditorGUILayout.LabelField(
                        new string(' ', depth * 2) + marker + definition.GetStateName(state) +
                        (isLeaf ? "   (active leaf)" : string.Empty),
                        style);
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var observation = runner.Observation;
            if (observation == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last activity", EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "Signal",
                observation.LastSignal.IsValid
                    ? definition.GetSignalName(observation.LastSignal) +
                      (observation.LastSignalUnhandled ? "   (unhandled)" : string.Empty)
                    : "<none yet>");

            EditorGUILayout.LabelField(
                "Reaction",
                observation.HasReaction
                    ? definition.GetStateName(observation.LastReactionSource) + " on " +
                      definition.GetSignalName(observation.LastReactionSignal) +
                      (observation.LastReactionTarget.IsValid
                          ? " → " + definition.GetStateName(observation.LastReactionTarget)
                          : "   (no transition)")
                    : "<none yet>");

            EditorGUILayout.LabelField(
                "Transition",
                observation.HasTransition
                    ? definition.GetStateName(observation.LastTransitionSource) + " → " +
                      definition.GetStateName(observation.LastTransitionTarget) +
                      (observation.LastTransitionLca.IsValid
                          ? "   (kept " + definition.GetStateName(observation.LastTransitionLca) + ")"
                          : "   (whole path re-entered)")
                    : "<none yet>");

            if (observation.Fault != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "Faulted in '" +
                    (observation.FaultState.IsValid ? definition.GetStateName(observation.FaultState) : "?") +
                    "':\n" + observation.Fault.Message,
                    MessageType.Error);
            }
#endif
        }

        private static string DescribeGraph(FyniteRunner runner) =>
            runner.Definition != null ? runner.Definition.name : "no graph";

        /// <summary>
        /// Collects the runners in the loaded scenes by walking their roots.
        /// </summary>
        /// <remarks>
        /// Deliberately a scene walk rather than a global object search. This is editor tooling, but the
        /// rule that nothing discovers its collaborators by scanning the world is worth keeping
        /// everywhere, and walking roots makes the scope of the search explicit.
        /// </remarks>
        private static List<FyniteRunner> FindRunners()
        {
            var runners = new List<FyniteRunner>();
            var roots = new List<GameObject>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                roots.Clear();
                scene.GetRootGameObjects(roots);

                for (int r = 0; r < roots.Count; r++)
                {
                    runners.AddRange(roots[r].GetComponentsInChildren<FyniteRunner>(true));
                }
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                runners.AddRange(stage.prefabContentsRoot.GetComponentsInChildren<FyniteRunner>(true));
            }

            return runners;
        }
    }
}
