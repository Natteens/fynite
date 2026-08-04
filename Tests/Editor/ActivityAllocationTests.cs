using Fynite;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace FyniteTests
{
    /// <summary>
    /// Everything an activity needs is compiled by <c>Build()</c>, so running, waiting, cancelling and
    /// starting over allocate nothing. The states here count instead of logging, because the probes
    /// used everywhere else build a string per callback.
    /// </summary>
    public sealed class ActivityAllocationTests : MachineFixture
    {
        [Test]
        public void StateWithoutActivity_DoesNotAllocate()
        {
            var machine = Track(Attach().Start<QuietPlain>().Build());

            machine.Tick(0.1f);

            Assert.That(
                () =>
                {
                    machine.Tick(0.1f);
                    machine.TickFixed(0.02f);
                },
                Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ImmediateStepsPublishCancelAndReentry_DoNotAllocate()
        {
            var listener = new CountingSink();
            Context.Beta.Subscribe(listener, 0);

            var machine = Track(Attach()
                .Start<QuietLoop>()
                .Use<QuietLoopModule>()
                .Build());

            // Every tick cancels the chain, re-enters the state and runs it from the first step.
            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(() => machine.Tick(0.1f), Is.Not.AllocatingGCMemory());
            Assert.That(listener.Signals, Is.GreaterThan(1));

            Context.Beta.Unsubscribe(listener);
        }

        [Test]
        public void Wait_DoesNotAllocate()
        {
            var machine = Track(Attach().Start<QuietWait>().Build());

            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(() => machine.Tick(0.1f), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void WaitUntil_DoesNotAllocate()
        {
            var machine = Track(Attach().Start<QuietUntil>().Build());

            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(() => machine.Tick(0.1f), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void WaitForAndItsPublication_DoNotAllocate()
        {
            var machine = Track(Attach().Start<QuietWaitFor>().Build());

            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(
                () =>
                {
                    Context.Alpha.Publish();
                    machine.Tick(0.1f);
                },
                Is.Not.AllocatingGCMemory());
        }

        /// <summary>
        /// The check that notices a step cancelled its own run is one integer compare, taken on the
        /// tick that was interrupted. It must not be the one thing that allocates.
        /// </summary>
        [Test]
        public void TheCancellationCheck_DoesNotAllocate()
        {
            FyniteActivityExecution<ProbeContext> execution = null;

            execution = new FyniteActivityExecution<ProbeContext>(new[]
            {
                FyniteActivityStep<ProbeContext>.ForDo(context =>
                {
                    context.Conditions++;
                    execution.Cancel();
                }),
                FyniteActivityStep<ProbeContext>.ForDo(context => context.Conditions++)
            });

            execution.Tick(Context, 0.1f);

            Assert.That(() => execution.Tick(Context, 0.1f), Is.Not.AllocatingGCMemory());
            Assert.That(Context.Conditions, Is.EqualTo(2));
        }

        private sealed class CountingSink : IFyniteEventSink
        {
            public int Signals;

            public void Signal(int slot) => Signals++;
        }

        private sealed class QuietPlain : FyniteState<ProbeContext>
        {
            protected override void Update() => Context.Conditions++;
        }

        private sealed class QuietLoop : FyniteState<ProbeContext>
        {
            protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
                => activity
                    .Do(context => context.Conditions++)
                    .Publish(context => context.Beta)
                    .Do(context => context.Conditions++);
        }

        private sealed class QuietWait : FyniteState<ProbeContext>
        {
            protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
                => activity.Wait(1000f).Do(context => context.Conditions++);
        }

        private sealed class QuietUntil : FyniteState<ProbeContext>
        {
            protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
                => activity.WaitUntil(context => context.Unlocked);
        }

        private sealed class QuietWaitFor : FyniteState<ProbeContext>
        {
            protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
                => activity.WaitFor(context => context.Alpha).Do(context => context.Conditions++);
        }

        private sealed class QuietLoopModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<QuietLoop>().To<QuietLoop>().When<Always>();
        }
    }
}
