using UnityEditor;
using UnityEngine;

namespace FyniteEditor
{
    /// <summary>
    /// Lists the Fynite machines the PlayerLoop is currently driving and shows the active path of the
    /// one you pick. Read only: nothing here starts, stops or steers a machine.
    /// </summary>
    public sealed class FyniteDebuggerWindow : EditorWindow
    {
        private const string Title = "Fynite Debugger";
        private const string NotPlaying = "Enter Play Mode to inspect running Fynite machines.";
        private const string Empty = "No running Fynite machines.";

        /// <summary>Five refreshes a second is plenty to watch a state machine by eye.</summary>
        private const double RefreshInterval = 0.2;

        private const float ListWidth = 240f;

        private readonly FyniteDebuggerModel model = new FyniteDebuggerModel();

        private bool autoRefresh = true;
        private double nextRefresh;
        private Vector2 listScroll;
        private Vector2 detailScroll;

        [MenuItem("Window/Fynite/Debugger")]
        public static void Open() => GetWindow<FyniteDebuggerWindow>().Show();

        private void OnEnable()
        {
            titleContent = new GUIContent(Title);

            // Removed first so re-enabling the window — which is what a domain reload looks like from
            // here — cannot leave two subscriptions behind.
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            model.Clear();
        }

        private void OnEditorUpdate()
        {
            if (!autoRefresh || !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now < nextRefresh)
            {
                return;
            }

            nextRefresh = now + RefreshInterval;

            model.Refresh();
            Repaint();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode ||
                change == PlayModeStateChange.EnteredEditMode)
            {
                // The machines of that session are gone; holding their entries would only keep them
                // and their owners alive.
                model.Clear();
            }

            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(NotPlaying, MessageType.Info);
                return;
            }

            if (model.Count == 0)
            {
                EditorGUILayout.HelpBox(Empty, MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMachineList();
                DrawDetails();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(
                    model.Count == 1 ? "1 machine" : model.Count + " machines",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(90f));

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    model.Refresh();
                }

                autoRefresh = GUILayout.Toggle(
                    autoRefresh,
                    "Auto Refresh",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(100f));

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawMachineList()
        {
            using (var scope = new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
            {
                _ = scope;
                listScroll = EditorGUILayout.BeginScrollView(listScroll);

                for (var i = 0; i < model.Count; i++)
                {
                    var selected = i == model.SelectedIndex;

                    if (GUILayout.Toggle(selected, model.GetEntry(i).ListLabel, EditorStyles.miniButton)
                        != selected)
                    {
                        model.Select(selected ? -1 : i);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDetails()
        {
            using (var scope = new EditorGUILayout.VerticalScope())
            {
                _ = scope;
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

                var entry = model.Selected;

                if (entry == null)
                {
                    EditorGUILayout.LabelField("Select a machine.", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                DrawOwner(entry);

                EditorGUILayout.LabelField("Context Type", entry.ContextTypeName);
                EditorGUILayout.LabelField("Status", entry.PathCount > 0 ? "Running" : "Stopped");
                EditorGUILayout.LabelField("Current State", entry.LeafLabel);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Active Path", EditorStyles.boldLabel);

                DrawActivePath(entry);

                EditorGUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// One interactive but harmless row: the field can be clicked, dragged onto and picked from,
        /// and whatever it returns is thrown away, so the machine's owner can never be swapped from
        /// here. Select and Ping are the two things the row is actually for.
        /// </summary>
        private static void DrawOwner(FyniteDebuggerEntry entry)
        {
            var owner = entry.Owner;
            var alive = owner != null;

            using (new EditorGUILayout.HorizontalScope())
            {
                // The return value is deliberately discarded: this shows an owner, it does not set one.
                EditorGUILayout.ObjectField("Owner", owner, typeof(Object), true);

                using (new EditorGUI.DisabledScope(!alive))
                {
                    if (GUILayout.Button("Select", EditorStyles.miniButtonLeft, GUILayout.Width(52f)))
                    {
                        SelectOwner(owner);
                    }

                    if (GUILayout.Button("Ping", EditorStyles.miniButtonRight, GUILayout.Width(42f)))
                    {
                        PingOwner(owner);
                    }
                }
            }

            if (!alive)
            {
                EditorGUILayout.LabelField(" ", FyniteDebuggerEntry.DestroyedOwner);
            }
        }

        /// <summary>Selects the owner in the Editor. A destroyed owner is simply not selected.</summary>
        internal static void SelectOwner(Object owner)
        {
            if (owner == null)
            {
                return;
            }

            Selection.activeObject = owner;
        }

        /// <summary>Highlights the owner where it lives. A destroyed owner is simply not pinged.</summary>
        internal static void PingOwner(Object owner)
        {
            if (owner == null)
            {
                return;
            }

            EditorGUIUtility.PingObject(owner);
        }

        private static void DrawActivePath(FyniteDebuggerEntry entry)
        {
            if (entry.PathCount == 0)
            {
                EditorGUILayout.LabelField(FyniteDebuggerEntry.Stopped);
                return;
            }

            for (var i = 0; i < entry.PathCount; i++)
            {
                var leaf = i == entry.PathCount - 1;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(i * 14f);

                    if (i > 0)
                    {
                        GUILayout.Label("└", GUILayout.Width(14f));
                    }

                    // The leaf is named as well as emphasised, so the current state is still obvious
                    // without relying on how it looks.
                    GUILayout.Label(
                        leaf ? entry.GetPathState(i).Name + "  (current)" : entry.GetPathState(i).Name,
                        leaf ? EditorStyles.boldLabel : EditorStyles.label);
                }
            }
        }
    }
}
