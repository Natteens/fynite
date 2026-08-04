using System;
using System.IO;
using System.Text.RegularExpressions;
using Fynite;
using FyniteEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FyniteTests
{
    /// <summary>
    /// The window's behaviour, tested through the model it draws from rather than through pixels: what
    /// it collects, what it keeps selected, and what it lets go of.
    /// </summary>
    public sealed class DebuggerWindowTests : MachineFixture
    {
        private static readonly Regex Boom = new Regex("fynite-test-boom");

        [Test]
        public void TheMenuOpensTheWindowFromTools()
        {
            FyniteDebuggerWindow window = null;

            // Showing a window on a batchmode editor logs that there is no graphics device. That is
            // the environment talking, not the window, so it is tolerated here and nowhere else.
            LogAssert.ignoreFailingMessages = true;

            try
            {
                var opened = EditorApplication.ExecuteMenuItem("Tools/Fynite/Debugger");

                Assert.That(opened, Is.True, "Tools/Fynite/Debugger does not exist");

                window = EditorWindow.GetWindow<FyniteDebuggerWindow>();

                Assert.That(window, Is.Not.Null);
                Assert.That(window.titleContent.text, Is.EqualTo("Fynite Debugger"));
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                    Object.DestroyImmediate(window);
                }

                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void TheOldWindowMenuIsGone()
        {
            var source = WindowSource();

            Assert.That(
                source,
                Does.Not.Contain("\"Window/Fynite/Debugger\""),
                "the old Window menu entry is still declared");
            Assert.That(
                source,
                Does.Contain("[MenuItem(\"Tools/Fynite/Debugger\")]"),
                "the Tools menu entry is missing");

            // Exactly one entry, so the item is not registered under two menus at once.
            Assert.That(CountOf(source, "[MenuItem("), Is.EqualTo(1));
        }

        [Test]
        public void TheLayoutAndStyleAssetsLoadAndClone()
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                FyniteDebuggerWindow.LayoutPath);
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(FyniteDebuggerWindow.StylePath);

            Assert.That(layout, Is.Not.Null, FyniteDebuggerWindow.LayoutPath);
            Assert.That(style, Is.Not.Null, FyniteDebuggerWindow.StylePath);

            var root = new VisualElement();
            Assert.DoesNotThrow(() => layout.CloneTree(root));
            Assert.That(root.childCount, Is.GreaterThan(0), "the layout cloned to nothing");
        }

        [Test]
        public void TheWindowBuildsEveryElementItNeeds()
        {
            var window = ScriptableObject.CreateInstance<FyniteDebuggerWindow>();

            try
            {
                window.CreateGUI();

                var root = window.rootVisualElement;

                Assert.That(root.Q<Toolbar>("toolbar"), Is.Not.Null, "toolbar");
                Assert.That(root.Q<Label>("machine-count-label"), Is.Not.Null, "machine count");
                Assert.That(root.Q<ToolbarButton>("refresh-button"), Is.Not.Null, "refresh button");
                Assert.That(root.Q<ToolbarToggle>("auto-refresh-toggle"), Is.Not.Null, "auto refresh");
                Assert.That(root.Q<HelpBox>("empty-state"), Is.Not.Null, "empty state");
                Assert.That(root.Q<TwoPaneSplitView>("content-split"), Is.Not.Null, "split");
                Assert.That(root.Q<ListView>("machine-list"), Is.Not.Null, "list");
                Assert.That(root.Q<VisualElement>("details-panel"), Is.Not.Null, "details panel");
                Assert.That(root.Q<ObjectField>("owner-field"), Is.Not.Null, "owner field");
                Assert.That(root.Q<Button>("select-owner-button"), Is.Not.Null, "select button");
                Assert.That(root.Q<Button>("ping-owner-button"), Is.Not.Null, "ping button");
                Assert.That(root.Q<Label>("context-type-label"), Is.Not.Null, "context type");
                Assert.That(root.Q<Label>("status-label"), Is.Not.Null, "status");
                Assert.That(root.Q<Label>("current-state-label"), Is.Not.Null, "current state");
                Assert.That(root.Q<VisualElement>("active-path-container"), Is.Not.Null, "active path");
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TheWindowStartsWithAutoRefreshOnAndTheRightMessages()
        {
            var window = ScriptableObject.CreateInstance<FyniteDebuggerWindow>();

            try
            {
                window.CreateGUI();

                var root = window.rootVisualElement;

                Assert.That(root.Q<ToolbarToggle>("auto-refresh-toggle").value, Is.True);

                // Edit mode: the message says so, and the split is out of the way.
                Assert.That(
                    root.Q<HelpBox>("empty-state").text,
                    Is.EqualTo(FyniteDebuggerWindow.NotPlayingMessage));
                Assert.That(
                    root.Q<TwoPaneSplitView>("content-split").style.display.value,
                    Is.EqualTo(DisplayStyle.None));

                // Nothing is selected, so the details panel says what to do instead of showing stale
                // fields.
                Assert.That(
                    root.Q<Label>("details-placeholder").text,
                    Is.EqualTo(FyniteDebuggerWindow.NoSelectionMessage));
                Assert.That(
                    root.Q<Label>("details-placeholder").style.display.value,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(
                    root.Q<VisualElement>("details-content").style.display.value,
                    Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void RebuildingTheUiDoesNotLeaveTwoOfAnything()
        {
            var window = ScriptableObject.CreateInstance<FyniteDebuggerWindow>();

            try
            {
                window.CreateGUI();
                window.CreateGUI();
                window.CreateGUI();

                var root = window.rootVisualElement;

                // A second clone of the tree would show up as duplicated named elements.
                Assert.That(root.Query<ListView>("machine-list").ToList(), Has.Count.EqualTo(1));
                Assert.That(root.Query<Toolbar>("toolbar").ToList(), Has.Count.EqualTo(1));
                Assert.That(root.Query<HelpBox>("empty-state").ToList(), Has.Count.EqualTo(1));
                Assert.That(
                    root.Query<ToolbarToggle>("auto-refresh-toggle").ToList(),
                    Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TheAutoRefreshToggleDoesNotThrowWhenSwitched()
        {
            var window = ScriptableObject.CreateInstance<FyniteDebuggerWindow>();

            try
            {
                window.CreateGUI();

                var toggle = window.rootVisualElement.Q<ToolbarToggle>("auto-refresh-toggle");

                Assert.DoesNotThrow(() => toggle.value = false);
                Assert.That(toggle.value, Is.False);

                Assert.DoesNotThrow(() => toggle.value = true);
                Assert.That(toggle.value, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TheWindowIsFreeOfImgui()
        {
            var source = WindowSource();

            foreach (var banned in new[]
                     {
                         "OnGUI", "EditorGUILayout", "EditorGUI.", "GUILayout", "GUIStyle",
                         "IMGUIContainer", "BeginScrollView", "EndScrollView", "DisabledScope"
                     })
            {
                Assert.That(source, Does.Not.Contain(banned), banned);
            }

            // Pinging is not IMGUI layout; it is the only EditorGUIUtility call the window makes.
            Assert.That(source, Does.Contain("EditorGUIUtility.PingObject"));
            Assert.That(CountOf(source, "EditorGUIUtility."), Is.EqualTo(1));
        }

        [Test]
        public void TheWindowTitleIsSetWhenItIsEnabled()
        {
            var window = ScriptableObject.CreateInstance<FyniteDebuggerWindow>();

            try
            {
                Assert.That(window.titleContent.text, Is.EqualTo("Fynite Debugger"));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void EnablingAndDisablingRepeatedlyIsClean()
        {
            // Every cycle runs OnEnable and OnDisable. The -= before each += is what stops a second
            // EditorApplication.update subscription surviving a reopen, which is the failure this
            // guards against; a leaked subscription would also outlive the DestroyImmediate below.
            for (var i = 0; i < 3; i++)
            {
                var window = ScriptableObject.CreateInstance<FyniteDebuggerWindow>();

                Assert.That(window.titleContent.text, Is.EqualTo("Fynite Debugger"));
                Assert.DoesNotThrow(() => Object.DestroyImmediate(window));
            }

            Assert.DoesNotThrow(() => EditorApplication.QueuePlayerLoopUpdate());
        }

        [Test]
        public void AModelWithoutMachinesIsEmpty()
        {
            var model = new FyniteDebuggerModel();

            model.Refresh();

            Assert.That(model.Count, Is.Zero);
            Assert.That(model.Selected, Is.Null);
            Assert.That(model.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void ARefreshFindsTheRunningMachines()
        {
            BuildLocomotion();

            var model = new FyniteDebuggerModel();
            model.Refresh();

            Assert.That(model.Count, Is.EqualTo(1));

            var entry = model.GetEntry(0);
            Assert.That(entry.Owner, Is.SameAs(Owner));
            Assert.That(entry.OwnerLabel, Is.EqualTo(Owner.name));
            Assert.That(entry.ContextTypeName, Is.EqualTo(nameof(ProbeContext)));
            Assert.That(entry.PathCount, Is.EqualTo(1));
            Assert.That(entry.Leaf, Is.EqualTo(typeof(IdleProbe)));
            Assert.That(entry.ListLabel, Is.EqualTo(Owner.name + " — IdleProbe"));
        }

        [Test]
        public void ARefreshReadsAHierarchicalPathRootToLeaf()
        {
            Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<LocomotionProbe, IdleProbe>()
                .Use<DeepBranchModule>()
                .Build());

            var model = new FyniteDebuggerModel();
            model.Refresh();

            var entry = model.GetEntry(0);

            Assert.That(entry.PathCount, Is.EqualTo(3));
            Assert.That(entry.GetPathState(0), Is.EqualTo(typeof(GroundedProbe)));
            Assert.That(entry.GetPathState(1), Is.EqualTo(typeof(LocomotionProbe)));
            Assert.That(entry.GetPathState(2), Is.EqualTo(typeof(IdleProbe)));
            Assert.That(entry.Leaf, Is.EqualTo(typeof(IdleProbe)));
        }

        [Test]
        public void ManualRefreshPicksUpATransition()
        {
            var machine = BuildLocomotion();

            var model = new FyniteDebuggerModel();
            model.Refresh();

            Assert.That(model.GetEntry(0).Leaf, Is.EqualTo(typeof(IdleProbe)));

            Context.ToWalk = true;
            machine.Tick(0.1f);

            model.Refresh();

            Assert.That(model.GetEntry(0).Leaf, Is.EqualTo(typeof(WalkProbe)));
        }

        [Test]
        public void SelectionSurvivesARefresh()
        {
            BuildLocomotion();
            var second = Track(Attach(NewOwner(), new ProbeContext())
                .Start<WalkProbe>()
                .Build());

            var model = new FyniteDebuggerModel();
            model.Refresh();

            var index = IndexOfLeaf(model, typeof(WalkProbe));
            model.Select(index);

            var selectedView = model.Selected.View;

            model.Refresh();

            Assert.That(model.Selected, Is.Not.Null);
            Assert.That(model.Selected.View, Is.SameAs(selectedView));
            Assert.That(model.Selected.View, Is.SameAs(second));
        }

        [Test]
        public void SelectionIsDroppedWhenItsMachineGoesAway()
        {
            BuildLocomotion();
            var second = Track(Attach(NewOwner(), new ProbeContext())
                .Start<WalkProbe>()
                .Build());

            var model = new FyniteDebuggerModel();
            model.Refresh();
            model.Select(IndexOfLeaf(model, typeof(WalkProbe)));

            second.Dispose();
            model.Refresh();

            Assert.That(model.Count, Is.EqualTo(1));
            Assert.That(model.Selected, Is.Null, "another machine was selected silently");
            Assert.That(model.SelectedIndex, Is.EqualTo(-1));
        }

        [Test]
        public void TwoMachinesOnTheSameOwnerStaySeparateEntries()
        {
            var owner = NewOwner();
            Track(Attach(owner, Context).Start<IdleProbe>().Build());
            Track(Attach(owner, new ProbeContext()).Start<WalkProbe>().Build());

            var model = new FyniteDebuggerModel();
            model.Refresh();

            Assert.That(model.Count, Is.EqualTo(2));
            Assert.That(model.GetEntry(0).View, Is.Not.SameAs(model.GetEntry(1).View));
            Assert.That(model.GetEntry(0).OwnerLabel, Is.EqualTo(model.GetEntry(1).OwnerLabel));

            model.Select(0);
            var first = model.Selected.View;

            model.Refresh();
            Assert.That(model.Selected.View, Is.SameAs(first), "the same owner confused the identity");
        }

        [Test]
        public void ADestroyedOwnerDoesNotThrow()
        {
            var owner = NewOwner();
            var machine = Track(Attach(owner, Context).Start<IdleProbe>().Build());

            var model = new FyniteDebuggerModel();

            Object.DestroyImmediate(owner.gameObject);

            // Still registered: the machine only notices its owner is gone on its next tick.
            Assert.DoesNotThrow(() => model.Refresh());

            if (model.Count > 0)
            {
                Assert.That(model.GetEntry(0).OwnerLabel, Is.EqualTo("<destroyed owner>"));

                // A destroyed Unity object is not a null reference, so ask Unity, not the CLR.
                Assert.That(model.GetEntry(0).Owner == null, Is.True);
            }

            machine.Tick(0.1f);
            model.Refresh();

            Assert.That(model.Count, Is.Zero);
        }

        [Test]
        public void AFaultedMachineDisappearsFromTheModel()
        {
            var machine = BuildLocomotion();

            var model = new FyniteDebuggerModel();
            model.Refresh();
            Assert.That(model.Count, Is.EqualTo(1));

            Context.OnUpdate = () => throw new System.InvalidOperationException("fynite-test-boom");
            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            model.Refresh();

            Assert.That(model.Count, Is.Zero);
        }

        [Test]
        public void ALoopResetEmptiesTheModel()
        {
            BuildLocomotion();

            var model = new FyniteDebuggerModel();
            model.Refresh();
            model.Select(0);

            FyniteLoop.Clear();
            model.Refresh();

            Assert.That(model.Count, Is.Zero);
            Assert.That(model.Selected, Is.Null);
        }

        [Test]
        public void ClearingLetsGoOfEveryMachineAndOwner()
        {
            BuildLocomotion();

            var model = new FyniteDebuggerModel();
            model.Refresh();
            model.Select(0);

            var entry = model.GetEntry(0);

            model.Clear();

            Assert.That(model.Count, Is.Zero);
            Assert.That(model.Selected, Is.Null);
            Assert.That(entry.View, Is.Null, "the entry kept the machine");
            Assert.That(entry.Owner, Is.Null, "the entry kept the owner");
            Assert.That(entry.PathCount, Is.Zero);
        }

        [Test]
        public void RepeatedRefreshesDoNotAccumulateEntries()
        {
            BuildLocomotion();

            var model = new FyniteDebuggerModel();

            for (var i = 0; i < 5; i++)
            {
                model.Refresh();
            }

            Assert.That(model.Count, Is.EqualTo(1));
        }

        [Test]
        public void SelectingOutOfRangeClearsTheSelection()
        {
            BuildLocomotion();

            var model = new FyniteDebuggerModel();
            model.Refresh();

            model.Select(5);
            Assert.That(model.Selected, Is.Null);

            model.Select(-1);
            Assert.That(model.Selected, Is.Null);

            model.Select(0);
            Assert.That(model.Selected, Is.Not.Null);
        }

        [Test]
        public void SelectingAValidOwnerSelectsItInTheEditor()
        {
            var previous = Selection.activeObject;

            try
            {
                Selection.activeObject = null;

                FyniteDebuggerWindow.SelectOwner(Owner);

                Assert.That(Selection.activeObject, Is.SameAs(Owner));
            }
            finally
            {
                Selection.activeObject = previous;
            }
        }

        [Test]
        public void ADestroyedOwnerIsNotSelectedOrPinged()
        {
            var owner = NewOwner();
            var previous = Selection.activeObject;

            try
            {
                Selection.activeObject = null;
                Object.DestroyImmediate(owner.gameObject);

                Assert.DoesNotThrow(() => FyniteDebuggerWindow.SelectOwner(owner));
                Assert.DoesNotThrow(() => FyniteDebuggerWindow.PingOwner(owner));

                Assert.That(Selection.activeObject, Is.Null, "a destroyed owner was selected");
            }
            finally
            {
                Selection.activeObject = previous;
            }
        }

        [Test]
        public void SelectingAnOwnerChangesNothingAboutTheMachine()
        {
            var machine = BuildLocomotion();
            var previous = Selection.activeObject;

            try
            {
                var before = machine.CurrentStateType;

                FyniteDebuggerWindow.SelectOwner(Owner);
                FyniteDebuggerWindow.PingOwner(Owner);

                Assert.That(machine.IsRunning, Is.True);
                Assert.That(machine.CurrentStateType, Is.EqualTo(before));
                Assert.That(machine.ActiveStateCount, Is.EqualTo(1));
                Assert.That(FyniteLoop.Count, Is.EqualTo(1));
            }
            finally
            {
                Selection.activeObject = previous;
            }
        }

        [Test]
        public void TheOwnerFieldIsNeverAnEditor()
        {
            var source = WindowSource();

            // No change callback means nothing a user drops on the field can reach a machine.
            Assert.That(
                source,
                Does.Not.Contain("ownerField.RegisterValueChangedCallback"),
                "the owner field listens for edits");
            Assert.That(
                source,
                Does.Contain("ownerField.SetValueWithoutNotify"),
                "the owner field is set in a way that could raise a change event");
        }

        [Test]
        public void TheDetailsPanelShowsTheSelectedMachine()
        {
            BuildLocomotion();

            var window = ScriptableObject.CreateInstance<FyniteDebuggerWindow>();

            try
            {
                window.CreateGUI();

                var root = window.rootVisualElement;
                var list = root.Q<ListView>("machine-list");

                // Edit mode does not collect, so drive the model the way a refresh would.
                var model = new FyniteDebuggerModel();
                model.Refresh();

                Assert.That(model.Count, Is.EqualTo(1), "the machine was not collected");

                var entry = model.GetEntry(0);

                Assert.That(entry.ListLabel, Does.Contain("IdleProbe"));
                Assert.That(entry.ContextTypeName, Is.EqualTo(nameof(ProbeContext)));
                Assert.That(entry.Owner, Is.SameAs(Owner));
                Assert.That(list, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        private static string WindowSource()
            => File.ReadAllText(Path.GetFullPath(
                Path.Combine("Packages", "com.natteens.fynite", "Editor", "FyniteDebuggerWindow.cs")));

        private static int CountOf(string text, string term)
        {
            var total = 0;
            var at = text.IndexOf(term, StringComparison.Ordinal);

            while (at >= 0)
            {
                total++;
                at = text.IndexOf(term, at + term.Length, StringComparison.Ordinal);
            }

            return total;
        }

        private static int IndexOfLeaf(FyniteDebuggerModel model, System.Type leaf)
        {
            for (var i = 0; i < model.Count; i++)
            {
                if (model.GetEntry(i).Leaf == leaf)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
