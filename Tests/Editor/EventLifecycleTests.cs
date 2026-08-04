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
    /// Subscriptions are the machine's to manage: nothing in a controller adds or removes them, and
    /// every way a machine can end has to take them with it.
    /// </summary>
    public sealed class EventLifecycleTests : MachineFixture
    {
        private static readonly Regex Boom = new Regex("fynite-test-boom");

        [Test]
        public void Dispose_RemovesSubscriptions()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaThenBetaModule>().Build());

            machine.Dispose();

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(Context.Beta.SubscriberCount, Is.Zero);
        }

        [Test]
        public void Dispose_IsStillIdempotent()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());

            machine.Dispose();
            Assert.DoesNotThrow(() => machine.Dispose());
            Assert.DoesNotThrow(() => machine.Dispose());

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(Context.CountOf("IdleProbe.Exit"), Is.EqualTo(1));
        }

        [Test]
        public void DestroyedOwner_RemovesSubscriptions()
        {
            var owner = NewOwner();
            var machine = Track(Attach(owner, Context).Start<IdleProbe>().Use<AlphaModule>().Build());

            Object.DestroyImmediate(owner.gameObject);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.DoesNotThrow(() => Context.Alpha.Publish());
        }

        [Test]
        public void Fault_RemovesSubscriptions()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());
            Context.OnUpdate = Throw;

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);

            Context.Log.Clear();
            Assert.DoesNotThrow(() => Context.Alpha.Publish());
            machine.Tick(0.1f);

            Assert.That(Context.Log, Is.Empty);
        }

        [Test]
        public void DisabledOwner_KeepsTheEventPending()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());

            Owner.enabled = false;
            Context.Alpha.Publish();

            machine.Tick(0.1f);
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);

            Owner.enabled = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void DisabledOwner_StillCoalescesRepeatedPublications()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaTwiceModule>().Build());

            Owner.enabled = false;
            for (var i = 0; i < 50; i++)
            {
                Context.Alpha.Publish();
            }

            Owner.enabled = true;
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void TwoMachinesOnTheSameSourceAreIndependent()
        {
            var first = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());
            var second = Track(Attach(NewOwner(), Context).Start<IdleProbe>().Use<AlphaModule>().Build());

            first.Dispose();

            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            Context.Alpha.Publish();
            second.Tick(0.1f);

            Assert.That(second.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void OneFaultedMachineKeepsTheOtherSubscribed()
        {
            var faultingContext = new ProbeContext();
            var shared = faultingContext.Alpha;

            var faulting = Track(Attach(NewOwner(), faultingContext)
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            var healthy = Track(Attach(NewOwner(), faultingContext)
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            faultingContext.OnUpdate = Throw;

            LogAssert.Expect(LogType.Exception, Boom);
            faulting.Tick(0.1f);

            Assert.That(faulting.IsRunning, Is.False);
            Assert.That(shared.SubscriberCount, Is.EqualTo(1));

            faultingContext.OnUpdate = null;
            shared.Publish();
            healthy.Tick(0.1f);

            Assert.That(healthy.IsIn<WalkProbe>(), Is.True);
        }

        private static void Throw() => throw new InvalidOperationException("fynite-test-boom");
    }
}
