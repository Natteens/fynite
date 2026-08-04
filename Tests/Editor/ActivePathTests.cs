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
    /// Reading the active path from outside: top level state first, current state last, and nothing
    /// readable at all once the machine has stopped.
    /// </summary>
    public sealed class ActivePathTests : MachineFixture
    {
        private static readonly Regex Boom = new Regex("fynite-test-boom");

        [Test]
        public void FlatMachine_HasOneActiveState()
        {
            var machine = BuildLocomotion();

            Assert.That(machine.ActiveStateCount, Is.EqualTo(1));
            Assert.That(machine.GetActiveStateType(0), Is.EqualTo(typeof(IdleProbe)));
            Assert.That(machine.GetActiveStateType(0), Is.EqualTo(machine.CurrentStateType));
        }

        [Test]
        public void FlatMachine_ReplacesItsOneStateOnATransition()
        {
            var machine = BuildLocomotion();

            Context.ToWalk = true;
            machine.Tick(0.1f);

            Assert.That(machine.ActiveStateCount, Is.EqualTo(1));
            Assert.That(machine.GetActiveStateType(0), Is.EqualTo(typeof(WalkProbe)));
            Assert.That(machine.GetActiveStateType(0), Is.EqualTo(machine.CurrentStateType));
        }

        [Test]
        public void Hierarchy_ReadsFromTheTopDownToTheCurrentState()
        {
            var machine = BuildDeepBranches();

            Assert.That(machine.ActiveStateCount, Is.EqualTo(3));
            Assert.That(machine.GetActiveStateType(0), Is.EqualTo(typeof(GroundedProbe)));
            Assert.That(machine.GetActiveStateType(1), Is.EqualTo(typeof(LocomotionProbe)));
            Assert.That(machine.GetActiveStateType(2), Is.EqualTo(typeof(IdleProbe)));
        }

        [Test]
        public void Hierarchy_EndsAtTheCurrentState()
        {
            var machine = BuildDeepBranches();

            Assert.That(
                machine.GetActiveStateType(machine.ActiveStateCount - 1),
                Is.EqualTo(machine.CurrentStateType));
        }

        [Test]
        public void Hierarchy_PutsAParentBeforeItsChild()
        {
            var machine = BuildDeepBranches();

            var grounded = IndexOf(machine, typeof(GroundedProbe));
            var locomotion = IndexOf(machine, typeof(LocomotionProbe));
            var idle = IndexOf(machine, typeof(IdleProbe));

            Assert.That(grounded, Is.LessThan(locomotion));
            Assert.That(locomotion, Is.LessThan(idle));
        }

        [Test]
        public void SiblingTransition_ChangesOnlyTheTailOfThePath()
        {
            var machine = BuildDeepBranches();

            Context.ToAttack = true;
            machine.Tick(0.1f);

            Assert.That(machine.ActiveStateCount, Is.EqualTo(2));
            Assert.That(machine.GetActiveStateType(0), Is.EqualTo(typeof(GroundedProbe)));
            Assert.That(machine.GetActiveStateType(1), Is.EqualTo(typeof(AttackProbe)));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(AttackProbe)));
        }

        [Test]
        public void CrossBranchTransition_ReplacesTheWholePath()
        {
            var machine = BuildDeepBranches();

            Context.ToAirborne = true;
            machine.Tick(0.1f);

            Assert.That(machine.ActiveStateCount, Is.EqualTo(2));
            Assert.That(machine.GetActiveStateType(0), Is.EqualTo(typeof(AirborneProbe)));
            Assert.That(machine.GetActiveStateType(1), Is.EqualTo(typeof(FallingProbe)));
            Assert.That(machine.IsIn<GroundedProbe>(), Is.False);
        }

        [Test]
        public void AncestorTarget_ReadsDownToTheInitialChild()
        {
            var machine = BuildDeepBranches();

            Context.ToAttack = true;
            machine.Tick(0.1f);
            Context.ToAttack = false;

            Context.ToGrounded = true;
            machine.Tick(0.1f);

            // Grounded stays, and its branch restarts at the initial child and its own initial child.
            Assert.That(machine.ActiveStateCount, Is.EqualTo(3));
            Assert.That(machine.GetActiveStateType(0), Is.EqualTo(typeof(GroundedProbe)));
            Assert.That(machine.GetActiveStateType(1), Is.EqualTo(typeof(LocomotionProbe)));
            Assert.That(machine.GetActiveStateType(2), Is.EqualTo(typeof(IdleProbe)));
        }

        [Test]
        public void SelfTransition_KeepsTheSamePath()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<SelfPathModule>()
                .Build());

            Context.ToSelf = true;
            machine.Tick(0.1f);

            Assert.That(machine.ActiveStateCount, Is.EqualTo(1));
            Assert.That(machine.GetActiveStateType(0), Is.EqualTo(typeof(IdleProbe)));
        }

        [Test]
        public void CompositeSelfTransition_RebuildsTheBranch()
        {
            var machine = BuildShell();

            Context.ToSelf = true;
            machine.Tick(0.1f);

            Assert.That(machine.ActiveStateCount, Is.EqualTo(2));
            Assert.That(machine.GetActiveStateType(0), Is.EqualTo(typeof(ShellProbe)));
            Assert.That(machine.GetActiveStateType(1), Is.EqualTo(typeof(LeftProbe)));
        }

        [Test]
        public void NegativeIndex_IsRejected()
        {
            var machine = BuildDeepBranches();

            var failure = Assert.Throws<ArgumentOutOfRangeException>(
                () => machine.GetActiveStateType(-1));

            Assert.That(failure.ParamName, Is.EqualTo("index"));
        }

        [Test]
        public void IndexEqualToTheCount_IsRejected()
        {
            var machine = BuildDeepBranches();

            var failure = Assert.Throws<ArgumentOutOfRangeException>(
                () => machine.GetActiveStateType(machine.ActiveStateCount));

            Assert.That(failure.ParamName, Is.EqualTo("index"));
        }

        [Test]
        public void IndexPastTheCount_IsRejected()
        {
            var machine = BuildDeepBranches();

            var failure = Assert.Throws<ArgumentOutOfRangeException>(
                () => machine.GetActiveStateType(machine.ActiveStateCount + 1));

            Assert.That(failure.ParamName, Is.EqualTo("index"));
        }

        [Test]
        public void EveryIndexIsRejectedOnceTheMachineStopped()
        {
            var machine = BuildDeepBranches();
            machine.Dispose();

            Assert.That(machine.ActiveStateCount, Is.Zero);

            foreach (var index in new[] { -1, 0, 1, 2 })
            {
                var failure = Assert.Throws<ArgumentOutOfRangeException>(
                    () => machine.GetActiveStateType(index));

                Assert.That(failure.ParamName, Is.EqualTo("index"), $"index {index}");
            }
        }

        [Test]
        public void Dispose_EmptiesThePath()
        {
            var machine = BuildDeepBranches();

            machine.Dispose();

            AssertStopped(machine);

            machine.Dispose();
            AssertStopped(machine);
        }

        [Test]
        public void DestroyedOwner_EmptiesThePath()
        {
            var owner = NewOwner();
            var machine = Track(Attach(owner, Context)
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<LocomotionProbe, IdleProbe>()
                .Use<DeepBranchModule>()
                .Build());

            Object.DestroyImmediate(owner.gameObject);
            machine.Tick(0.1f);

            AssertStopped(machine);
        }

        [Test]
        public void Fault_EmptiesThePath()
        {
            var machine = BuildDeepBranches();
            Context.OnUpdate = () => throw new InvalidOperationException("fynite-test-boom");

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            AssertStopped(machine);
        }

        [Test]
        public void LoopReset_EmptiesThePath()
        {
            var machine = BuildDeepBranches();

            FyniteLoop.Clear();

            AssertStopped(machine);
        }

        [Test]
        public void FailedLaunch_NeverLeavesAReadablePath()
        {
            Context.ThrowOn = "LocomotionProbe.Enter";

            Assert.Throws<InvalidOperationException>(
                () => Attach()
                    .Start<GroundedProbe>()
                    .Child<GroundedProbe, LocomotionProbe>()
                    .Child<LocomotionProbe, IdleProbe>()
                    .Use<DeepBranchModule>()
                    .Build());

            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void TwoMachines_KeepIndependentPaths()
        {
            var hierarchical = BuildDeepBranches();

            var flatContext = new ProbeContext();
            var flat = Track(Attach(NewOwner(), flatContext)
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            Assert.That(hierarchical.ActiveStateCount, Is.EqualTo(3));
            Assert.That(flat.ActiveStateCount, Is.EqualTo(1));

            flatContext.ToWalk = true;
            flat.Tick(0.1f);

            Assert.That(flat.GetActiveStateType(0), Is.EqualTo(typeof(WalkProbe)));

            // The other machine did not move.
            Assert.That(hierarchical.ActiveStateCount, Is.EqualTo(3));
            Assert.That(hierarchical.GetActiveStateType(2), Is.EqualTo(typeof(IdleProbe)));

            Context.ToAirborne = true;
            hierarchical.Tick(0.1f);

            Assert.That(hierarchical.GetActiveStateType(0), Is.EqualTo(typeof(AirborneProbe)));
            Assert.That(flat.ActiveStateCount, Is.EqualTo(1));
            Assert.That(flat.GetActiveStateType(0), Is.EqualTo(typeof(WalkProbe)));
        }

        /// <summary>Grounded &gt; Locomotion &gt; Idle, with Attack beside Locomotion and Airborne &gt; Falling.</summary>
        private FyniteMachine<ProbeContext> BuildDeepBranches()
            => Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<LocomotionProbe, IdleProbe>()
                .Child<AirborneProbe, FallingProbe>()
                .Use<DeepBranchModule>()
                .Build());

        private static int IndexOf(FyniteMachine<ProbeContext> machine, Type state)
        {
            for (var i = 0; i < machine.ActiveStateCount; i++)
            {
                if (machine.GetActiveStateType(i) == state)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void AssertStopped(FyniteMachine<ProbeContext> machine)
        {
            Assert.That(machine.ActiveStateCount, Is.Zero);
            Assert.That(machine.CurrentStateType, Is.Null);
            Assert.That(machine.IsIn<IdleProbe>(), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => machine.GetActiveStateType(0));
        }

        private sealed class SelfPathModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe, IdleProbe>().When<ToSelf>();
        }
    }

    /// <summary>Grounded &gt; Locomotion &gt; Idle, Attack beside Locomotion, Airborne &gt; Falling.</summary>
    public sealed class DeepBranchModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<LocomotionProbe, AttackProbe>().When<ToAttack>();
            transitions.From<AttackProbe, GroundedProbe>().When<ToGrounded>();
            transitions.From<GroundedProbe, AirborneProbe>().When<ToAirborne>();
            transitions.From<AirborneProbe, GroundedProbe>().When<ToGrounded>();
        }
    }
}
