using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>
    /// A disabled owner freezes the activity exactly where it is. The one thing that still gets
    /// through is a publication to a wait that was already listening.
    /// </summary>
    public sealed class ActivityPauseTests : MachineFixture
    {
        [Test]
        public void DisabledOwner_StopsTheClock()
        {
            var machine = Track(Attach().Start<WaitProbe>().Build());

            machine.Tick(0.2f);
            Owner.enabled = false;

            for (var i = 0; i < 20; i++)
            {
                machine.Tick(0.2f);
            }

            Assert.That(Context.CountOf("End"), Is.Zero, "the wait ran while the owner was disabled");

            Owner.enabled = true;
            machine.Tick(0.2f);
            Assert.That(Context.CountOf("End"), Is.Zero);

            machine.Tick(0.2f);
            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
        }

        [Test]
        public void DisabledOwner_DoesNotAskTheCondition()
        {
            var machine = Track(Attach().Start<UntilProbe>().Build());

            machine.Tick(0.1f);
            Assert.That(Context.Conditions, Is.EqualTo(1));

            Owner.enabled = false;
            Context.Unlocked = true;

            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(Context.Conditions, Is.EqualTo(1));
            Assert.That(Context.CountOf("End"), Is.Zero);

            Owner.enabled = true;
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
        }

        [Test]
        public void DisabledOwner_DoesNotRunTheImmediateSteps()
        {
            var machine = Track(Attach().Start<ImmediateProbe>().Build());

            Owner.enabled = false;
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("Begin"), Is.Zero);

            Owner.enabled = true;
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("Begin"), Is.EqualTo(1));
        }

        [Test]
        public void DisabledOwner_DoesNotPublish()
        {
            var machine = Track(Attach()
                .Start<PublishProbe>()
                .Use<PublishModule>()
                .Build());

            Owner.enabled = false;
            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Owner.enabled = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<PublishProbe>(), Is.True, "the publication happened while paused");

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        [Test]
        public void DisabledOwner_StillReceivesAWaitThatWasAlreadyListening()
        {
            var machine = Track(Attach().Start<WaitForProbe>().Build());

            machine.Tick(0.1f);
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            Owner.enabled = false;
            Context.Alpha.Publish();

            machine.Tick(0.1f);
            Assert.That(Context.CountOf("End"), Is.Zero, "the chain moved on while paused");

            Owner.enabled = true;
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
        }
    }
}
