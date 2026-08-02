using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>
    /// Draws the state authoring commands in the <c>.fyn</c> inspector.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A form: one popup to choose which state the commands apply to, read-only text saying what that
    /// state is, and a button per operation. It deliberately draws no node, no edge, no canvas and no
    /// tree — the graph is the graph, and this is the inspector of the file the graph lives in. It also
    /// claims no integration with the canvas: opening one does not change the other's selection, and the
    /// canvas has to be reopened to see edits made here.
    /// </para>
    /// <para>
    /// Everything shown is read from the live graph on each repaint. There is no cached hierarchy to go
    /// stale, so a node deleted or rewired elsewhere simply stops appearing.
    /// </para>
    /// </remarks>
    internal sealed class FyniteStateAuthoringGUI
    {
        private const string NoChoice = "— choose —";

        private string _pendingName;
        private string _nameOwnerGuid;
        private string _moveTargetGuid;
        private string _orphanGuid;
        private string _orphanParentGuid;
        private string _message;
        private bool _messageIsError;
        private bool _expanded = true;

        /// <summary>Draws the section. <paramref name="enabled"/> is false while other edits are pending.</summary>
        internal void Draw(FyniteStateAuthoringSession session, bool enabled)
        {
            _expanded = EditorGUILayout.Foldout(_expanded, "State Authoring", true, EditorStyles.foldoutHeader);

            if (!_expanded)
            {
                return;
            }

            if (session == null)
            {
                EditorGUILayout.HelpBox("The .fyn graph could not be loaded.", MessageType.Error);
                return;
            }

            using (new EditorGUI.DisabledScope(!enabled))
            {
                DrawSelection(session);
                DrawSelectedState(session);
                EditorGUILayout.Space();
                DrawOrphans(session);
            }

            DrawMessage();
        }

        private void DrawSelection(FyniteStateAuthoringSession session)
        {
            var states = session.States();

            if (states.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "This graph has no states yet. Add the first one under the root.",
                    MessageType.Info);

                using (new EditorGUI.DisabledScope(session.Graph.FindRoot() == null))
                {
                    if (GUILayout.Button("Add State Under Root"))
                    {
                        Run(session.CreateChildOf(session.Graph.FindRoot()?.FyniteGuid, out var error), error,
                            "State created under the root.");
                    }
                }

                return;
            }

            var labels = new string[states.Count + 1];
            var guids = new string[states.Count + 1];
            labels[0] = NoChoice;
            guids[0] = null;

            for (int i = 0; i < states.Count; i++)
            {
                labels[i + 1] = Describe(states[i]);
                guids[i + 1] = states[i].FyniteGuid;
            }

            int current = IndexOf(guids, session.SelectedGuid);
            int next = EditorGUILayout.Popup(
                new GUIContent("State", "The state every command below applies to. Identified by GUID, so renaming never changes which one this is."),
                current,
                labels);

            if (next != current)
            {
                session.Select(guids[next]);
                _pendingName = null;
                _moveTargetGuid = null;
                _message = null;
            }
        }

        private void DrawSelectedState(FyniteStateAuthoringSession session)
        {
            var state = session.SelectedState();

            if (state == null)
            {
                return;
            }

            SyncPendingName(state);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    new GUIContent("Path", "Where this state sits, from the root down."),
                    session.SelectedPath());
                EditorGUILayout.TextField(
                    new GUIContent("Role", "Read-only. Initial is an invariant of the parent, not a box to tick."),
                    Role(state));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _pendingName = EditorGUILayout.TextField(
                    new GUIContent("Name", "The label only. The GUID, the wires and every reaction survive a rename."),
                    _pendingName);

                bool renameable = !string.IsNullOrWhiteSpace(_pendingName) &&
                                  _pendingName.Trim() != state.StateName;

                using (new EditorGUI.DisabledScope(!renameable))
                {
                    if (GUILayout.Button("Rename", GUILayout.Width(72f)))
                    {
                        Run(session.Rename(_pendingName, out var error), error,
                            "Renamed to '" + _pendingName.Trim() + "'.");
                    }
                }
            }

            bool parented = state.ResolveParent() != null;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!parented || state.IsInitial))
                {
                    if (GUILayout.Button(new GUIContent(
                            "Set as Initial",
                            "Makes this the child its parent enters. Only direct siblings lose the mark.")))
                    {
                        Run(session.SetAsInitial(out var error), error,
                            "'" + state.StateName + "' is now the initial child.");
                    }
                }

                if (GUILayout.Button(new GUIContent("Add Child", "Creates a state inside this one.")))
                {
                    Run(session.CreateChild(out var error), error, "Child created and selected.");
                }

                using (new EditorGUI.DisabledScope(!parented))
                {
                    if (GUILayout.Button(new GUIContent(
                            "Duplicate", "Adds a sibling copy with a new identity. The copy is never Initial.")))
                    {
                        Run(session.Duplicate(out var error), error, "Duplicated and selected the copy.");
                    }
                }
            }

            DrawMove(session, state);
        }

        private void DrawMove(FyniteStateAuthoringSession session, FyniteStateNode state)
        {
            var choices = session.ParentChoices();

            var labels = new string[choices.Count + 1];
            var guids = new string[choices.Count + 1];
            labels[0] = NoChoice;
            guids[0] = null;

            for (int i = 0; i < choices.Count; i++)
            {
                labels[i + 1] = FyniteStateOperations.PathOf(choices[i]);
                guids[i + 1] = choices[i].FyniteGuid;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                int current = IndexOf(guids, _moveTargetGuid);
                int next = EditorGUILayout.Popup(
                    new GUIContent("Move Under", "Only nodes that cannot form a cycle are listed. Nothing is chosen for you."),
                    current,
                    labels);

                if (next != current)
                {
                    _moveTargetGuid = guids[next];
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_moveTargetGuid)))
                {
                    if (GUILayout.Button("Move", GUILayout.Width(72f)))
                    {
                        Run(session.MoveTo(_moveTargetGuid, out var error), error,
                            "'" + state.StateName + "' moved.");
                        _moveTargetGuid = null;
                    }
                }
            }
        }

        private void DrawOrphans(FyniteStateAuthoringSession session)
        {
            var orphans = session.Orphans();

            EditorGUILayout.LabelField(
                new GUIContent("Unparented States", "States wired to no parent. They belong to no machine and the compiler reports them."),
                EditorStyles.boldLabel);

            if (orphans.Count == 0)
            {
                EditorGUILayout.LabelField(" ", "None.");
                return;
            }

            var labels = new string[orphans.Count + 1];
            var guids = new string[orphans.Count + 1];
            labels[0] = NoChoice;
            guids[0] = null;

            for (int i = 0; i < orphans.Count; i++)
            {
                labels[i + 1] = orphans[i].StateName;
                guids[i + 1] = orphans[i].FyniteGuid;
            }

            int currentOrphan = IndexOf(guids, _orphanGuid);
            int nextOrphan = EditorGUILayout.Popup(new GUIContent("State"), currentOrphan, labels);

            if (nextOrphan != currentOrphan)
            {
                _orphanGuid = guids[nextOrphan];
                _orphanParentGuid = null;
            }

            var orphan = FyniteStateOperations.FindState(session.Graph, _orphanGuid);

            if (orphan == null)
            {
                return;
            }

            var choices = FyniteStateOperations.ValidParents(session.Graph, orphan);
            var parentLabels = new string[choices.Count + 1];
            var parentGuids = new string[choices.Count + 1];
            parentLabels[0] = NoChoice;
            parentGuids[0] = null;

            for (int i = 0; i < choices.Count; i++)
            {
                parentLabels[i + 1] = FyniteStateOperations.PathOf(choices[i]);
                parentGuids[i + 1] = choices[i].FyniteGuid;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                int current = IndexOf(parentGuids, _orphanParentGuid);
                int next = EditorGUILayout.Popup(new GUIContent("Give Parent"), current, parentLabels);

                if (next != current)
                {
                    _orphanParentGuid = parentGuids[next];
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_orphanParentGuid)))
                {
                    if (GUILayout.Button("Adopt", GUILayout.Width(72f)))
                    {
                        Run(session.AdoptOrphan(_orphanGuid, _orphanParentGuid, out var error), error,
                            "'" + orphan.StateName + "' now has a parent.");
                        _orphanGuid = null;
                        _orphanParentGuid = null;
                    }
                }
            }
        }

        private void DrawMessage()
        {
            if (string.IsNullOrEmpty(_message))
            {
                return;
            }

            EditorGUILayout.HelpBox(_message, _messageIsError ? MessageType.Warning : MessageType.Info);
        }

        private void SyncPendingName(FyniteStateNode state)
        {
            if (string.Equals(_nameOwnerGuid, state.FyniteGuid, System.StringComparison.Ordinal) &&
                _pendingName != null)
            {
                return;
            }

            _nameOwnerGuid = state.FyniteGuid;
            _pendingName = state.StateName;
        }

        private void Run(bool succeeded, string error, string success)
        {
            _messageIsError = !succeeded;
            _message = succeeded ? success : error;

            if (succeeded)
            {
                // The rename field follows the state it belongs to; after any command the selection may
                // be a different node, so let it reload from whatever is selected now.
                _pendingName = null;
                _nameOwnerGuid = null;
            }

            GUI.FocusControl(null);
        }

        private static string Describe(FyniteStateNode state)
        {
            var path = FyniteStateOperations.PathOf(state);
            return state.IsInitial ? path + "  (initial)" : path;
        }

        private static string Role(FyniteStateNode state)
        {
            var parts = new List<string>(3);
            parts.Add(state.ResolveChildren().Count > 0 ? "composite" : "leaf");

            if (state.IsInitial)
            {
                parts.Add("initial child of " + FyniteStateOperations.PathOf(state.ResolveParent()));
            }

            if (state.ResolveParent() == null)
            {
                parts.Add("no parent");
            }

            return string.Join(" · ", parts);
        }

        private static int IndexOf(string[] guids, string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return 0;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                if (string.Equals(guids[i], guid, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
