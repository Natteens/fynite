using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>
    /// The two steps that touch events. <c>WaitFor</c> listens only while it is the active step, and
    /// <c>Publish</c> never turns into a transition inside the tick that published.
    /// </summary>
    public sealed class ActivityEventTests : MachineFixture
    {
        [Test]
        public void WaitFor_IgnoresWhatWasPublishedBeforeItBecameActive()
        {
            var machine = Track(Attach().Start<WaitForProbe>().Build());

            Context.Alpha.Publish();
            Context.Log.Clear();

            machine.Tick(0.1f);
            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("Begin"), Is.EqualTo(1));
            Assert.That(Context.CountOf("End"), Is.Zero, "a publication from before the wait counted");
        }

        [Test]
        public void WaitFor_CompletesOnAPublicationWhileActive()
        {
            var machine = Track(Attach().Start<WaitForProbe>().Build());

            machine.Tick(0.1f);
            Assert.That(Context.CountOf("End"), Is.Zero);

            Context.Alpha.Publish();
            Assert.That(Context.CountOf("End"), Is.Zero, "Publish ran the next step on the spot");

            machine.Tick(0.1f);
            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
        }

        [Test]
        public void WaitFor_SubscribesOnlyWhileItIsTheActiveStep()
        {
            var machine = Track(Attach().Start<WaitForProbe>().Build());

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero, "listening started before the step");

            machine.Tick(0.1f);
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero, "the finished step kept listening");
        }

        [Test]
        public void WaitFor_StopsListeningWhenTheActivityIsCancelled()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<WaitForModule>()
                .Build());

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Context.ToWalk = false;

            Assert.That(machine.IsIn<WaitForProbe>(), Is.True);
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            Context.ToIdle = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
        }

        [Test]
        public void WaitFor_TreatsRepeatedPublicationsAsOne()
        {
            var machine = Track(Attach().Start<TwiceWaitForProbe>().Build());

            machine.Tick(0.1f);

            Context.Alpha.Publish();
            Context.Alpha.Publish();
            Context.Alpha.Publish();

            machine.Tick(0.1f);

            Assert.That(Context.CountOf("Middle"), Is.EqualTo(1));
            Assert.That(Context.CountOf("End"), Is.Zero, "three publications passed two waits");

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
        }

        [Test]
        public void WaitFor_ListensAgainOnASecondEntry()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<WaitForModule>()
                .Build());

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("End"), Is.EqualTo(1));

            Context.ToWalk = false;
            Context.ToIdle = true;
            machine.Tick(0.1f);
            Context.ToIdle = false;
            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WaitForProbe>(), Is.True);
            Assert.That(Context.CountOf("Begin"), Is.EqualTo(2));
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1), "the second entry is not listening");

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("End"), Is.EqualTo(2));
        }

        [Test]
        public void Publish_HappensExactlyOnce()
        {
            var counter = new CountingSink();
            Context.Beta.Subscribe(counter, 0);

            var machine = Track(Attach().Start<PublishProbe>().Build());

            for (var i = 0; i < 5; i++)
            {
                machine.Tick(0.1f);
            }

            Assert.That(counter.Signals, Is.EqualTo(1));

            Context.Beta.Unsubscribe(counter);
        }

        [Test]
        public void Publish_TransitionsOnlyOnALaterCycle()
        {
            var machine = Track(Attach()
                .Start<PublishProbe>()
                .Use<PublishModule>()
                .Build());

            machine.Tick(0.1f);

            Assert.That(
                machine.IsIn<PublishProbe>(),
                Is.True,
                "the activity transitioned inside its own tick");
            Assert.That(Context.CountOf("Begin"), Is.EqualTo(1));

            machine.Tick(0.1f);

            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        [Test]
        public void PublishAfterAWait_TransitionsOnTheCycleAfterTheWaitEnds()
        {
            var machine = Track(Attach()
                .Start<TimedPublishProbe>()
                .Use<TimedPublishModule>()
                .Build());

            machine.Tick(0.2f);
            machine.Tick(0.2f);
            Assert.That(machine.IsIn<TimedPublishProbe>(), Is.True);

            // The wait ends here and the event is published, but the transition is not resolved yet.
            machine.Tick(0.2f);
            Assert.That(machine.IsIn<TimedPublishProbe>(), Is.True);

            machine.Tick(0.2f);
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        /// <summary>Counts publications straight from the source, without a machine in the way.</summary>
        private sealed class CountingSink : IFyniteEventSink
        {
            public int Signals;

            public void Signal(int slot) => Signals++;
        }
    }
}
