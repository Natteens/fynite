using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    public sealed class HierarchyTransitionTests : MachineFixture
    {
        [SetUp]
        public void ResetCounters()
        {
            CountingPredicate.Evaluations = 0;
            CountingPredicate.Result = false;
        }

        [Test]
        public void SiblingTransitionKeepsTheParentActive()
        {
            var machine = BuildBranches();
            Context.Log.Clear();
            Context.ToAttack = true;

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo(
                    "LocomotionProbe.Exit,AttackProbe.Enter,GroundedProbe.Update,AttackProbe.Update"));
            Assert.That(machine.IsIn<GroundedProbe>(), Is.True);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(AttackProbe)));
        }

        [Test]
        public void ChildToAnotherTopLevelExitsTheWholeBranch()
        {
            var machine = BuildBranches();
            Context.Log.Clear();
            Context.ToAirborne = true;

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo(
                    "LocomotionProbe.Exit,GroundedProbe.Exit,AirborneProbe.Enter,FallingProbe.Enter," +
                    "AirborneProbe.Update,FallingProbe.Update"));
            Assert.That(machine.IsIn<GroundedProbe>(), Is.False);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(FallingProbe)));
        }

        [Test]
        public void CompositeTargetEntersItsInitialChild()
        {
            var machine = BuildBranches();
            Context.ToAirborne = true;
            machine.Tick(0.1f);

            Context.ToAirborne = false;
            Context.ToGrounded = true;
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo(
                    "FallingProbe.Exit,AirborneProbe.Exit,GroundedProbe.Enter,LocomotionProbe.Enter," +
                    "GroundedProbe.Update,LocomotionProbe.Update"));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LocomotionProbe)));
        }

        [Test]
        public void TransitionToAnActiveAncestorRestoresItsInitialChild()
        {
            var machine = BuildBranches();
            Context.ToAttack = true;
            machine.Tick(0.1f);

            Context.ToAttack = false;
            Context.ToGrounded = true;
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo(
                    "AttackProbe.Exit,LocomotionProbe.Enter,GroundedProbe.Update,LocomotionProbe.Update"));
            Assert.That(machine.IsIn<GroundedProbe>(), Is.True);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LocomotionProbe)));
        }

        [Test]
        public void LeafTransitionsAreEvaluatedBeforeTheParentOnes()
        {
            var machine = BuildBranches();
            Context.ToAttack = true;
            Context.ToAirborne = true;
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(AttackProbe)));
            Assert.That(machine.IsIn<AirborneProbe>(), Is.False);
        }

        [Test]
        public void ParentTransitionsRunWhenNoLeafTransitionWins()
        {
            var machine = BuildBranches();
            Context.ToAirborne = true;

            machine.Tick(0.1f);

            Assert.That(machine.IsIn<AirborneProbe>(), Is.True);
        }

        [Test]
        public void GlobalTransitionsAreEvaluatedBeforeTheActivePath()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Use<PlayerBranchModule>()
                .Use<DeathModule>()
                .Build());

            Context.Log.Clear();
            Context.ToAttack = true;
            Context.ToDead = true;

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo(
                    "LocomotionProbe.Exit,GroundedProbe.Exit,DeadProbe.Enter,DeadProbe.Update"));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(DeadProbe)));
        }

        [Test]
        public void TransitionToAFlatStateStillWorks()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Use<GroundedToWalkModule>()
                .Build());

            Context.Log.Clear();
            Context.ToWalk = true;

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo(
                    "LocomotionProbe.Exit,GroundedProbe.Exit,WalkProbe.Enter,WalkProbe.Update"));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(WalkProbe)));
        }

        [Test]
        public void OnlyOneTransitionRunsPerUpdate()
        {
            var machine = BuildBranches();
            Context.Log.Clear();
            Context.ToAttack = true;
            Context.ToGrounded = true;

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo(
                    "LocomotionProbe.Exit,AttackProbe.Enter,GroundedProbe.Update,AttackProbe.Update"));
        }

        [Test]
        public void SelfTransitionOnALeafRestartsOnlyThatLeaf()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Use<LeafSelfModule>()
                .Build());

            Context.Log.Clear();
            Context.ToSelf = true;

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo(
                    "LocomotionProbe.Exit,LocomotionProbe.Enter,GroundedProbe.Update," +
                    "LocomotionProbe.Update"));
            Assert.That(Context.CountOf("GroundedProbe.Exit"), Is.Zero);
        }

        [Test]
        public void SelfTransitionOnACompositeRestartsTheWholeBranch()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Use<CompositeSelfModule>()
                .Build());

            Context.ToAttack = true;
            machine.Tick(0.1f);

            Context.ToAttack = false;
            Context.ToSelf = true;
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo(
                    "AttackProbe.Exit,GroundedProbe.Exit,GroundedProbe.Enter,LocomotionProbe.Enter," +
                    "GroundedProbe.Update,LocomotionProbe.Update"));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LocomotionProbe)));
        }

        [Test]
        public void TheWinningPredicateIsEvaluatedExactlyOnce()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Use<CountingLeafModule>()
                .Build());

            CountingPredicate.Result = true;
            machine.Tick(0.1f);

            Assert.That(CountingPredicate.Evaluations, Is.EqualTo(1));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(AttackProbe)));
        }

        [Test]
        public void PredicatesAfterTheWinnerAreNotEvaluated()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Use<LeafWinsOverParentModule>()
                .Build());

            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.True);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(AttackProbe)));
            Assert.That(CountingPredicate.Evaluations, Is.Zero);
        }

        [Test]
        public void TwoMachinesKeepIndependentPaths()
        {
            var first = BuildBranches();
            var secondContext = new ProbeContext();
            var second = Track(Attach(NewOwner(), secondContext)
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<AirborneProbe, FallingProbe>()
                .Use<PlayerBranchModule>()
                .Build());

            Assert.That(ProbeState.Instances, Is.EqualTo(10));

            Context.ToAirborne = true;
            first.Tick(0.1f);

            Assert.That(first.CurrentStateType, Is.EqualTo(typeof(FallingProbe)));
            Assert.That(second.CurrentStateType, Is.EqualTo(typeof(LocomotionProbe)));
            Assert.That(second.IsIn<GroundedProbe>(), Is.True);
        }

        [Test]
        public void OneMachineDoesNotLogIntoTheOther()
        {
            var first = BuildBranches();
            var secondContext = new ProbeContext();
            Track(Attach(NewOwner(), secondContext)
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Use<PlayerBranchModule>()
                .Build());

            Context.Log.Clear();
            secondContext.Log.Clear();

            first.Tick(0.1f);

            Assert.That(Context.Trace, Is.EqualTo("GroundedProbe.Update,LocomotionProbe.Update"));
            Assert.That(secondContext.Log, Is.Empty);
        }

        private sealed class GroundedToWalkModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<GroundedProbe>().To<WalkProbe>().When<ToWalk>();
        }

        private sealed class LeafSelfModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<LocomotionProbe>().To<LocomotionProbe>().When<ToSelf>();
        }

        private sealed class CompositeSelfModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<LocomotionProbe>().To<AttackProbe>().When<ToAttack>();
                transitions.From<GroundedProbe>().To<GroundedProbe>().When<ToSelf>();
            }
        }

        private sealed class CountingLeafModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<LocomotionProbe>().To<AttackProbe>().When<CountingPredicate>();
        }

        private sealed class LeafWinsOverParentModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<LocomotionProbe>().To<AttackProbe>().When<Always>();
                transitions.From<LocomotionProbe>().To<FallingProbe>().When<ThrowingPredicate>();
                transitions.From<GroundedProbe>().To<AirborneProbe>().When<CountingPredicate>();
            }
        }
    }
}
