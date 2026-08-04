using System;
using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>
    /// <c>From&lt;TFrom, TTo&gt;()</c> and <c>Any&lt;TTo&gt;()</c> are spellings, not behaviour: every
    /// test here checks what the machine actually does, not what the API looks like.
    /// </summary>
    public sealed class ShorthandTests : MachineFixture
    {
        [Test]
        public void FromShorthand_RegistersBothStates()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortLocomotionModule>()
                .Build());

            // WalkProbe is never named by To<T>() anywhere, so reaching it proves the shorthand
            // registered it.
            Assert.That(ProbeState.Instances, Is.EqualTo(2));

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);

            Context.ToWalk = false;
            Context.ToIdle = true;
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        [Test]
        public void AnyShorthand_RegistersTheTarget()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortDeathModule>()
                .Build());

            Assert.That(ProbeState.Instances, Is.EqualTo(2));

            Context.ToDead = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<DeadProbe>(), Is.True);
        }

        [Test]
        public void ShortAndLongForms_BehaveIdentically()
        {
            var longContext = new ProbeContext();
            var byLongForm = Track(Attach(NewOwner(), longContext)
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            var byShortForm = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortLocomotionModule>()
                .Build());

            longContext.Log.Clear();
            Context.Log.Clear();

            Drive(byLongForm, longContext);
            Drive(byShortForm, Context);

            Assert.That(Context.Trace, Is.EqualTo(longContext.Trace));
            Assert.That(byShortForm.CurrentStateType, Is.EqualTo(byLongForm.CurrentStateType));
        }

        [Test]
        public void Shorthand_WorksWithAPredicateClass()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortLocomotionModule>()
                .Build());

            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void Shorthand_WorksWithAPredicateLambda()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortLambdaModule>()
                .Build());

            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void Shorthand_WorksWithAnEventTransition()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortEventModule>()
                .Build());

            Context.Beta.Publish();
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);

            Context.Alpha.Publish();
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<DeadProbe>(), Is.True);
        }

        [Test]
        public void GlobalShorthand_StillWinsOverALocalOne()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortPrecedenceModule>()
                .Build());

            Context.ToWalk = true;
            Context.ToDead = true;
            machine.Tick(0.1f);

            Assert.That(
                machine.IsIn<DeadProbe>(),
                Is.True,
                "the local shorthand, declared first, beat the global one");
        }

        [Test]
        public void GlobalEventShorthand_StillWinsOverALocalOne()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortEventPrecedenceModule>()
                .Build());

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<DeadProbe>(), Is.True);
        }

        [Test]
        public void LocalShorthand_StillEvaluatesLeafBeforeParent()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<AirborneProbe, FallingProbe>()
                .Use<ShortLeafOverParentModule>()
                .Build());

            // Both rules answer the same predicate; the leaf has to be the one that wins.
            Context.ToAirborne = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<AttackProbe>(), Is.True);
            Assert.That(machine.IsIn<GroundedProbe>(), Is.True);
        }

        [Test]
        public void Shorthand_KeepsDeclarationOrder()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortAmbiguousModule>()
                .Build());

            Context.ToWalk = true;
            Context.ToRun = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void Shorthand_KeepsDeclarationOrderAcrossModules()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortIdleToRunModule>()
                .Use<ShortAmbiguousModule>()
                .Build());

            Context.ToWalk = true;
            Context.ToRun = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<RunProbe>(), Is.True);
        }

        [Test]
        public void Shorthand_DoesOneTransitionPerUpdate()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortChainModule>()
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
        public void Shorthand_SupportsASelfTransition()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortSelfModule>()
                .Build());

            Context.Log.Clear();
            Context.ToSelf = true;
            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("IdleProbe.Exit,IdleProbe.Enter,IdleProbe.Update"));
        }

        [Test]
        public void Shorthand_KeepsHierarchyIntact()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<AirborneProbe, FallingProbe>()
                .Use<ShortBranchModule>()
                .Build());

            // A sibling transition keeps the parent running.
            Context.Log.Clear();
            Context.ToAttack = true;
            machine.Tick(0.1f);
            Context.ToAttack = false;

            Assert.That(machine.IsIn<AttackProbe>(), Is.True);
            Assert.That(machine.IsIn<GroundedProbe>(), Is.True);
            Assert.That(Context.CountOf("GroundedProbe.Exit"), Is.Zero);

            // Back to the parent, which restarts its branch at the initial child.
            Context.ToGrounded = true;
            machine.Tick(0.1f);
            Context.ToGrounded = false;

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LocomotionProbe)));

            // Leaving the branch altogether goes through the common ancestor and enters the initial
            // child of the composite target.
            Context.Log.Clear();
            Context.ToAirborne = true;
            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Does.StartWith(
                    "LocomotionProbe.Exit,GroundedProbe.Exit,AirborneProbe.Enter,FallingProbe.Enter"));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(FallingProbe)));
        }

        [Test]
        public void Shorthand_LeavesActivitiesAlone()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortActivityModule>()
                .Build());

            Context.Log.Clear();
            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("IdleProbe.Exit,ImmediateProbe.Enter,ImmediateProbe.Update,Begin,End"));
            Assert.That(ImmediateProbe.Configured, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedTypes_DoNotCreateDuplicateStates()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortRepeatedModule>()
                .Build());

            Assert.That(ProbeState.Instances, Is.EqualTo(2), "a state was registered more than once");

            // The second rule out of Idle still works, so the repeats really are the same states.
            Context.ToRun = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void MixingShortAndLongForms_SharesTheSameStates()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ShortAndLongModule>()
                .Build());

            Assert.That(ProbeState.Instances, Is.EqualTo(3));

            Context.ToWalk = true;
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);

            Context.ToRun = true;
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<RunProbe>(), Is.True);
        }

        [Test]
        public void Shorthand_IsRejectedAfterBuild()
        {
            Track(Attach()
                .Start<IdleProbe>()
                .Use<CapturingShortModule>()
                .Build());

            var captured = CapturingShortModule.Captured;

            Assert.Throws<InvalidOperationException>(() => captured.From<IdleProbe, RunProbe>());
            Assert.Throws<InvalidOperationException>(() => captured.Any<RunProbe>());
        }

        private static void Drive(FyniteMachine<ProbeContext> machine, ProbeContext context)
        {
            machine.Tick(0.1f);

            context.ToWalk = true;
            machine.Tick(0.1f);

            context.ToWalk = false;
            machine.Tick(0.1f);

            context.ToIdle = true;
            machine.Tick(0.1f);

            context.ToIdle = false;
            machine.Tick(0.1f);
        }

        /// <summary>The same rules as <see cref="LocomotionModule"/>, written short.</summary>
        private sealed class ShortLocomotionModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions
                    .From<IdleProbe, WalkProbe>()
                    .When<ToWalk>();

                transitions
                    .From<WalkProbe, IdleProbe>()
                    .When<ToIdle>();
            }
        }

        private sealed class ShortDeathModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions
                    .Any<DeadProbe>()
                    .When<ToDead>();
        }

        private sealed class ShortLambdaModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe, WalkProbe>().When(context => context.ToWalk);
                transitions.From<WalkProbe, IdleProbe>().When(context => context.ToIdle);
            }
        }

        private sealed class ShortEventModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe, WalkProbe>().On(context => context.Beta);
                transitions.Any<DeadProbe>().On(context => context.Alpha);
            }
        }

        /// <summary>The local rule is declared first, so only precedence can put Dead ahead of it.</summary>
        private sealed class ShortPrecedenceModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe, WalkProbe>().When<ToWalk>();
                transitions.Any<DeadProbe>().When<ToDead>();
            }
        }

        private sealed class ShortEventPrecedenceModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe, WalkProbe>().On(context => context.Alpha);
                transitions.Any<DeadProbe>().On(context => context.Alpha);
            }
        }

        /// <summary>The parent rule is declared first; the leaf still has to answer.</summary>
        private sealed class ShortLeafOverParentModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<GroundedProbe, AirborneProbe>().When<ToAirborne>();
                transitions.From<LocomotionProbe, AttackProbe>().When<ToAirborne>();
            }
        }

        private sealed class ShortAmbiguousModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe, WalkProbe>().When<ToWalk>();
                transitions.From<IdleProbe, RunProbe>().When<ToRun>();
            }
        }

        private sealed class ShortIdleToRunModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe, RunProbe>().When<ToRun>();
        }

        private sealed class ShortChainModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe, WalkProbe>().When<ToWalk>();
                transitions.From<WalkProbe, RunProbe>().When<ToRun>();
            }
        }

        private sealed class ShortSelfModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe, IdleProbe>().When<ToSelf>();
        }

        /// <summary>The same rules as <see cref="PlayerBranchModule"/>, written short.</summary>
        private sealed class ShortBranchModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<LocomotionProbe, AttackProbe>().When<ToAttack>();
                transitions.From<AttackProbe, GroundedProbe>().When<ToGrounded>();
                transitions.From<GroundedProbe, AirborneProbe>().When<ToAirborne>();
                transitions.From<AirborneProbe, GroundedProbe>().When<ToGrounded>();
            }
        }

        private sealed class ShortActivityModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe, ImmediateProbe>().When<ToWalk>();
        }

        private sealed class ShortRepeatedModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe, WalkProbe>().When<ToWalk>();
                transitions.From<IdleProbe, WalkProbe>().When<ToRun>();
                transitions.From<WalkProbe, IdleProbe>().When<ToIdle>();
                transitions.Any<WalkProbe>().When<Never>();
            }
        }

        private sealed class ShortAndLongModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<IdleProbe, WalkProbe>().When<ToWalk>();
                transitions.From<WalkProbe>().To<RunProbe>().When<ToRun>();
                transitions.Any().To<WalkProbe>().When<Never>();
                transitions.Any<RunProbe>().When<Never>();
            }
        }

        private sealed class CapturingShortModule : IFyniteTransitions<ProbeContext>
        {
            public static FyniteTransitions<ProbeContext> Captured;

            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                Captured = transitions;
                transitions.From<IdleProbe, WalkProbe>().When<ToWalk>();
            }
        }
    }
}
