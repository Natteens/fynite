using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>What <c>Publish()</c> does, and what it deliberately does not do.</summary>
    public sealed class EventTests : MachineFixture
    {
        [Test]
        public void Publish_WithoutSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new FyniteEvent().Publish());
            Assert.DoesNotThrow(() => Context.Alpha.Publish());
        }

        [Test]
        public void Publish_ReachesTheMachine()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void Publish_ReachesEveryMachineOnTheSameSource()
        {
            var first = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());
            var second = Track(Attach(NewOwner(), Context).Start<IdleProbe>().Use<AlphaModule>().Build());

            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(2));

            Context.Alpha.Publish();
            first.Tick(0.1f);
            second.Tick(0.1f);

            Assert.That(first.IsIn<WalkProbe>(), Is.True);
            Assert.That(second.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void Publish_DoesNotReachADisposedMachine()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());

            machine.Dispose();
            Context.Log.Clear();

            Assert.DoesNotThrow(() => Context.Alpha.Publish());

            machine.Tick(0.1f);

            Assert.That(Context.Log, Is.Empty);
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void Publish_DoesNotChangeStateImmediately()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());
            Context.Log.Clear();

            Context.Alpha.Publish();

            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
            Assert.That(Context.Log, Is.Empty);
        }

        [Test]
        public void Event_RunsExitThenEnterThenUpdateOfTarget()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());
            Context.Log.Clear();

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("IdleProbe.Exit,WalkProbe.Enter,WalkProbe.Update"));
        }

        [Test]
        public void Event_OnlyTriggersItsOwnTransitions()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaThenBetaModule>().Build());

            Context.Beta.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        [Test]
        public void SameSource_IsCoalesced()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaTwiceModule>().Build());

            Context.Alpha.Publish();
            Context.Alpha.Publish();
            Context.Alpha.Publish();

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True, "three publications became more than one");
        }

        [Test]
        public void DifferentSources_KeepPublicationOrder()
        {
            var alphaFirst = Track(Attach()
                .Start<IdleProbe>()
                .Use<BetaThenAlphaFromIdleModule>()
                .Build());

            Context.Alpha.Publish();
            Context.Beta.Publish();
            alphaFirst.Tick(0.1f);

            Assert.That(alphaFirst.IsIn<WalkProbe>(), Is.True);

            var betaContext = new ProbeContext();
            var betaFirst = Track(Attach(NewOwner(), betaContext)
                .Start<IdleProbe>()
                .Use<BetaThenAlphaFromIdleModule>()
                .Build());

            betaContext.Beta.Publish();
            betaContext.Alpha.Publish();
            betaFirst.Tick(0.1f);

            Assert.That(betaFirst.IsIn<RunProbe>(), Is.True);
        }

        [Test]
        public void RemainingEvents_StayPendingAfterATransition()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaThenBetaModule>().Build());

            Context.Alpha.Publish();
            Context.Beta.Publish();

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<RunProbe>(), Is.True);
        }

        [Test]
        public void OnlyOneTransitionRunsPerUpdate()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaThenBetaModule>().Build());
            Context.Log.Clear();

            Context.Alpha.Publish();
            Context.Beta.Publish();
            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("IdleProbe.Exit,WalkProbe.Enter,WalkProbe.Update"));
        }

        [Test]
        public void EventWithoutAMatchingTransition_IsConsumed()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<WalkOnlyAlphaModule>().Build());

            // Nothing leaves Idle on Alpha, so this occurrence has to be spent, not stored.
            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<IdleProbe>(), Is.True);

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True, "a spent event came back in a later state");
        }

        [Test]
        public void DeclarationOrder_DecidesBetweenTwoTransitionsOnTheSameSource()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaOrderModule>().Build());

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void GlobalEventBeatsLocal()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Use<GlobalAlphaModule>()
                .Build());

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<DeadProbe>(), Is.True);
        }

        [Test]
        public void GlobalEventAppliesFromAnyState()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<WalkOnlyAlphaModule>()
                .Use<GlobalAlphaModule>()
                .Build());

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Context.ToWalk = false;

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<DeadProbe>(), Is.True);
        }

        [Test]
        public void Predicates_AreNotEvaluatedWhenAnEventTransitions()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<EventBeforePredicateModule>()
                .Build());

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
            Assert.That(Context.PredicateCalls, Is.Zero);
        }

        [Test]
        public void Predicates_AreEvaluatedWhenNoEventMatches()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<EventBeforePredicateModule>()
                .Build());

            machine.Tick(0.1f);

            Assert.That(Context.PredicateCalls, Is.EqualTo(1));
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        [Test]
        public void Predicates_AreEvaluatedAfterAnEventThatMatchesNothing()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<UnmatchedEventModule>()
                .Build());

            // The machine listens to Alpha, but nothing leaves Idle on it.
            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(Context.PredicateCalls, Is.EqualTo(1));
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        [Test]
        public void PredicatesAndEvents_CoexistInOneMachine()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<WalkOnlyAlphaModule>()
                .Build());

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);

            Context.Alpha.Publish();
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<RunProbe>(), Is.True);
        }

        [Test]
        public void SelfTransitionOnAnEvent_RunsExitAndEnter()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<SelfAlphaModule>().Build());
            Context.Log.Clear();

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("IdleProbe.Exit,IdleProbe.Enter,IdleProbe.Update"));
        }
    }
}
