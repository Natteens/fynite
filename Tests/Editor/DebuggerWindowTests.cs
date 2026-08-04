using System;
using System.IO;
using System.Text.RegularExpressions;
using Fynite;
using FyniteEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
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
        public void TheMenuOpensTheWindow()
        {
            FyniteDebuggerWindow window = null;

            // Showing a window on a batchmode editor logs that there is no graphics device. That is
            // the environment talking, not the window, so it is tolerated here and nowhere else.
            LogAssert.ignoreFailingMessages = true;

            try
            {
                EditorApplication.ExecuteMenuItem("Window/Fynite/Debugger");

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
        public void TheOwnerRowPingsAndIsNotDisabled()
        {
            var source = File.ReadAllText(Path.GetFullPath(
                Path.Combine("Packages", "com.natteens.fynite", "Editor", "FyniteDebuggerWindow.cs")));

            Assert.That(
                source,
                Does.Contain("EditorGUIUtility.PingObject"),
                "the window never pings the owner");

            var field = source.IndexOf("ObjectField(\"Owner\"", StringComparison.Ordinal);
            Assert.That(field, Is.GreaterThan(0), "the owner field moved or was renamed");

            // The only DisabledScope near the owner row must be the one wrapping the two buttons, so
            // the field itself stays interactive.
            var rowStart = source.LastIndexOf("private static void DrawOwner", StringComparison.Ordinal);
            Assert.That(rowStart, Is.GreaterThan(0));

            var beforeField = source.Substring(rowStart, field - rowStart);

            Assert.That(
                beforeField,
                Does.Not.Contain("DisabledScope"),
                "the owner ObjectField is inside a DisabledScope again");
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
