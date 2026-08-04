using System;
using System.Text.RegularExpressions;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FyniteTests
{
    /// <summary>
    /// When an activity starts, when it stops, and the guarantee that no way of ending a machine
    /// leaves a step listening.
    /// </summary>
    public sealed class ActivityLifecycleTests : MachineFixture
    {
        private static readonly Regex Boom = new Regex("fynite-test-boom");

        [Test]
        public void Activity_StartsAfterEnter()
        {
            var machine = Track(Attach().Start<ImmediateProbe>().Build());

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("ImmediateProbe.Enter,ImmediateProbe.Update,Begin,End"));
        }

        [Test]
        public void Cancel_HappensBeforeExit()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<WaitForModule>()
                .Build());

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Context.ToWalk = false;

            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            var listenersDuringExit = -1;
            Context.OnExit = () => listenersDuringExit = Context.Alpha.SubscriberCount;

            Context.ToIdle = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
            Assert.That(listenersDuringExit, Is.Zero, "Exit ran before the step stopped listening");
        }

        [Test]
        public void Reentry_StartsTheChainOver()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ImmediateModule>()
                .Build());

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Assert.That(Context.CountOf("Begin"), Is.EqualTo(1));

            Context.ToWalk = false;
            Context.ToIdle = true;
            machine.Tick(0.1f);
            Context.ToIdle = false;
            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("Begin"), Is.EqualTo(2));
            Assert.That(Context.CountOf("End"), Is.EqualTo(2));
        }

        [Test]
        public void SelfTransition_StartsTheChainOver()
        {
            var machine = Track(Attach()
                .Start<WaitProbe>()
                .Use<SelfWaitModule>()
                .Build());

            machine.Tick(0.2f);
            machine.Tick(0.2f);
            Assert.That(Context.CountOf("End"), Is.Zero);

            Context.ToSelf = true;
            machine.Tick(0.2f);
            Context.ToSelf = false;

            Assert.That(Context.CountOf("Begin"), Is.EqualTo(2), "the chain did not restart");

            // The wait is a full 0.5s again, so the 0.4s already spent count for nothing.
            machine.Tick(0.2f);
            Assert.That(Context.CountOf("End"), Is.Zero);

            machine.Tick(0.2f);
            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
        }

        [Test]
        public void Dispose_CancelsTheActivity()
        {
            var machine = Track(Attach().Start<WaitForProbe>().Build());

            machine.Tick(0.1f);
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            machine.Dispose();

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.DoesNotThrow(() => Context.Alpha.Publish());
        }

        [Test]
        public void DestroyedOwner_CancelsTheActivity()
        {
            var owner = NewOwner();
            var machine = Track(Attach(owner, Context).Start<WaitForProbe>().Build());

            machine.Tick(0.1f);
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            Object.DestroyImmediate(owner.gameObject);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
        }

        [Test]
        public void Fault_CancelsTheActivity()
        {
            var machine = Track(Attach().Start<WaitForProbe>().Build());

            machine.Tick(0.1f);
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            Context.OnUpdate = Throw;

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
        }

        [Test]
        public void ExceptionInAStep_FaultsTheMachine()
        {
            var machine = Track(Attach().Start<ThrowingStepProbe>().Build());

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void ExceptionInWaitUntil_FaultsTheMachine()
        {
            var machine = Track(Attach().Start<ThrowingConditionProbe>().Build());

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
        }

        private static void Throw() => throw new InvalidOperationException("fynite-test-boom");

        private sealed class ThrowingStepProbe : ProbeState
        {
            protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
                => activity.Do(context => Throw());
        }

        private sealed class ThrowingConditionProbe : ProbeState
        {
            protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
                => activity.WaitUntil(context => throw new InvalidOperationException("fynite-test-boom"));
        }
    }
}
