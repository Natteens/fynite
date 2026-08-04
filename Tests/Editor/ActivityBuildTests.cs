using System;
using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>What <c>Build()</c> compiles, and everything it refuses to compile.</summary>
    public sealed class ActivityBuildTests : MachineFixture
    {
        [Test]
        public void ConfigureActivity_RunsExactlyOncePerState()
        {
            var machine = Track(Attach()
                .Start<ImmediateProbe>()
                .Use<ImmediateModule>()
                .Build());

            Assert.That(ImmediateProbe.Configured, Is.EqualTo(1));

            Context.ToIdle = true;
            machine.Tick(0.1f);
            Context.ToIdle = false;
            Context.ToWalk = true;
            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<ImmediateProbe>(), Is.True);
            Assert.That(ImmediateProbe.Configured, Is.EqualTo(1), "the activity was rebuilt");
        }

        [Test]
        public void ConfigureActivity_RunsOncePerMachine()
        {
            Track(Attach().Start<ImmediateProbe>().Build());
            Track(Attach(NewOwner(), new ProbeContext()).Start<ImmediateProbe>().Build());

            Assert.That(ImmediateProbe.Configured, Is.EqualTo(2));
        }

        [Test]
        public void StateWithoutActivity_StillWorks()
        {
            var machine = BuildLocomotion();
            Context.Log.Clear();

            machine.Tick(0.1f);
            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("IdleProbe.Update,IdleProbe.Exit,WalkProbe.Enter,WalkProbe.Update"));
        }

        [Test]
        public void WaitForSelector_RunsExactlyOnceDuringBuild()
        {
            var machine = Track(Attach().Start<CountedWaitForProbe>().Build());

            Assert.That(Context.Lookups, Is.EqualTo(1));

            machine.Tick(0.1f);
            machine.Tick(0.1f);
            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(Context.Lookups, Is.EqualTo(1), "the selector ran again after Build()");
        }

        [Test]
        public void NullDo_IsRejected()
            => Assert.Throws<ArgumentNullException>(() => Attach().Start<NullDoProbe>().Build());

        [Test]
        public void NegativeWait_IsRejected()
            => Assert.Throws<ArgumentOutOfRangeException>(
                () => Attach().Start<NegativeWaitProbe>().Build());

        [Test]
        public void NotANumberWait_IsRejected()
            => Assert.Throws<ArgumentOutOfRangeException>(
                () => Attach().Start<NanWaitProbe>().Build());

        [Test]
        public void InfiniteWait_IsRejected()
            => Assert.Throws<ArgumentOutOfRangeException>(
                () => Attach().Start<InfiniteWaitProbe>().Build());

        [Test]
        public void NullWaitUntil_IsRejected()
            => Assert.Throws<ArgumentNullException>(() => Attach().Start<NullUntilProbe>().Build());

        [Test]
        public void NullWaitForSelector_IsRejected()
            => Assert.Throws<ArgumentNullException>(() => Attach().Start<NullWaitForProbe>().Build());

        [Test]
        public void NullWaitForSource_IsRejected()
        {
            var failure = Assert.Throws<InvalidOperationException>(
                () => Attach().Start<MissingWaitForProbe>().Build());

            Assert.That(failure.Message, Does.Contain("null event source"));
            Assert.That(failure.Message, Does.Contain("MissingWaitForProbe"));
            Assert.That(failure.Message, Does.Contain("WaitFor"));
        }

        [Test]
        public void NullPublishSelector_IsRejected()
            => Assert.Throws<ArgumentNullException>(() => Attach().Start<NullPublishProbe>().Build());

        [Test]
        public void NullPublishSource_IsRejected()
        {
            var failure = Assert.Throws<InvalidOperationException>(
                () => Attach().Start<MissingPublishProbe>().Build());

            Assert.That(failure.Message, Does.Contain("null event source"));
            Assert.That(failure.Message, Does.Contain("Publish"));
        }

        [Test]
        public void FailedBuild_LeavesNoSubscriptionAndNoMachineInTheLoop()
        {
            // The transition subscribes to Alpha first; the activity of the state then fails.
            Assert.Throws<InvalidOperationException>(
                () => Attach()
                    .Start<IdleProbe>()
                    .Use<AlphaModule>()
                    .Use<MissingWaitForModule>()
                    .Build());

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(FyniteLoop.Count, Is.Zero);
            Assert.DoesNotThrow(() => Context.Alpha.Publish());
        }

        [Test]
        public void FailingInitialEnter_LeavesNoActivitySubscription()
        {
            Context.ThrowOn = "WaitForProbe.Enter";

            Assert.Throws<InvalidOperationException>(() => Attach().Start<WaitForProbe>().Build());

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void ActivityDoesNotStartDuringBuild()
        {
            Track(Attach().Start<ImmediateProbe>().Build());

            Assert.That(Context.Trace, Is.EqualTo("ImmediateProbe.Enter"), "a step ran during Build()");
        }

        /// <summary>
        /// A state that declares nothing costs nothing: no draft list, and no compiled chain for the
        /// state to tick.
        /// </summary>
        [Test]
        public void AStateThatDeclaresNoStepBuildsNoDraftList()
        {
            var builder = new FyniteActivityBuilder<ProbeContext>(typeof(IdleProbe));

            Assert.That(builder.HasDrafts, Is.False);
            Assert.That(builder.Compile(Context), Is.Null);

            builder.Do(context => context.Mark("Begin"));

            Assert.That(builder.HasDrafts, Is.True);
            Assert.That(builder.Compile(Context), Is.Not.Null);
        }

        private sealed class MissingWaitForModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe>().To<MissingWaitForProbe>().When<ToWalk>();
        }
    }
}
