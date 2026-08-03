using System;
using System.Text.RegularExpressions;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FyniteTests
{
    public sealed class HierarchyFaultTests : MachineFixture
    {
        private static readonly Regex Boom = new Regex("fynite-test-boom");
        private static readonly Regex PredicateBoom = new Regex("fynite-test-predicate");

        [Test]
        public void ExceptionInParentEnterDuringBuildIsNotSwallowed()
        {
            Context.ThrowOn = "GroundedProbe.Enter";

            Assert.Throws<InvalidOperationException>(() => BuildBranches());

            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
            Assert.That(Context.CountOf("LocomotionProbe.Enter"), Is.Zero);
        }

        [Test]
        public void ExceptionInChildEnterDuringBuildIsNotSwallowed()
        {
            Context.ThrowOn = "LocomotionProbe.Enter";

            Assert.Throws<InvalidOperationException>(() => BuildBranches());

            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExceptionInParentEnterDuringATransitionFaultsTheMachine()
        {
            var machine = BuildBranches();
            Context.ToAirborne = true;
            Context.ThrowOn = "AirborneProbe.Enter";

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(Context.CountOf("FallingProbe.Enter"), Is.Zero);
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExceptionInChildEnterDuringATransitionFaultsTheMachine()
        {
            var machine = BuildBranches();
            Context.ToAirborne = true;
            Context.ThrowOn = "FallingProbe.Enter";

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(Context.CountOf("AirborneProbe.Update"), Is.Zero);
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExceptionInParentUpdateStopsBeforeTheChild()
        {
            var machine = BuildBranches();
            Context.Log.Clear();
            Context.ThrowOn = "GroundedProbe.Update";

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Assert.That(Context.CountOf("LocomotionProbe.Update"), Is.Zero);
            Assert.That(machine.IsRunning, Is.False);
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExceptionInChildUpdateDoesNotRepeat()
        {
            var machine = BuildBranches();
            Context.ThrowOn = "LocomotionProbe.Update";

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Context.Log.Clear();
            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(Context.Log, Is.Empty);
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void ExceptionInParentFixedUpdateStopsBeforeTheChild()
        {
            var machine = BuildBranches();
            Context.Log.Clear();
            Context.ThrowOn = "GroundedProbe.FixedUpdate";

            LogAssert.Expect(LogType.Exception, Boom);
            machine.TickFixed(0.02f);

            Assert.That(Context.CountOf("LocomotionProbe.FixedUpdate"), Is.Zero);
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void ExceptionInChildFixedUpdateDoesNotRepeat()
        {
            var machine = BuildBranches();
            Context.ThrowOn = "LocomotionProbe.FixedUpdate";

            LogAssert.Expect(LogType.Exception, Boom);
            machine.TickFixed(0.02f);

            Context.Log.Clear();
            machine.TickFixed(0.02f);

            Assert.That(Context.Log, Is.Empty);
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void ExceptionWhileExitingABranchLeavesNoRegistration()
        {
            var machine = BuildBranches();
            Context.ToAirborne = true;
            Context.ThrowOn = "GroundedProbe.Exit";

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(Context.CountOf("AirborneProbe.Enter"), Is.Zero);
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExceptionInExitDuringDisposeStillUnregisters()
        {
            var machine = BuildBranches();
            Context.ThrowOn = "LocomotionProbe.Exit";

            Assert.Throws<InvalidOperationException>(() => machine.Dispose());

            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void ExceptionInALeafPredicateDoesNotCorruptThePath()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Use<ThrowingLeafModule>()
                .Build());

            LogAssert.Expect(LogType.Exception, PredicateBoom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(machine.CurrentStateType, Is.Null);
            Assert.That(Context.CountOf("LocomotionProbe.Exit"), Is.Zero);
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExceptionInASuperstatePredicateDoesNotCorruptThePath()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Use<ThrowingParentModule>()
                .Build());

            LogAssert.Expect(LogType.Exception, PredicateBoom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(Context.CountOf("GroundedProbe.Exit"), Is.Zero);
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void AFaultedHierarchyIsSafeToDispose()
        {
            var machine = BuildBranches();
            Context.ThrowOn = "LocomotionProbe.Update";

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Context.Log.Clear();
            Assert.DoesNotThrow(() => machine.Dispose());
            Assert.That(Context.Log, Is.Empty);
        }

        [Test]
        public void OneFaultedHierarchyDoesNotStopAnother()
        {
            var faulting = BuildBranches();
            var healthyContext = new ProbeContext();
            var healthy = Track(Attach(NewOwner(), healthyContext)
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Use<PlayerBranchModule>()
                .Build());

            Context.ThrowOn = "GroundedProbe.Update";
            healthyContext.Log.Clear();

            LogAssert.Expect(LogType.Exception, Boom);
            FyniteLoop.Tick(0.1f, false);

            Assert.That(faulting.IsRunning, Is.False);
            Assert.That(healthy.IsRunning, Is.True);
            Assert.That(
                healthyContext.Trace,
                Is.EqualTo("GroundedProbe.Update,LocomotionProbe.Update"));
            Assert.That(FyniteLoop.Count, Is.EqualTo(1));
        }

        private sealed class ThrowingLeafModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<LocomotionProbe>().To<AttackProbe>().When<ThrowingPredicate>();
        }

        private sealed class ThrowingParentModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<LocomotionProbe>().To<AttackProbe>().When<Never>();
                transitions.From<GroundedProbe>().To<AirborneProbe>().When<ThrowingPredicate>();
            }
        }
    }
}
