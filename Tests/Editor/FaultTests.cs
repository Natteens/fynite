using System;
using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class FaultTests
    {
        [Test]
        public void AnExceptionInAnEnterBlockPropagatesAndFaultsTheMachine()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Grounded).OnEnter(() => new ThrowingAction("enter failed"));

            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                var error = Assert.Throws<InvalidOperationException>(() => machine.Start());

                Assert.AreEqual("enter failed", error.Message);
                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
                Assert.AreEqual(1, diagnostics.Faults.Count);
                Assert.AreSame(error, diagnostics.Faults[0]);
            }
        }

        [Test]
        public void AnExceptionInATickBlockPropagatesAndFaultsTheMachine()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).OnTick(() => new ThrowingAction("tick failed"));

            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();

                var error = Assert.Throws<InvalidOperationException>(() => machine.Tick(0.1f));

                Assert.AreEqual("tick failed", error.Message);
                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
                Assert.IsTrue(diagnostics.Contains("faulted:InvalidOperationException"));
            }
        }

        [Test]
        public void AnExceptionInAFixedTickBlockFaultsTheMachine()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).OnFixedTick(() => new ThrowingAction("fixed failed"));

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                Assert.Throws<InvalidOperationException>(() => machine.FixedTick(0.02f));
                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
            }
        }

        [Test]
        public void AnExceptionInAGuardPropagatesAndFaultsTheMachine()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new ThrowingGuard("guard failed"))
                .TransitionTo(sample.Moving);

            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();

                var error = Assert.Throws<InvalidOperationException>(() => machine.Raise(sample.Move));

                Assert.AreEqual("guard failed", error.Message);
                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
                Assert.AreEqual(1, diagnostics.Faults.Count);
            }
        }

        [Test]
        public void AnExceptionInAnEffectPropagatesAndFaultsTheMachine()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .Do(() => new ThrowingAction("effect failed"))
                .TransitionTo(sample.Moving);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                var error = Assert.Throws<InvalidOperationException>(() => machine.Raise(sample.Move));

                Assert.AreEqual("effect failed", error.Message);
                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
            }
        }

        [Test]
        public void AnExceptionInAnExitBlockPropagatesAndFaultsTheMachine()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).OnExit(() => new ThrowingAction("exit failed"));

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                var error = Assert.Throws<InvalidOperationException>(() => machine.Stop());

                Assert.AreEqual("exit failed", error.Message);
                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
            }
        }

        [Test]
        public void PendingSignalsAreClearedWhenTheMachineFaults()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnTick(() => new RaiseAction("queue-move", sample.Move))
                .OnTick(() => new ThrowingAction("tick failed"))
                .On(sample.Move).Do(() => new TraceAction("should-not-run"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();

                Assert.Throws<InvalidOperationException>(() => machine.Tick(0.1f));

                Assert.AreEqual("queue-move", context.Trace);
                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
            }
        }

        [Test]
        public void AFaultedMachineStillDisposesItsBlocks()
        {
            var tally = new DisposalTally();
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnEnter(() => new DisposableAction(tally, "enter"))
                .OnTick(() => new ThrowingAction("tick failed"));

            var machine = sample.Build().CreateMachine(new TraceContext());
            machine.Start();
            Assert.Throws<InvalidOperationException>(() => machine.Tick(0.1f));

            machine.Dispose();

            Assert.AreEqual(1, tally.CountOf("enter"));
            Assert.AreEqual(FyniteLifecycle.Disposed, machine.Lifecycle);
        }

        [Test]
        public void ABlockFactoryFailureDoesNotLeaveTheMachineHalfBuilt()
        {
            var tally = new DisposalTally();
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).OnEnter(() => new DisposableAction(tally, "first"));
            sample.Builder.State(sample.Moving).OnEnter(() => throw new InvalidOperationException("factory failed"));

            var definition = sample.Build();

            var error = Assert.Throws<InvalidOperationException>(() => definition.CreateMachine(new TraceContext()));

            Assert.AreEqual("factory failed", error.Message);
            Assert.AreEqual(1, tally.CountOf("first"));
        }
    }
}
