using System;
using System.Text.RegularExpressions;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FyniteTests
{
    public sealed class FaultTests : MachineFixture
    {
        private static readonly Regex Boom = new Regex("fynite-test-boom");
        private static readonly Regex PredicateBoom = new Regex("fynite-test-predicate");

        [Test]
        public void ExceptionInEnterDuringBuildIsNotSwallowed()
        {
            Context.OnEnter = Throw;

            Assert.Throws<InvalidOperationException>(
                () => Attach().Start<IdleProbe>().Use<LocomotionModule>().Build());

            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExceptionInEnterDuringTransitionFaultsTheMachine()
        {
            var machine = BuildLocomotion();
            Context.ToWalk = true;
            Context.OnEnter = Throw;

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExceptionInUpdateDoesNotRepeat()
        {
            var machine = BuildLocomotion();
            Context.OnUpdate = Throw;

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Context.Log.Clear();
            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(Context.Log, Is.Empty);
            Assert.That(machine.IsRunning, Is.False);
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExceptionInFixedUpdateDoesNotRepeat()
        {
            var machine = BuildLocomotion();
            Context.OnFixedUpdate = Throw;

            LogAssert.Expect(LogType.Exception, Boom);
            machine.TickFixed(0.02f);

            Context.Log.Clear();
            machine.TickFixed(0.02f);
            machine.TickFixed(0.02f);

            Assert.That(Context.Log, Is.Empty);
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void ExceptionInExitLeavesNoActiveRegistration()
        {
            var machine = BuildLocomotion();
            Context.ToWalk = true;
            Context.OnExit = Throw;

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void ExceptionInExitDuringDisposeStillUnregisters()
        {
            var machine = BuildLocomotion();
            Context.OnExit = Throw;

            Assert.Throws<InvalidOperationException>(() => machine.Dispose());

            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void ExceptionInPredicateDoesNotCorruptTheCurrentState()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<ThrowingModule>()
                .Build());

            LogAssert.Expect(LogType.Exception, PredicateBoom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(machine.CurrentStateType, Is.Null);
            Assert.That(Context.CountOf("IdleProbe.Exit"), Is.EqualTo(0));
            Assert.That(Context.CountOf("WalkProbe.Enter"), Is.EqualTo(0));
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void FaultedMachineIsSafeToDispose()
        {
            var machine = BuildLocomotion();
            Context.OnUpdate = Throw;

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Context.Log.Clear();
            Assert.DoesNotThrow(() => machine.Dispose());
            Assert.That(Context.Log, Is.Empty);
        }

        [Test]
        public void OneFaultedMachineDoesNotStopAnother()
        {
            var faulting = BuildLocomotion();
            var healthyContext = new ProbeContext();
            var healthy = Track(Attach(NewOwner(), healthyContext)
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            Context.OnUpdate = Throw;
            healthyContext.Log.Clear();

            LogAssert.Expect(LogType.Exception, Boom);
            FyniteLoop.Tick(0.1f, false);

            Assert.That(faulting.IsRunning, Is.False);
            Assert.That(healthy.IsRunning, Is.True);
            Assert.That(healthyContext.Trace, Is.EqualTo("IdleProbe.Update"));
        }

        private static void Throw() => throw new InvalidOperationException("fynite-test-boom");

        private sealed class ThrowingModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe>().To<WalkProbe>().When<ThrowingPredicate>();
        }
    }
}
