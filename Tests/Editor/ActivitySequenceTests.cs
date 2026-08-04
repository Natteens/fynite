using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>The chain itself: order, what blocks, what does not, and what stays finished.</summary>
    public sealed class ActivitySequenceTests : MachineFixture
    {
        [Test]
        public void ImmediateSteps_RunInOrderInsideOneTick()
        {
            var machine = Track(Attach().Start<ImmediateProbe>().Build());
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(Context.Trace, Is.EqualTo("ImmediateProbe.Update,Begin,End"));
        }

        [Test]
        public void StateUpdate_RunsBeforeTheActivityTick()
        {
            var machine = Track(Attach().Start<ImmediateProbe>().Build());
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(Context.Log[0], Is.EqualTo("ImmediateProbe.Update"));
            Assert.That(Context.Log[1], Is.EqualTo("Begin"));
        }

        [Test]
        public void FinishedActivity_StaysFinished()
        {
            var machine = Track(Attach().Start<ImmediateProbe>().Build());

            machine.Tick(0.1f);
            Context.Log.Clear();

            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("ImmediateProbe.Update,ImmediateProbe.Update"),
                "the chain started over on its own");
        }

        [Test]
        public void Do_RunsExactlyOnce()
        {
            var machine = Track(Attach().Start<ImmediateProbe>().Build());

            for (var i = 0; i < 5; i++)
            {
                machine.Tick(0.1f);
            }

            Assert.That(Context.CountOf("Begin"), Is.EqualTo(1));
            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
        }

        [Test]
        public void Wait_CountsDeltaTime()
        {
            var machine = Track(Attach().Start<WaitProbe>().Build());
            Context.Log.Clear();

            machine.Tick(0.2f);
            Assert.That(Context.Trace, Is.EqualTo("WaitProbe.Update,Begin"));

            machine.Tick(0.2f);
            Assert.That(Context.CountOf("End"), Is.Zero, "0.4s was enough for a 0.5s wait");

            machine.Tick(0.2f);
            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
        }

        [Test]
        public void Wait_IgnoresFixedDeltaTime()
        {
            var machine = Track(Attach().Start<WaitProbe>().Build());

            machine.Tick(0.2f);
            Context.Log.Clear();

            for (var i = 0; i < 50; i++)
            {
                machine.TickFixed(0.02f);
            }

            Assert.That(Context.CountOf("End"), Is.Zero, "FixedUpdate advanced the wait");
        }

        [Test]
        public void ZeroWait_CompletesImmediately()
        {
            var machine = Track(Attach().Start<ZeroWaitProbe>().Build());
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(Context.Trace, Is.EqualTo("ZeroWaitProbe.Update,Begin,End"));
        }

        [Test]
        public void WaitUntil_BlocksAndThenAdvances()
        {
            var machine = Track(Attach().Start<UntilProbe>().Build());
            Context.Log.Clear();

            machine.Tick(0.1f);
            machine.Tick(0.1f);
            Assert.That(Context.CountOf("End"), Is.Zero);

            Context.Unlocked = true;
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
        }

        [Test]
        public void WaitUntil_IsAskedAtMostOncePerUpdate()
        {
            var machine = Track(Attach().Start<UntilProbe>().Build());

            machine.Tick(0.1f);
            Assert.That(Context.Conditions, Is.EqualTo(1));

            machine.Tick(0.1f);
            machine.Tick(0.1f);
            Assert.That(Context.Conditions, Is.EqualTo(3));

            machine.TickFixed(0.02f);
            Assert.That(Context.Conditions, Is.EqualTo(3), "FixedUpdate asked the condition");
        }

        [Test]
        public void WaitUntil_IsNotAskedAfterItPassed()
        {
            var machine = Track(Attach().Start<UntilProbe>().Build());

            Context.Unlocked = true;
            machine.Tick(0.1f);
            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(Context.Conditions, Is.EqualTo(1));
        }

        [Test]
        public void Activity_DoesNotRunInFixedUpdate()
        {
            var machine = Track(Attach().Start<ImmediateProbe>().Build());
            Context.Log.Clear();

            machine.TickFixed(0.02f);

            Assert.That(Context.Trace, Is.EqualTo("ImmediateProbe.FixedUpdate"));
        }

        [Test]
        public void TargetOfATransition_RunsItsActivityInTheSameCycle()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ImmediateModule>()
                .Build());

            Context.Log.Clear();
            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("IdleProbe.Exit,ImmediateProbe.Enter,ImmediateProbe.Update,Begin,End"));
        }
    }
}
