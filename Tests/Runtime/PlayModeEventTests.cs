using System.Collections;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FyniteTests
{
    /// <summary>
    /// The same machine under the real PlayerLoop: nothing in the test forwards the publication, and
    /// nothing unsubscribes by hand.
    /// </summary>
    public sealed class PlayModeEventTests
    {
        private GameObject owner;
        private DamageContext context;
        private FyniteMachine<DamageContext> machine;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("FynitePlayModeEventOwner");
            context = new DamageContext();
        }

        [TearDown]
        public void TearDown()
        {
            machine?.Dispose();
            machine = null;

            if (owner != null)
            {
                Object.Destroy(owner);
            }
        }

        [UnityTest]
        public IEnumerator PublishedEventTransitionsOnTheNextLoopUpdate()
        {
            machine = Build();

            yield return null;

            context.Damaged.Publish();

            Assert.That(machine.IsIn<DamageIdleState>(), Is.True, "Publish changed the state on the spot");

            yield return null;

            Assert.That(machine.IsIn<DamageHitState>(), Is.True);
        }

        [UnityTest]
        public IEnumerator RepeatedPublicationsCauseOneTransition()
        {
            machine = Build();

            yield return null;

            for (var i = 0; i < 20; i++)
            {
                context.Damaged.Publish();
            }

            yield return null;
            yield return null;

            Assert.That(machine.IsIn<DamageHitState>(), Is.True);
            Assert.That(context.Entries, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisabledOwnerKeepsTheEventUntilItIsEnabledAgain()
        {
            machine = Build();

            yield return null;

            owner.SetActive(false);
            context.Damaged.Publish();

            yield return null;
            yield return null;

            Assert.That(machine.IsIn<DamageIdleState>(), Is.True);

            owner.SetActive(true);

            yield return null;

            Assert.That(machine.IsIn<DamageHitState>(), Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyedOwnerStopsReceivingPublications()
        {
            machine = Build();

            yield return null;

            Object.Destroy(owner);

            yield return null;
            yield return null;

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(context.Damaged.SubscriberCount, Is.Zero);
            Assert.DoesNotThrow(() => context.Damaged.Publish());
        }

        private FyniteMachine<DamageContext> Build()
            => Machine
                .Attach(owner, context)
                .Start<DamageIdleState>()
                .Use<DamageTransitions>()
                .Build();
    }

    public sealed class DamageContext
    {
        public readonly FyniteEvent Damaged = new FyniteEvent();

        public int Entries;
    }

    public sealed class DamageIdleState : FyniteState<DamageContext>
    {
    }

    public sealed class DamageHitState : FyniteState<DamageContext>
    {
        protected override void Enter() => Context.Entries++;
    }

    public sealed class DamageTransitions : IFyniteTransitions<DamageContext>
    {
        public void Configure(FyniteTransitions<DamageContext> transitions)
            => transitions
                .From<DamageIdleState>()
                .To<DamageHitState>()
                .On(context => context.Damaged);
    }
}
