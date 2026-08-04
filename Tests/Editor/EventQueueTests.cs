using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>
    /// Publishing from inside a callback must never resolve there and then: every one of these
    /// occurrences has to survive until the next safe update.
    /// </summary>
    public sealed class EventQueueTests : MachineFixture
    {
        [Test]
        public void EventPublishedDuringUpdate_Waits()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());

            var published = false;
            Context.OnUpdate = () =>
            {
                if (published)
                {
                    return;
                }

                published = true;
                Context.Alpha.Publish();
            };

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void EventPublishedDuringExit_Waits()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<WalkOnlyAlphaModule>().Build());

            Context.OnExit = () => Context.Alpha.Publish();
            Context.ToWalk = true;

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True, "the Exit publication resolved too early");

            Context.OnExit = null;
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<RunProbe>(), Is.True);
        }

        [Test]
        public void EventPublishedDuringEnter_Waits()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<WalkOnlyAlphaModule>().Build());

            Context.OnEnter = () => Context.Alpha.Publish();
            Context.ToWalk = true;

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True, "the Enter publication resolved too early");

            Context.OnEnter = null;
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<RunProbe>(), Is.True);
        }

        [Test]
        public void EventPublishedDuringPredicate_Waits()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<PublishingPredicateModule>()
                .Build());

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<IdleProbe>(), Is.True, "the predicate publication resolved too early");

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void EventPublishedDuringFixedUpdate_Waits()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());

            var published = false;
            Context.OnFixedUpdate = () =>
            {
                if (published)
                {
                    return;
                }

                published = true;
                Context.Alpha.Publish();
            };

            machine.TickFixed(0.02f);
            machine.TickFixed(0.02f);

            Assert.That(machine.IsIn<IdleProbe>(), Is.True, "FixedUpdate resolved an event");

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void FixedUpdate_NeitherConsumesNorResolvesEvents()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());

            Context.Alpha.Publish();

            machine.TickFixed(0.02f);
            machine.TickFixed(0.02f);
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True, "FixedUpdate ate the event");
        }

        [Test]
        public void EventPublishedWhileHandlingAnother_Waits()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaTwiceModule>().Build());

            // Entering Walk republishes Alpha; that occurrence must not be handled in the same cycle.
            Context.OnEnter = () => Context.Alpha.Publish();
            Context.Alpha.Publish();

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);

            Context.OnEnter = null;
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<RunProbe>(), Is.True);
        }

        [Test]
        public void RepeatedPublications_NeverOverflowTheQueue()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaThenBetaModule>().Build());

            for (var i = 0; i < 500; i++)
            {
                Context.Alpha.Publish();
                Context.Beta.Publish();
            }

            Assert.DoesNotThrow(() => machine.Tick(0.1f));
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<RunProbe>(), Is.True);
        }
    }
}
