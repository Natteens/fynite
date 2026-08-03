using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    public sealed class TransitionTests : MachineFixture
    {
        [Test]
        public void FalsePredicate_KeepsState()
        {
            var machine = BuildLocomotion();

            machine.Tick(0.1f);

            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        [Test]
        public void TruePredicate_ChangesState()
        {
            var machine = BuildLocomotion();
            Context.ToWalk = true;

            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void FirstDeclaredTrueTransitionWins()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<AmbiguousModule>()
                .Build());

            Context.ToWalk = true;
            Context.ToRun = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void DeclarationOrderAcrossModulesIsDeterministic()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<IdleToRunModule>()
                .Use<AmbiguousModule>()
                .Build());

            Context.ToWalk = true;
            Context.ToRun = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<RunProbe>(), Is.True);
        }

        [Test]
        public void OnlyOneTransitionRunsPerUpdate()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ChainModule>()
                .Build());

            Context.ToWalk = true;
            Context.ToRun = true;
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("IdleProbe.Exit,WalkProbe.Enter,WalkProbe.Update"));
        }

        [Test]
        public void GlobalTransitionIsEvaluatedBeforeLocal()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Use<DeathModule>()
                .Build());

            Context.ToWalk = true;
            Context.ToDead = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<DeadProbe>(), Is.True);
        }

        [Test]
        public void GlobalTransitionAppliesFromAnyState()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Use<DeathModule>()
                .Build());

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Context.ToWalk = false;
            Context.ToDead = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<DeadProbe>(), Is.True);
        }

        [Test]
        public void LambdaAndPredicateBehaveTheSame()
        {
            var byInterface = Track(Attach()
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            var lambdaContext = new ProbeContext();
            var byLambda = Track(Attach(NewOwner(), lambdaContext)
                .Start<IdleProbe>()
                .Use<LambdaModule>()
                .Build());

            Context.ToWalk = true;
            lambdaContext.ToWalk = true;
            Context.Log.Clear();
            lambdaContext.Log.Clear();

            byInterface.Tick(0.1f);
            byLambda.Tick(0.1f);

            Assert.That(lambdaContext.Trace, Is.EqualTo(Context.Trace));
            Assert.That(byLambda.CurrentStateType, Is.EqualTo(byInterface.CurrentStateType));
        }

        [Test]
        public void MachinesDoNotShareStateInstances()
        {
            var first = BuildLocomotion();
            var secondContext = new ProbeContext();
            var second = Track(Attach(NewOwner(), secondContext)
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            Assert.That(ProbeState.Instances, Is.EqualTo(4));

            Context.ToWalk = true;
            first.Tick(0.1f);

            Assert.That(first.IsIn<WalkProbe>(), Is.True);
            Assert.That(second.IsIn<IdleProbe>(), Is.True);
        }

        [Test]
        public void MachinesDoNotShareContext()
        {
            var first = BuildLocomotion();
            var secondContext = new ProbeContext();
            var second = Track(Attach(NewOwner(), secondContext)
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            Context.Log.Clear();
            secondContext.Log.Clear();

            first.Tick(0.1f);

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Update"));
            Assert.That(secondContext.Log, Is.Empty);
            Assert.That(second.IsRunning, Is.True);
        }

        [Test]
        public void SelfTransitionRunsExitAndEnter()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<SelfModule>()
                .Build());

            Context.Log.Clear();
            Context.ToIdle = true;
            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("IdleProbe.Exit,IdleProbe.Enter,IdleProbe.Update"));
        }

        private sealed class AmbiguousModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe>().To<WalkProbe>().When<ToWalk>();
                transitions.From<IdleProbe>().To<RunProbe>().When<ToRun>();
            }
        }

        private sealed class IdleToRunModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe>().To<RunProbe>().When<ToRun>();
        }

        private sealed class ChainModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe>().To<WalkProbe>().When<ToWalk>();
                transitions.From<WalkProbe>().To<RunProbe>().When<ToRun>();
            }
        }

        private sealed class LambdaModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions
                    .From<IdleProbe>()
                    .To<WalkProbe>()
                    .When(static context => context.ToWalk);

                transitions
                    .From<WalkProbe>()
                    .To<IdleProbe>()
                    .When(static context => context.ToIdle);
            }
        }

        private sealed class SelfModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe>().To<IdleProbe>().When<ToIdle>();
        }
    }
}
