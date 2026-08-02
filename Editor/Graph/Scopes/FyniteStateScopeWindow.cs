using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Fynite.GraphEditor
{
    /// <summary>A focused, one-level projection of the hierarchy stored in a single .fyn graph.</summary>
    public sealed class FyniteStateScopeWindow : EditorWindow
    {
        private const float CardWidth = 210f;
        private const float CardHeight = 92f;
        private const float InspectorWidth = 280f;

        [SerializeField] private string _assetPath;
        [SerializeField] private string _selectedGuid;
        private FyniteStateScopeSession _session;
        private string _renamingGuid;
        private string _renameBuffer;
        private string _notice;

        [OnOpenAsset(0)]
        private static bool OpenFynite(EntityId instanceId, int line)
        {
            var asset = EditorUtility.EntityIdToObject(instanceId) as FyniteGraphAsset;
            if (asset == null)
            {
                return false;
            }

            Open(AssetDatabase.GetAssetPath(asset));
            return true;
        }

        public static FyniteStateScopeWindow Open(string assetPath)
        {
            var window = CreateWindow<FyniteStateScopeWindow>();
            window.titleContent = new GUIContent("Fynite State Scope");
            window._assetPath = assetPath;
            window.Reload();
            window.Show();
            window.Focus();
            return window;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            if (!string.IsNullOrEmpty(_assetPath))
            {
                Reload();
            }
        }

        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void OnUndoRedo()
        {
            Reload();
            Repaint();
        }

        private void Reload()
        {
            var graph = GraphDatabase.LoadGraph<FyniteGraph>(_assetPath);
            if (graph == null)
            {
                _session = null;
                return;
            }

            if (_session == null)
            {
                _session = new FyniteStateScopeSession(graph);
            }
            else
            {
                _session.ReplaceGraph(graph);
            }
        }

        private void OnGUI()
        {
            HandleKeyboard();
            DrawToolbar();
            if (_session?.Graph == null)
            {
                EditorGUILayout.HelpBox("The .fyn graph could not be loaded.", MessageType.Error);
                return;
            }

            var roots = _session.Graph.FindRoots();
            if (roots.Count != 1)
            {
                EditorGUILayout.HelpBox(
                    roots.Count == 0
                        ? "This graph has no Root. Add one explicitly before using State Scopes."
                        : "This graph has multiple Roots. Resolve the ambiguity without deleting data before using State Scopes.",
                    MessageType.Error);
                DrawRecovery();
                return;
            }

            DrawBreadcrumbs();
            if (!string.IsNullOrEmpty(_notice))
            {
                EditorGUILayout.HelpBox(_notice, MessageType.Info);
            }

            var content = new Rect(0f, 62f, position.width - InspectorWidth, position.height - 62f);
            var inspector = new Rect(position.width - InspectorWidth, 62f, InspectorWidth, position.height - 62f);
            DrawScope(content);
            DrawInspector(inspector);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(_session == null || !(_session.Owner is FyniteStateNode)))
                {
                    if (GUILayout.Button("Back", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                    {
                        _session.Back();
                    }
                }
                if (GUILayout.Button("Root", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                {
                    _session?.ToRoot();
                }
                using (new EditorGUI.DisabledScope(_session?.Owner == null))
                {
                    if (GUILayout.Button("Create State", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                    {
                        CreateChild();
                    }
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label("State Scope", EditorStyles.miniLabel);
            }
        }

        private void DrawBreadcrumbs()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                var nodes = _session.BreadcrumbNodes();
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (i > 0) GUILayout.Label("/", GUILayout.Width(10f));
                    var label = nodes[i] is FyniteStateNode state ? state.StateName : "Root";
                    if (GUILayout.Button(label, EditorStyles.linkLabel, GUILayout.ExpandWidth(false)))
                    {
                        _session.OpenAncestor(nodes[i]);
                    }
                }
            }
        }

        private void DrawScope(Rect area)
        {
            GUI.Box(area, GUIContent.none);
            var owner = _session.Owner;
            var children = _session.VisibleChildren;
            var ownerRect = new Rect(area.x + 30f, area.y + 35f, CardWidth, 62f);
            GUI.Box(ownerRect, owner is FyniteStateNode state ? state.StateName : "Root", EditorStyles.helpBox);

            if (children.Count == 0)
            {
                GUI.Label(new Rect(area.x + 285f, area.y + 38f, 360f, 22f), "This State has no child States yet.");
                if (GUI.Button(new Rect(area.x + 285f, area.y + 66f, 140f, 25f), "Create Child State"))
                {
                    CreateChild();
                }
            }
            else
            {
                Handles.BeginGUI();
                for (int i = 0; i < children.Count; i++)
                {
                    float y = area.y + 25f + i * (CardHeight + 18f);
                    var card = new Rect(area.x + 310f, y, CardWidth, CardHeight);
                    Handles.DrawLine(new Vector3(ownerRect.xMax, ownerRect.center.y), new Vector3(card.x, card.center.y));
                }
                Handles.EndGUI();

                for (int i = 0; i < children.Count; i++)
                {
                    float y = area.y + 25f + i * (CardHeight + 18f);
                    DrawStateCard(children[i], new Rect(area.x + 310f, y, CardWidth, CardHeight));
                }
            }

            if (owner is FyniteRootStateNode)
            {
                DrawOrphans(area);
            }
        }

        private void DrawStateCard(FyniteStateNode state, Rect rect)
        {
            bool selected = state.FyniteGuid == _selectedGuid;
            GUI.Box(rect, GUIContent.none, selected ? "SelectionRect" : "HelpBox");
            var titleRect = new Rect(rect.x + 8f, rect.y + 6f, rect.width - 42f, 22f);

            if (_renamingGuid == state.FyniteGuid)
            {
                GUI.SetNextControlName("StateRename");
                _renameBuffer = GUI.TextField(titleRect, _renameBuffer);
                EditorGUI.FocusTextInControl("StateRename");
            }
            else
            {
                GUI.Label(titleRect, state.StateName, EditorStyles.boldLabel);
            }

            int childCount = state.ResolveChildren().Count;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 31f, rect.width - 16f, 18f),
                childCount == 0 ? "STATE · leaf" : "STATE · " + childCount + (childCount == 1 ? " child" : " children"),
                EditorStyles.miniLabel);
            if (state.IsInitial)
            {
                GUI.Label(new Rect(rect.x + 8f, rect.y + 55f, 58f, 18f), "INITIAL", EditorStyles.miniBoldLabel);
            }
            if (GUI.Button(new Rect(rect.xMax - 32f, rect.y + 5f, 26f, 22f), new GUIContent("›", "Open this state's direct children in a focused canvas.")))
            {
                _session.Open(state);
            }

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
            {
                _selectedGuid = state.FyniteGuid;
                if (evt.button == 1)
                {
                    ShowContextMenu(state);
                    evt.Use();
                }
                else if (evt.clickCount == 2)
                {
                    if (titleRect.Contains(evt.mousePosition)) BeginRename(state);
                    else _session.Open(state);
                    evt.Use();
                }
            }
        }

        private void DrawInspector(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.helpBox);
            GUILayout.Label("State", EditorStyles.boldLabel);
            var selected = SelectedState();
            if (selected == null)
            {
                GUILayout.Label("Select a State to edit it.", EditorStyles.wordWrappedMiniLabel);
                GUILayout.EndArea();
                return;
            }

            EditorGUI.BeginChangeCheck();
            var nextName = EditorGUILayout.DelayedTextField(new GUIContent("Name"), selected.StateName);
            if (EditorGUI.EndChangeCheck() && FyniteStateOperations.Rename(_session.Graph, selected, nextName))
            {
                Persist();
            }
            if (GUILayout.Button(new GUIContent("Open State", "Open this state's direct children in a focused canvas.")))
                _session.Open(selected);
            if (GUILayout.Button(new GUIContent("Set as Initial", "The Initial state is the first direct child entered when its Parent becomes active.")))
            {
                if (FyniteStateOperations.SetAsInitial(_session.Graph, selected)) Persist();
            }
            if (GUILayout.Button("Move to Parent...")) ShowMoveMenu(selected);

            GUILayout.Space(10f);
            GUILayout.Label("Reactions", EditorStyles.boldLabel);
            foreach (var reaction in selected.ResolveReactions())
            {
                var target = reaction.ResolveTarget();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(target != null ? FyniteStateOperations.PathOf(target) : "<effect only>", EditorStyles.miniLabel);
                    if (target != null && GUILayout.Button("Open", GUILayout.Width(46f))) _session.Open(target);
                }
            }
            GUILayout.EndArea();
        }

        private void DrawOrphans(Rect area)
        {
            var orphans = _session.Graph.GetNodes().OfType<FyniteStateNode>().Where(s => s.ResolveParent() == null).ToList();
            if (orphans.Count == 0) return;
            var rect = new Rect(area.x + 30f, area.yMax - 90f, area.width - 60f, 65f);
            GUI.Box(rect, "Unparented States", EditorStyles.helpBox);
            for (int i = 0; i < orphans.Count; i++)
            {
                if (GUI.Button(new Rect(rect.x + 8f + i * 130f, rect.y + 28f, 120f, 22f), orphans[i].StateName))
                    _selectedGuid = orphans[i].FyniteGuid;
            }
        }

        private void DrawRecovery()
        {
            GUILayout.Label("All nodes remain in the .fyn file. Open the Graph Toolkit canvas to repair Root structure.");
        }

        private void CreateChild()
        {
            int index = _session.VisibleChildren.Count;
            var child = FyniteStateOperations.CreateChild(
                _session.Graph,
                _session.Owner,
                new Vector2(320f, 40f + index * (CardHeight + 18f)));
            if (child != null)
            {
                _selectedGuid = child.FyniteGuid;
                Persist();
                BeginRename(child);
            }
        }

        private void ShowContextMenu(FyniteStateNode state)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Open State"), false, () => { _session.Open(state); Repaint(); });
            menu.AddItem(new GUIContent("Rename"), false, () => BeginRename(state));
            menu.AddItem(new GUIContent("Set as Initial"), state.IsInitial, () => { if (FyniteStateOperations.SetAsInitial(_session.Graph, state)) Persist(); });
            menu.AddItem(new GUIContent("Move to Parent..."), false, () => ShowMoveMenu(state));
            menu.ShowAsContext();
        }

        private void ShowMoveMenu(FyniteStateNode state)
        {
            var menu = new GenericMenu();
            var root = _session.Graph.FindRoot();
            if (root != null) menu.AddItem(new GUIContent("Root"), false, () => Move(state, root));
            foreach (var parent in FyniteStateOperations.ValidParents(_session.Graph, state))
            {
                var captured = parent;
                menu.AddItem(new GUIContent(FyniteStateOperations.PathOf(parent)), false, () => Move(state, captured));
            }
            menu.ShowAsContext();
        }

        private void Move(FyniteStateNode state, IFyniteIdentifiedNode parent)
        {
            if (FyniteStateOperations.MoveToParent(_session.Graph, state, parent, out var error))
            {
                _notice = null;
                Persist();
            }
            else _notice = error;
        }

        private void BeginRename(FyniteStateNode state)
        {
            _renamingGuid = state.FyniteGuid;
            _renameBuffer = state.StateName;
            Repaint();
        }

        private void CommitRename()
        {
            var state = SelectedOrRenamingState();
            if (state != null && FyniteStateOperations.Rename(_session.Graph, state, _renameBuffer)) Persist();
            else if (string.IsNullOrWhiteSpace(_renameBuffer)) _notice = "Name cannot be empty; the previous name was restored.";
            _renamingGuid = null;
        }

        private void HandleKeyboard()
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown) return;
            if (evt.keyCode == KeyCode.F2 && SelectedState() != null)
            {
                BeginRename(SelectedState());
                evt.Use();
            }
            else if (!string.IsNullOrEmpty(_renamingGuid) && evt.keyCode == KeyCode.Return)
            {
                CommitRename();
                evt.Use();
            }
            else if (!string.IsNullOrEmpty(_renamingGuid) && evt.keyCode == KeyCode.Escape)
            {
                _renamingGuid = null;
                evt.Use();
            }
        }

        private FyniteStateNode SelectedState() =>
            _session?.Graph.GetNodes().OfType<FyniteStateNode>().FirstOrDefault(s => s.FyniteGuid == _selectedGuid);

        private FyniteStateNode SelectedOrRenamingState() =>
            _session?.Graph.GetNodes().OfType<FyniteStateNode>().FirstOrDefault(s => s.FyniteGuid == _renamingGuid);

        private void Persist()
        {
            GraphDatabase.SaveGraph(_session.Graph);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Reload();
            Repaint();
        }
    }
}
