using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>
    /// A step runs code that belongs to the game, and that code can end the machine or leave the
    /// state. The tick that ran the step is still on the stack when it does, and it must not carry on
    /// through a chain that no longer has a run.
    /// </summary>
    public sealed class ActivityCancellationTests : MachineFixture
    {
        [Test]
        public void DisposeFromInsideDo_StopsTheChainThere()
        {
            var machine = Track(Attach().Start<StopInDoProbe>().Build());
            Context.Machine = machine;
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("StopInDoProbe.Update,Begin,Stop,StopInDoProbe.Exit"));
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void DisposeFromInsideDo_LeavesNoSubscriptionBehind()
        {
            var machine = Track(Attach().Start<StopInDoProbe>().Build());
            Context.Machine = machine;

            machine.Tick(0.1f);

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
        }

        [Test]
        public void DisposeFromInsideDo_RunsNoFurtherStateCallback()
        {
            var machine = Track(Attach().Start<StopInDoProbe>().Build());
            Context.Machine = machine;
            Context.Log.Clear();

            machine.Tick(0.1f);
            machine.Tick(0.1f);
            machine.TickFixed(0.02f);

            Assert.That(Context.CountOf("StopInDoProbe.Exit"), Is.EqualTo(1));
            Assert.That(Context.CountOf("StopInDoProbe.Update"), Is.EqualTo(1));
            Assert.That(Context.CountOf("StopInDoProbe.FixedUpdate"), Is.Zero);
        }

        [Test]
        public void ShutdownFromInsideWaitUntil_DoesNotAdvanceTheChain()
        {
            var machine = Track(Attach().Start<StopInUntilProbe>().Build());
            Context.Machine = machine;
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("StopInUntilProbe.Update,Begin,Ask,StopInUntilProbe.Exit"));
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void AResetFromInsideAStepDropsTheRestOfThatRun()
        {
            FyniteActivityExecution<ProbeContext> execution = null;

            execution = new FyniteActivityExecution<ProbeContext>(new[]
            {
                FyniteActivityStep<ProbeContext>.ForDo(context => context.Mark("First")),
                FyniteActivityStep<ProbeContext>.ForDo(context =>
                {
                    context.Mark("Rewind");

                    // What entering the state again does, only from inside the run itself.
                    execution.Reset();
                }),
                FyniteActivityStep<ProbeContext>.ForDo(context => context.Mark("Never"))
            });

            execution.Tick(Context, 0.1f);

            Assert.That(Context.Trace, Is.EqualTo("First,Rewind"));
        }

        [Test]
        public void AResetFromInsideAStepLeavesTheChainAtItsFirstStep()
        {
            FyniteActivityExecution<ProbeContext> execution = null;
            var rewound = false;

            execution = new FyniteActivityExecution<ProbeContext>(new[]
            {
                FyniteActivityStep<ProbeContext>.ForDo(context => context.Mark("First")),
                FyniteActivityStep<ProbeContext>.ForDo(context =>
                {
                    if (rewound)
                    {
                        context.Mark("Second");
                        return;
                    }

                    rewound = true;
                    execution.Reset();
                })
            });

            execution.Tick(Context, 0.1f);
            execution.Tick(Context, 0.1f);

            Assert.That(Context.Trace, Is.EqualTo("First,First,Second"));
        }

        [Test]
        public void ACancelFromInsideAStepDoesNotLetTheNextStepSubscribe()
        {
            FyniteActivityExecution<ProbeContext> execution = null;

            execution = new FyniteActivityExecution<ProbeContext>(new[]
            {
                FyniteActivityStep<ProbeContext>.ForDo(context =>
                {
                    context.Mark("First");
                    execution.Cancel();
                }),
                FyniteActivityStep<ProbeContext>.ForWaitFor(Context.Alpha)
            });

            execution.Tick(Context, 0.1f);

            Assert.That(Context.Trace, Is.EqualTo("First"));
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
        }

        [Test]
        public void AnUnknownStepKindIsRejected()
        {
            var execution = new FyniteActivityExecution<ProbeContext>(new[]
            {
                Internals.StepOfUnknownKind()
            });

            Assert.That(
                () => execution.Tick(Context, 0.1f),
                Throws.InvalidOperationException.With.Message.Contains("unsupported activity step"));
        }

        [Test]
        public void AFinishedChainStaysFinishedUntilTheStateIsEnteredAgain()
        {
            var machine = Track(Attach().Start<ImmediateProbe>().Build());
            Context.Log.Clear();

            machine.Tick(0.1f);
            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("Begin"), Is.EqualTo(1));
            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
        }
    }

    /// <summary>Do, Do (ends the machine), Do, WaitFor, Do — only the first two may ever run.</summary>
    public sealed class StopInDoProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Begin"))
                .Do(context =>
                {
                    context.Mark("Stop");
                    context.Machine.Dispose();
                })
                .Do(context => context.Mark("After"))
                .WaitFor(context => context.Alpha)
                .Do(context => context.Mark("Never"));
    }

    /// <summary>The condition ends the machine and then answers true, which is the worst case.</summary>
    public sealed class StopInUntilProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Begin"))
                .WaitUntil(context =>
                {
                    context.Mark("Ask");
                    context.Machine.Dispose();
                    return true;
                })
                .Do(context => context.Mark("After"));
    }
}
