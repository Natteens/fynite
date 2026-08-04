using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace FyniteTests
{
    /// <summary>
    /// Activities belong to a state instance, and a state instance belongs to one machine. Two
    /// machines sharing a context share the events, and nothing else.
    /// </summary>
    public sealed class ActivityIsolationTests : MachineFixture
    {
        private static readonly Regex Boom = new Regex("fynite-test-boom");

        [Test]
        public void TwoMachines_KeepSeparateProgress()
        {
            var first = Track(Attach().Start<WaitProbe>().Build());

            var secondContext = new ProbeContext();
            var second = Track(Attach(NewOwner(), secondContext).Start<WaitProbe>().Build());

            first.Tick(0.2f);
            first.Tick(0.2f);
            first.Tick(0.2f);

            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
            Assert.That(secondContext.CountOf("Begin"), Is.Zero, "the other machine advanced too");

            secondContext.Log.Clear();
            second.Tick(0.2f);

            Assert.That(secondContext.CountOf("Begin"), Is.EqualTo(1));
            Assert.That(secondContext.CountOf("End"), Is.Zero);
        }

        [Test]
        public void OneSource_FinishesTheWaitOfEveryMachineListening()
        {
            var first = Track(Attach().Start<WaitForProbe>().Build());
            var second = Track(Attach(NewOwner(), Context).Start<WaitForProbe>().Build());

            first.Tick(0.1f);
            second.Tick(0.1f);

            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(2));

            Context.Alpha.Publish();
            first.Tick(0.1f);
            second.Tick(0.1f);

            Assert.That(Context.CountOf("End"), Is.EqualTo(2));
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
        }

        [Test]
        public void OneMachineFaulting_LeavesTheOtherListening()
        {
            var faulting = Track(Attach().Start<WaitForProbe>().Build());
            var healthy = Track(Attach(NewOwner(), Context).Start<WaitForProbe>().Build());

            faulting.Tick(0.1f);
            healthy.Tick(0.1f);
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(2));

            Context.OnUpdate = Throw;

            LogAssert.Expect(LogType.Exception, Boom);
            faulting.Tick(0.1f);

            Assert.That(faulting.IsRunning, Is.False);
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            Context.OnUpdate = null;
            Context.Log.Clear();
            Context.Alpha.Publish();
            healthy.Tick(0.1f);

            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
        }

        [Test]
        public void TwoInstancesOfTheSameState_DoNotShareTheirCursor()
        {
            var first = Track(Attach().Start<WaitForProbe>().Build());

            var secondContext = new ProbeContext();
            var second = Track(Attach(NewOwner(), secondContext).Start<WaitForProbe>().Build());

            first.Tick(0.1f);
            second.Tick(0.1f);

            Context.Alpha.Publish();
            first.Tick(0.1f);
            second.Tick(0.1f);

            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
            Assert.That(secondContext.CountOf("End"), Is.Zero, "the cursor was shared");
            Assert.That(secondContext.Alpha.SubscriberCount, Is.EqualTo(1));
        }

        private static void Throw() => throw new InvalidOperationException("fynite-test-boom");
    }
}
