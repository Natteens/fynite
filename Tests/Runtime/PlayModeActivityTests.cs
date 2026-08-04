using System.Collections;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FyniteTests
{
    /// <summary>
    /// Activities under the real PlayerLoop: real frames, real <c>DeltaTime</c>, and no manual tick
    /// anywhere in the test.
    /// </summary>
    public sealed class PlayModeActivityTests
    {
        private GameObject owner;
        private ChoreContext context;
        private FyniteMachine<ChoreContext> machine;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("FynitePlayModeActivityOwner");
            context = new ChoreContext();
        }

        [TearDown]
        public void TearDown()
        {
            machine?.Dispose();
            machine = null;

            if (owner != null)
            {
                Object.Destroy(owner);
            }
        }

        [UnityTest]
        public IEnumerator TimedChainRunsOnTheLoop()
        {
            machine = BuildTimed();

            yield return null;

            Assert.That(context.Begun, Is.EqualTo(1), "the first step did not run on the first update");

            yield return new WaitForSeconds(0.5f);

            Assert.That(context.Landed, Is.EqualTo(1));
            Assert.That(context.Begun, Is.EqualTo(1), "the chain ran more than once");
        }

        [UnityTest]
        public IEnumerator ChainCompletionPublishesAndTransitions()
        {
            machine = BuildTimed();

            yield return new WaitForSeconds(0.5f);
            yield return null;

            Assert.That(machine.IsIn<ChoreDoneState>(), Is.True);
        }

        [UnityTest]
        public IEnumerator WaitForFinishesOnAPublication()
        {
            machine = BuildWaiting();

            yield return null;

            Assert.That(context.Begun, Is.EqualTo(1));
            Assert.That(context.Released.SubscriberCount, Is.EqualTo(1));

            yield return null;
            yield return null;

            Assert.That(context.Landed, Is.Zero, "the chain moved on without a publication");

            context.Released.Publish();

            yield return null;

            Assert.That(context.Landed, Is.EqualTo(1));
            Assert.That(context.Released.SubscriberCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TransitionOutCancelsTheChain()
        {
            machine = BuildWaiting();

            yield return null;

            Assert.That(context.Released.SubscriberCount, Is.EqualTo(1));

            context.Abort.Publish();

            yield return null;

            Assert.That(machine.IsIn<ChoreDoneState>(), Is.True);
            Assert.That(context.Released.SubscriberCount, Is.Zero);
            Assert.That(context.Landed, Is.Zero, "the cancelled chain kept going");
        }

        [UnityTest]
        public IEnumerator DisabledOwnerFreezesTheChainAndResumes()
        {
            machine = BuildTimed();

            yield return null;

            owner.SetActive(false);

            yield return new WaitForSeconds(0.5f);

            Assert.That(context.Landed, Is.Zero, "the wait ran while the owner was disabled");

            owner.SetActive(true);

            yield return new WaitForSeconds(0.5f);

            Assert.That(context.Landed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ParentChainSurvivesItsChildren()
        {
            var branchContext = new ChoreBranchContext();
            var branchMachine = Machine
                .Attach(owner, branchContext)
                .Start<ChoreShellState>()
                .Child<ChoreShellState, ChoreFirstState>()
                .Child<ChoreShellState, ChoreSecondState>()
                .Use<ChoreBranchTransitions>()
                .Build();

            try
            {
                yield return null;

                Assert.That(branchContext.ShellBegun, Is.EqualTo(1));
                Assert.That(branchContext.FirstBegun, Is.EqualTo(1));
                Assert.That(branchContext.Never.SubscriberCount, Is.EqualTo(1));

                branchContext.Swap.Publish();

                yield return null;

                Assert.That(branchMachine.IsIn<ChoreSecondState>(), Is.True);
                Assert.That(branchContext.SecondBegun, Is.EqualTo(1));
                Assert.That(branchContext.ShellBegun, Is.EqualTo(1), "the parent chain restarted");
                Assert.That(
                    branchContext.Never.SubscriberCount,
                    Is.EqualTo(1),
                    "the parent stopped listening");
            }
            finally
            {
                branchMachine.Dispose();
            }
        }

        private FyniteMachine<ChoreContext> BuildTimed()
            => Machine
                .Attach(owner, context)
                .Start<ChoreTimedState>()
                .Use<ChoreTransitions>()
                .Build();

        private FyniteMachine<ChoreContext> BuildWaiting()
            => Machine
                .Attach(owner, context)
                .Start<ChoreWaitingState>()
                .Use<ChoreTransitions>()
                .Build();
    }

    public sealed class ChoreContext
    {
        public readonly FyniteEvent Finished = new FyniteEvent();
        public readonly FyniteEvent Released = new FyniteEvent();
        public readonly FyniteEvent Abort = new FyniteEvent();

        public int Begun;
        public int Landed;
    }

    public sealed class ChoreTimedState : FyniteState<ChoreContext>
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ChoreContext> activity)
            => activity
                .Do(context => context.Begun++)
                .Wait(0.25f)
                .Do(context => context.Landed++)
                .Publish(context => context.Finished);
    }

    public sealed class ChoreWaitingState : FyniteState<ChoreContext>
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ChoreContext> activity)
            => activity
                .Do(context => context.Begun++)
                .WaitFor(context => context.Released)
                .Do(context => context.Landed++)
                .Publish(context => context.Finished);
    }

    public sealed class ChoreDoneState : FyniteState<ChoreContext>
    {
    }

    public sealed class ChoreTransitions : IFyniteTransitions<ChoreContext>
    {
        public void Configure(FyniteTransitions<ChoreContext> transitions)
        {
            transitions.Any().To<ChoreDoneState>().On(context => context.Finished);
            transitions.Any().To<ChoreDoneState>().On(context => context.Abort);
        }
    }

    public sealed class ChoreBranchContext
    {
        public readonly FyniteEvent Swap = new FyniteEvent();
        public readonly FyniteEvent Never = new FyniteEvent();

        public int ShellBegun;
        public int FirstBegun;
        public int SecondBegun;
    }

    public sealed class ChoreShellState : FyniteState<ChoreBranchContext>
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ChoreBranchContext> activity)
            => activity
                .Do(context => context.ShellBegun++)
                .WaitFor(context => context.Never);
    }

    public sealed class ChoreFirstState : FyniteState<ChoreBranchContext>
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ChoreBranchContext> activity)
            => activity.Do(context => context.FirstBegun++);
    }

    public sealed class ChoreSecondState : FyniteState<ChoreBranchContext>
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ChoreBranchContext> activity)
            => activity.Do(context => context.SecondBegun++);
    }

    public sealed class ChoreBranchTransitions : IFyniteTransitions<ChoreBranchContext>
    {
        public void Configure(FyniteTransitions<ChoreBranchContext> transitions)
            => transitions.From<ChoreFirstState>().To<ChoreSecondState>().On(context => context.Swap);
    }
}
