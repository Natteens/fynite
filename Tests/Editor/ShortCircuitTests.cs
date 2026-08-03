using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    public sealed class ShortCircuitTests : MachineFixture
    {
        [SetUp]
        public void ResetCounters()
        {
            CountingPredicate.Evaluations = 0;
            CountingPredicate.Result = false;
        }

        [Test]
        public void LocalTransitionsStopAtTheFirstTruePredicate()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<LocalShortCircuitModule>()
                .Build());

            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.True);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
            Assert.That(CountingPredicate.Evaluations, Is.Zero);
        }

        [Test]
        public void GlobalTransitionsStopAtTheFirstTruePredicate()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<GlobalShortCircuitModule>()
                .Build());

            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.True);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
            Assert.That(CountingPredicate.Evaluations, Is.Zero);
        }

        [Test]
        public void TrueGlobalTransitionSkipsLocalPredicates()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<GlobalBeatsLocalModule>()
                .Build());

            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.True);
            Assert.That(machine.IsIn<DeadProbe>(), Is.True);
            Assert.That(CountingPredicate.Evaluations, Is.Zero);
        }

        [Test]
        public void FalsePredicateLetsTheNextOneRun()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<FalseThenCountingModule>()
                .Build());

            machine.Tick(0.1f);

            Assert.That(CountingPredicate.Evaluations, Is.EqualTo(1));
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        [Test]
        public void WinningPredicateIsEvaluatedExactlyOncePerCycle()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<CountingWinnerModule>()
                .Build());

            CountingPredicate.Result = true;
            machine.Tick(0.1f);

            Assert.That(CountingPredicate.Evaluations, Is.EqualTo(1));
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void FixedUpdateEvaluatesNoPredicates()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<CountingWinnerModule>()
                .Build());

            CountingPredicate.Result = true;
            machine.TickFixed(0.02f);

            Assert.That(CountingPredicate.Evaluations, Is.Zero);
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        private sealed class LocalShortCircuitModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe>().To<WalkProbe>().When<Always>();
                transitions.From<IdleProbe>().To<RunProbe>().When<ThrowingPredicate>();
                transitions.From<IdleProbe>().To<DeadProbe>().When<CountingPredicate>();
            }
        }

        private sealed class GlobalShortCircuitModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.Any().To<WalkProbe>().When<Always>();
                transitions.Any().To<RunProbe>().When<ThrowingPredicate>();
                transitions.Any().To<DeadProbe>().When<CountingPredicate>();
            }
        }

        private sealed class GlobalBeatsLocalModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe>().To<WalkProbe>().When<ThrowingPredicate>();
                transitions.From<IdleProbe>().To<RunProbe>().When<CountingPredicate>();
                transitions.Any().To<DeadProbe>().When<Always>();
            }
        }

        private sealed class FalseThenCountingModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe>().To<WalkProbe>().When<Never>();
                transitions.From<IdleProbe>().To<RunProbe>().When<CountingPredicate>();
            }
        }

        private sealed class CountingWinnerModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe>().To<WalkProbe>().When<CountingPredicate>();
                transitions.From<IdleProbe>().To<RunProbe>().When<ThrowingPredicate>();
            }
        }
    }

    public sealed class CountingPredicate : IPredicate<ProbeContext>
    {
        public static int Evaluations;
        public static bool Result;

        public bool Evaluate(ProbeContext context)
        {
            Evaluations++;
            return Result;
        }
    }
}
