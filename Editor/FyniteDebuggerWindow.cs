using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FyniteEditor
{
    /// <summary>
    /// Lists the Fynite machines the PlayerLoop is currently driving and shows the active path of the
    /// one you pick. Read only: nothing here starts, stops or steers a machine.
    /// </summary>
    public sealed class FyniteDebuggerWindow : EditorWindow
    {
        internal const string Title = "Fynite Debugger";
        internal const string NotPlayingMessage = "Enter Play Mode to inspect running Fynite machines.";
        internal const string EmptyMessage = "No running Fynite machines.";
        internal const string NoSelectionMessage = "Select a machine to inspect it.";

        /// <summary>Package paths, so the assets resolve wherever the project sits on disk.</summary>
        internal const string LayoutPath = "Packages/com.natteens.fynite/Editor/FyniteDebuggerWindow.uxml";
        internal const string StylePath = "Packages/com.natteens.fynite/Editor/FyniteDebuggerWindow.uss";

        /// <summary>Five refreshes a second is plenty to watch a state machine by eye.</summary>
        private const long RefreshIntervalMs = 200;

        private readonly FyniteDebuggerModel model = new FyniteDebuggerModel();

        /// <summary>Backs the ListView. Reused; the ListView keeps pointing at this same instance.</summary>
        private readonly List<FyniteDebuggerEntry> items = new List<FyniteDebuggerEntry>();

        private Label machineCountLabel;
        private ToolbarButton refreshButton;
        private ToolbarToggle autoRefreshToggle;
        private HelpBox emptyState;
        private TwoPaneSplitView contentSplit;
        private ListView machineList;
        private VisualElement detailsContent;
        private Label detailsPlaceholder;
        private ObjectField ownerField;
        private Button selectOwnerButton;
        private Button pingOwnerButton;
        private Label ownerMissingLabel;
        private Label contextTypeLabel;
        private Label statusLabel;
        private Label currentStateLabel;
        private VisualElement activePathContainer;

        private IVisualElementScheduledItem refreshLoop;

        [MenuItem("Tools/Fynite/Debugger")]
        public static void Open() => GetWindow<FyniteDebuggerWindow>().Show();

        public void CreateGUI()
        {
            titleContent = new GUIContent(Title);

            // Anything from a previous build of the UI goes first, including its schedule, so running
            // CreateGUI again cannot leave a second one ticking.
            StopRefreshLoop();
            rootVisualElement.Clear();

            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);

            if (layout == null || style == null)
            {
                // Say which asset is missing and stop. No half-built UI, and no callbacks left behind.
                rootVisualElement.Add(new HelpBox(
                    $"Fynite: the debugger UI could not be loaded. Missing " +
                    $"{(layout == null ? LayoutPath : StylePath)}.",
                    HelpBoxMessageType.Error));
                return;
            }

            layout.CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(style);

            QueryElements();
            ConfigureMachineList();
            ConfigureToolbar();
            ConfigureOwnerRow();

            StartRefreshLoop();
            Sync();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(Title);

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            StopRefreshLoop();
            model.Clear();
            items.Clear();
        }

        private void QueryElements()
        {
            var root = rootVisualElement;

            machineCountLabel = root.Q<Label>("machine-count-label");
            refreshButton = root.Q<ToolbarButton>("refresh-button");
            autoRefreshToggle = root.Q<ToolbarToggle>("auto-refresh-toggle");
            emptyState = root.Q<HelpBox>("empty-state");
            contentSplit = root.Q<TwoPaneSplitView>("content-split");
            machineList = root.Q<ListView>("machine-list");
            detailsContent = root.Q<VisualElement>("details-content");
            detailsPlaceholder = root.Q<Label>("details-placeholder");
            ownerField = root.Q<ObjectField>("owner-field");
            selectOwnerButton = root.Q<Button>("select-owner-button");
            pingOwnerButton = root.Q<Button>("ping-owner-button");
            ownerMissingLabel = root.Q<Label>("owner-missing-label");
            contextTypeLabel = root.Q<Label>("context-type-label");
            statusLabel = root.Q<Label>("status-label");
            currentStateLabel = root.Q<Label>("current-state-label");
            activePathContainer = root.Q<VisualElement>("active-path-container");

            detailsPlaceholder.text = NoSelectionMessage;
        }

        private void ConfigureToolbar()
        {
            // The button and the schedule go through the same method, so there is one refresh path.
            refreshButton.clicked += Refresh;

            autoRefreshToggle.SetValueWithoutNotify(true);
            autoRefreshToggle.RegisterValueChangedCallback(change =>
            {
                if (change.newValue)
                {
                    refreshLoop?.Resume();
                }
                else
                {
                    refreshLoop?.Pause();
                }
            });
        }

        private void ConfigureMachineList()
        {
            machineList.itemsSource = items;
            machineList.makeItem = () =>
            {
                var label = new Label();
                label.AddToClassList("fynite-list-item");
                return label;
            };

            machineList.bindItem = (element, index) =>
                ((Label)element).text = items[index].ListLabel;

            machineList.selectionChanged += _ => OnListSelectionChanged();
        }

        private void ConfigureOwnerRow()
        {
            ownerField.objectType = typeof(Object);
            ownerField.allowSceneObjects = true;

            // No change callback on purpose: the field shows an owner, it never sets one. Anything a
            // user drops on it is overwritten by the next refresh and never reaches the machine.
            selectOwnerButton.clicked += () => SelectOwner(model.Selected?.Owner);
            pingOwnerButton.clicked += () => PingOwner(model.Selected?.Owner);
        }

        private void StartRefreshLoop()
        {
            refreshLoop = rootVisualElement.schedule.Execute(Refresh).Every(RefreshIntervalMs);

            if (autoRefreshToggle != null && !autoRefreshToggle.value)
            {
                refreshLoop.Pause();
            }
        }

        private void StopRefreshLoop()
        {
            refreshLoop?.Pause();
            refreshLoop = null;
        }

        /// <summary>The one refresh path: the toolbar button and the schedule both land here.</summary>
        private void Refresh()
        {
            if (machineList == null)
            {
                return;
            }

            if (EditorApplication.isPlaying)
            {
                model.Refresh();
            }
            else if (model.Count > 0)
            {
                model.Clear();
            }

            Sync();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode ||
                change == PlayModeStateChange.EnteredEditMode)
            {
                // The machines of that session are gone; holding their entries would only keep them
                // and their owners alive.
                model.Clear();
                items.Clear();
            }

            if (machineList != null)
            {
                Sync();
            }
        }

        /// <summary>Pushes the model into the UI. Never reads a machine itself.</summary>
        private void Sync()
        {
            items.Clear();
            for (var i = 0; i < model.Count; i++)
            {
                items.Add(model.GetEntry(i));
            }

            machineList.RefreshItems();

            var playing = EditorApplication.isPlaying;

            emptyState.text = playing ? EmptyMessage : NotPlayingMessage;
            Show(emptyState, !playing || items.Count == 0);
            Show(contentSplit, playing);

            machineCountLabel.text = items.Count == 1 ? "1 machine" : items.Count + " machines";

            // Told, not asked: setting the selection back must not look like the user clicking.
            if (model.SelectedIndex >= 0)
            {
                machineList.SetSelectionWithoutNotify(new[] { model.SelectedIndex });
            }
            else
            {
                machineList.ClearSelection();
            }

            SyncDetails(model.Selected);
        }

        private void OnListSelectionChanged()
        {
            model.Select(machineList.selectedIndex);
            SyncDetails(model.Selected);
        }

        private void SyncDetails(FyniteDebuggerEntry entry)
        {
            Show(detailsPlaceholder, entry == null);
            Show(detailsContent, entry != null);

            if (entry == null)
            {
                // Nothing of the previous selection is left on screen.
                ownerField.SetValueWithoutNotify(null);
                activePathContainer.Clear();
                return;
            }

            var owner = entry.Owner;
            var alive = owner != null;

            ownerField.SetValueWithoutNotify(alive ? owner : null);
            Show(ownerMissingLabel, !alive);
            selectOwnerButton.SetEnabled(alive);
            pingOwnerButton.SetEnabled(alive);

            contextTypeLabel.text = "Context Type: " + entry.ContextTypeName;
            statusLabel.text = "Status: " + (entry.PathCount > 0 ? "Running" : "Stopped");
            currentStateLabel.text = "Current State: " + entry.LeafLabel;

            SyncActivePath(entry);
        }

        private void SyncActivePath(FyniteDebuggerEntry entry)
        {
            activePathContainer.Clear();

            if (entry.PathCount == 0)
            {
                activePathContainer.Add(Row(FyniteDebuggerEntry.Stopped, 0, false));
                return;
            }

            for (var i = 0; i < entry.PathCount; i++)
            {
                var leaf = i == entry.PathCount - 1;
                var name = entry.GetPathState(i).Name;

                activePathContainer.Add(Row(
                    i == 0 ? name : "└ " + name,
                    i,
                    leaf));
            }
        }

        private static Label Row(string text, int depth, bool leaf)
        {
            // The leaf is named as well as emphasised, so the current state is still obvious without
            // relying on how it looks.
            var row = new Label(leaf ? text + "  (current)" : text);

            row.AddToClassList("active-path-row");
            row.style.marginLeft = depth * 12f;

            if (leaf)
            {
                row.AddToClassList("active-path-leaf");
            }

            return row;
        }

        private static void Show(VisualElement element, bool visible)
            => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

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
    }
}
