using System;
using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>How <c>Build()</c> resolves selectors and subscribes, and how it refuses to.</summary>
    public sealed class EventBuildTests : MachineFixture
    {
        [Test]
        public void Selector_RunsExactlyOnceDuringBuild()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<CountedAlphaModule>().Build());

            Assert.That(Context.Lookups, Is.EqualTo(1));

            Context.Alpha.Publish();
            machine.Tick(0.1f);
            machine.Tick(0.1f);
            machine.TickFixed(0.02f);
            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(Context.Lookups, Is.EqualTo(1), "the selector ran again after Build()");
        }

        [Test]
        public void NullSelector_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => Attach().Start<IdleProbe>().Use<NullSelectorModule>().Build());
        }

        [Test]
        public void NullSource_IsRejected()
        {
            var failure = Assert.Throws<InvalidOperationException>(
                () => Attach().Start<IdleProbe>().Use<AlphaThenMissingModule>().Build());

            Assert.That(failure.Message, Does.Contain("null event source"));
            Assert.That(failure.Message, Does.Contain("WalkProbe"));
            Assert.That(failure.Message, Does.Contain("RunProbe"));
        }

        [Test]
        public void SameSourceInSeveralTransitions_SubscribesOnce()
        {
            Track(Attach().Start<IdleProbe>().Use<AlphaTwiceModule>().Build());

            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));
        }

        [Test]
        public void DifferentSources_SubscribeSeparately()
        {
            Track(Attach().Start<IdleProbe>().Use<AlphaThenBetaModule>().Build());

            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));
            Assert.That(Context.Beta.SubscriberCount, Is.EqualTo(1));
            Assert.That(Context.Gamma.SubscriberCount, Is.Zero);
        }

        [Test]
        public void FailedBuild_LeavesNoSubscription()
        {
            Assert.Throws<InvalidOperationException>(
                () => Attach().Start<IdleProbe>().Use<AlphaThenMissingModule>().Build());

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(FyniteLoop.Count, Is.Zero);

            Assert.DoesNotThrow(() => Context.Alpha.Publish());
        }

        [Test]
        public void FailingInitialEnter_RemovesSubscriptions()
        {
            Context.ThrowOn = "IdleProbe.Enter";

            Assert.Throws<InvalidOperationException>(
                () => Attach().Start<IdleProbe>().Use<AlphaModule>().Build());

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void EventsPublishedDuringTheInitialEnter_Wait()
        {
            var published = false;
            Context.OnEnter = () =>
            {
                if (published)
                {
                    return;
                }

                published = true;
                Context.Alpha.Publish();
            };

            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());

            Assert.That(machine.IsIn<IdleProbe>(), Is.True, "Build() ran a transition");

            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void On_IsRejectedAfterBuild()
        {
            Track(Attach()
                .Start<IdleProbe>()
                .Use<CapturingModule>()
                .Build());

            var captured = CapturingModule.Captured;

            Assert.Throws<InvalidOperationException>(
                () => captured.From<IdleProbe>().To<WalkProbe>().On(context => context.Alpha));
        }

        private sealed class CapturingModule : IFyniteTransitions<ProbeContext>
        {
            public static FyniteTransitions<ProbeContext> Captured;

            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                Captured = transitions;
                transitions.From<IdleProbe>().To<WalkProbe>().On(context => context.Alpha);
            }
        }
    }
}
