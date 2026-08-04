using System.Collections;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FyniteTests
{
    /// <summary>
    /// A subsystem reset under the real PlayerLoop, which is what a domain reload and leaving play
    /// mode both come down to: everything alive has to end, and the next session has to start clean.
    /// </summary>
    public sealed class PlayModeShutdownTests
    {
        private GameObject owner;
        private ResetContext context;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("FynitePlayModeResetOwner");
            context = new ResetContext();
        }

        [TearDown]
        public void TearDown()
        {
            FyniteLoop.Clear();

            if (owner != null)
            {
                Object.Destroy(owner);
            }

            // The reset removed the systems along with the machines; put them back for the next test.
            FyniteLoopBootstrapper.Install();
        }

        [UnityTest]
        public IEnumerator BootstrapEndsARunningMachine()
        {
            var machine = Build();

            yield return null;

            Assert.That(context.Updates, Is.GreaterThan(0));

            FyniteLoopBootstrapper.Bootstrap();

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(machine.CurrentStateType, Is.Null);
            Assert.That(context.Exits, Is.EqualTo(1));
            Assert.That(FyniteLoop.Count, Is.Zero);

            var after = context.Updates;

            yield return null;
            yield return null;

            Assert.That(context.Updates, Is.EqualTo(after), "the machine kept updating after the reset");
        }

        [UnityTest]
        public IEnumerator BootstrapExitsAHierarchyLeafFirst()
        {
            var branchContext = new ResetBranchContext();
            Machine
                .Attach(owner, branchContext)
                .Start<ResetShellState>()
                .Child<ResetShellState, ResetChildState>()
                .Build();

            yield return null;

            Assert.That(branchContext.Order, Is.Empty);

            FyniteLoopBootstrapper.Bootstrap();

            Assert.That(branchContext.Order, Is.EqualTo("child,shell"));
        }

        [UnityTest]
        public IEnumerator BootstrapRemovesEventSubscriptions()
        {
            Build();

            yield return null;

            Assert.That(context.Poke.SubscriberCount, Is.EqualTo(1), "the transition is not listening");
            Assert.That(context.Release.SubscriberCount, Is.EqualTo(1), "the activity is not listening");

            FyniteLoopBootstrapper.Bootstrap();

            Assert.That(context.Poke.SubscriberCount, Is.Zero);
            Assert.That(context.Release.SubscriberCount, Is.Zero);

            var after = context.Updates;
            Assert.DoesNotThrow(() => context.Poke.Publish());
            Assert.DoesNotThrow(() => context.Release.Publish());

            yield return null;

            Assert.That(context.Updates, Is.EqualTo(after));
        }

        [UnityTest]
        public IEnumerator ANewMachineRunsAfterTheReset()
        {
            var stale = Build();

            yield return null;

            FyniteLoopBootstrapper.Bootstrap();

            var freshContext = new ResetContext();
            var fresh = Machine
                .Attach(owner, freshContext)
                .Start<ResetIdleState>()
                .Use<ResetTransitions>()
                .Build();

            yield return null;
            yield return null;

            Assert.That(freshContext.Updates, Is.GreaterThan(0));
            Assert.That(FyniteLoop.Count, Is.EqualTo(1));

            freshContext.Poke.Publish();

            yield return null;

            Assert.That(fresh.IsIn<ResetTargetState>(), Is.True);
            Assert.That(stale.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator BootstrapEndsAMachineWhoseOwnerIsDisabled()
        {
            var machine = Build();

            yield return null;

            owner.SetActive(false);

            yield return null;

            var paused = context.Updates;

            FyniteLoopBootstrapper.Bootstrap();

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(context.Exits, Is.EqualTo(1), "a paused machine was dropped without exiting");
            Assert.That(context.Updates, Is.EqualTo(paused));
            Assert.That(context.Poke.SubscriberCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BootstrapEndsAPendingMachine()
        {
            var lateContext = new ResetContext();
            var lateOwner = new GameObject("FynitePlayModeLateOwner");

            try
            {
                var machine = Build();
                FyniteMachine<ResetContext> late = null;

                // Built from inside another machine's Update, so it is still pending when the reset
                // is asked for in the very same cycle.
                context.OnUpdate = () =>
                {
                    context.OnUpdate = null;
                    late = Machine.Attach(lateOwner, lateContext).Start<ResetIdleState>().Build();
                    FyniteLoop.Clear();
                };

                yield return null;
                yield return null;

                Assert.That(late, Is.Not.Null);
                Assert.That(late.IsRunning, Is.False);
                Assert.That(lateContext.Updates, Is.Zero, "the pending machine received an update");
                Assert.That(lateContext.Exits, Is.EqualTo(1));
                Assert.That(machine.IsRunning, Is.False);
                Assert.That(FyniteLoop.Count, Is.Zero);
            }
            finally
            {
                Object.Destroy(lateOwner);
            }
        }

        private FyniteMachine<ResetContext> Build()
            => Machine
                .Attach(owner, context)
                .Start<ResetIdleState>()
                .Use<ResetTransitions>()
                .Build();
    }

    public sealed class ResetContext
    {
        public readonly FyniteEvent Poke = new FyniteEvent();
        public readonly FyniteEvent Release = new FyniteEvent();

        public int Updates;
        public int Exits;

        public System.Action OnUpdate;
    }

    public sealed class ResetIdleState : FyniteState<ResetContext>
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ResetContext> activity)
            => activity.WaitFor(context => context.Release);

        protected override void Update()
        {
            Context.Updates++;
            Context.OnUpdate?.Invoke();
        }

        protected override void Exit() => Context.Exits++;
    }

    public sealed class ResetTargetState : FyniteState<ResetContext>
    {
        protected override void Update() => Context.Updates++;
    }

    public sealed class ResetTransitions : IFyniteTransitions<ResetContext>
    {
        public void Configure(FyniteTransitions<ResetContext> transitions)
            => transitions.From<ResetIdleState, ResetTargetState>().On(context => context.Poke);
    }

    public sealed class ResetBranchContext
    {
        public string Order = string.Empty;

        public void Leaving(string state)
            => Order = Order.Length == 0 ? state : Order + "," + state;
    }

    public sealed class ResetShellState : FyniteState<ResetBranchContext>
    {
        protected override void Exit() => Context.Leaving("shell");
    }

    public sealed class ResetChildState : FyniteState<ResetBranchContext>
    {
        protected override void Exit() => Context.Leaving("child");
    }
}
