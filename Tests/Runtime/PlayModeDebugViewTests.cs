#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FyniteTests
{
    /// <summary>
    /// What the Editor debugger would see while the real PlayerLoop is running: a live machine, its
    /// path changing frame to frame, and nothing left once the session ends.
    /// </summary>
    public sealed class PlayModeDebugViewTests
    {
        private readonly List<IFyniteDebugView> views = new List<IFyniteDebugView>();

        private GameObject owner;
        private WatchContext context;
        private FyniteMachine<WatchContext> machine;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("FynitePlayModeDebugOwner");
            context = new WatchContext();
        }

        [TearDown]
        public void TearDown()
        {
            views.Clear();

            machine?.Dispose();
            machine = null;

            if (owner != null)
            {
                Object.Destroy(owner);
            }
        }

        [UnityTest]
        public IEnumerator ALiveMachineIsReportedWithItsPath()
        {
            machine = Build();

            yield return null;

            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Has.Count.EqualTo(1));

            var view = views[0];
            Assert.That(view.DebugIsRunning, Is.True);
            Assert.That(view.DebugOwner, Is.SameAs(owner));
            Assert.That(view.DebugContextType, Is.EqualTo(typeof(WatchContext)));
            Assert.That(view.DebugActiveStateCount, Is.EqualTo(2));
            Assert.That(view.DebugGetActiveStateType(0), Is.EqualTo(typeof(WatchShellState)));
            Assert.That(view.DebugGetActiveStateType(1), Is.EqualTo(typeof(WatchIdleState)));
        }

        [UnityTest]
        public IEnumerator TheReportedPathFollowsARealTransition()
        {
            machine = Build();

            yield return null;

            context.Move.Publish();

            yield return null;

            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Has.Count.EqualTo(1));
            Assert.That(
                views[0].DebugGetActiveStateType(views[0].DebugActiveStateCount - 1),
                Is.EqualTo(typeof(WatchBusyState)));
            Assert.That(views[0].DebugGetActiveStateType(0), Is.EqualTo(typeof(WatchShellState)));
        }

        [UnityTest]
        public IEnumerator ADisposedMachineStopsBeingReported()
        {
            machine = Build();

            yield return null;

            machine.Dispose();
            machine = null;

            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ASubsystemResetLeavesNothingToReport()
        {
            machine = Build();

            yield return null;

            FyniteLoopBootstrapper.Bootstrap();

            FyniteLoop.CollectDebugViews(views);

            Assert.That(views, Is.Empty);

            // The loop is usable again for the next session.
            FyniteLoopBootstrapper.Install();
        }

        private FyniteMachine<WatchContext> Build()
            => Machine
                .Attach(owner, context)
                .Start<WatchShellState>()
                .Child<WatchShellState, WatchIdleState>()
                .Child<WatchShellState, WatchBusyState>()
                .Use<WatchTransitions>()
                .Build();
    }

    public sealed class WatchContext
    {
        public readonly FyniteEvent Move = new FyniteEvent();
    }

    public sealed class WatchShellState : FyniteState<WatchContext>
    {
    }

    public sealed class WatchIdleState : FyniteState<WatchContext>
    {
    }

    public sealed class WatchBusyState : FyniteState<WatchContext>
    {
    }

    public sealed class WatchTransitions : IFyniteTransitions<WatchContext>
    {
        public void Configure(FyniteTransitions<WatchContext> transitions)
            => transitions.From<WatchIdleState, WatchBusyState>().On(context => context.Move);
    }
}
#endif
