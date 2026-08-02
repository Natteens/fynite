using System;
using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class InstanceIsolationTests
    {
        [Test]
        public void TwoMachinesOfOneDefinitionKeepIndependentPaths()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);
            var definition = sample.Build();

            using (var first = definition.CreateMachine(new TraceContext()))
            using (var second = definition.CreateMachine(new TraceContext()))
            {
                first.Start();
                second.Start();

                first.Raise(sample.Move);

                Assert.AreEqual(sample.Moving, first.ActiveLeaf);
                Assert.AreEqual(sample.Idle, second.ActiveLeaf);
            }
        }

        [Test]
        public void TwoMachinesKeepIndependentQueues()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).Do(() => new TraceAction("moved"));
            var definition = sample.Build();

            var firstContext = new TraceContext();
            var secondContext = new TraceContext();
            using (var first = definition.CreateMachine(firstContext))
            using (var second = definition.CreateMachine(secondContext))
            {
                first.Start();
                second.Start();

                first.Raise(sample.Move);

                Assert.AreEqual("moved", firstContext.Trace);
                Assert.AreEqual(string.Empty, secondContext.Trace);
            }
        }

        [Test]
        public void BlockInstancesAreNotSharedBetweenMachines()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).OnTick(() => new CountingAction("count"));
            var definition = sample.Build();

            var firstContext = new TraceContext();
            var secondContext = new TraceContext();
            using (var first = definition.CreateMachine(firstContext))
            using (var second = definition.CreateMachine(secondContext))
            {
                first.Start();
                second.Start();

                first.Tick(0.1f);
                first.Tick(0.1f);
                first.Tick(0.1f);
                second.Tick(0.1f);

                Assert.AreEqual("count:1,count:2,count:3", firstContext.Trace);
                Assert.AreEqual("count:1", secondContext.Trace);
            }
        }

        [Test]
        public void EachConfiguredOccurrenceGetsItsOwnInstance()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnTick(() => new CountingAction("a"))
                .OnTick(() => new CountingAction("b"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Tick(0.1f);
                machine.Tick(0.1f);

                Assert.AreEqual("a:1,b:1,a:2,b:2", context.Trace);
            }
        }

        [Test]
        public void TheSameBlockTypeCanAppearInSeveralPhasesIndependently()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnEnter(() => new CountingAction("enter"))
                .OnExit(() => new CountingAction("exit"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Stop();
                machine.Start();
                machine.Stop();

                Assert.AreEqual("enter:1,exit:1,enter:2,exit:2", context.Trace);
            }
        }

        [Test]
        public void DisposableBlocksAreDisposedExactlyOnce()
        {
            var tally = new DisposalTally();
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).OnEnter(() => new DisposableAction(tally, "enter"));

            var machine = sample.Build().CreateMachine(new TraceContext());
            machine.Start();
            machine.Dispose();

            Assert.AreEqual(1, tally.CountOf("enter"));

            machine.Dispose();
            Assert.AreEqual(1, tally.CountOf("enter"));
        }

        [Test]
        public void DifferentOccurrencesAreDisposedSeparately()
        {
            var tally = new DisposalTally();
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnEnter(() => new DisposableAction(tally, "enter"))
                .OnExit(() => new DisposableAction(tally, "exit"));
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new DisposableGuard(tally, "guard"))
                .Do(() => new DisposableAction(tally, "effect"));

            var machine = sample.Build().CreateMachine(new TraceContext());
            machine.Dispose();

            Assert.AreEqual(1, tally.CountOf("enter"));
            Assert.AreEqual(1, tally.CountOf("exit"));
            Assert.AreEqual(1, tally.CountOf("guard"));
            Assert.AreEqual(1, tally.CountOf("effect"));
            Assert.AreEqual(4, tally.Total);
        }

        [Test]
        public void OneMachineDisposesOnlyItsOwnInstances()
        {
            var tally = new DisposalTally();
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).OnEnter(() => new DisposableAction(tally, "enter"));
            var definition = sample.Build();

            var first = definition.CreateMachine(new TraceContext());
            var second = definition.CreateMachine(new TraceContext());

            first.Dispose();
            Assert.AreEqual(1, tally.Total);

            second.Dispose();
            Assert.AreEqual(2, tally.Total);
        }

        [Test]
        public void WhenExitAndDisposalBothThrowTheExitFailureIsPropagated()
        {
            var tally = new DisposalTally();
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnEnter(() => new ThrowingDisposableAction(tally, "bad-block", "dispose failed"))
                .OnExit(() => new ThrowingAction("exit failed"));
            sample.Builder.State(sample.Grounded).OnEnter(() => new DisposableAction(tally, "good-block"));
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new DisposableGuard(tally, "good-guard"))
                .TransitionTo(sample.Moving);

            var diagnostics = new RecordingDiagnostics();
            var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics);
            machine.Start();

            var error = Assert.Throws<InvalidOperationException>(() => machine.Dispose());

            Assert.AreEqual("exit failed", error.Message, "The exit failure must win over the disposal failure.");

            // Every block was still offered disposal, including the ones after the one that threw.
            Assert.AreEqual(1, tally.CountOf("bad-block"));
            Assert.AreEqual(1, tally.CountOf("good-block"));
            Assert.AreEqual(1, tally.CountOf("good-guard"));

            Assert.AreEqual(FyniteLifecycle.Disposed, machine.Lifecycle);

            // The swallowed disposal failure is still reported rather than lost.
            Assert.AreEqual(2, diagnostics.Faults.Count);
            Assert.AreEqual("exit failed", diagnostics.Faults[0].Message);
            Assert.AreEqual("dispose failed", diagnostics.Faults[1].Message);

            // A second Dispose is a no-op and must not dispose anything twice.
            Assert.DoesNotThrow(() => machine.Dispose());
            Assert.AreEqual(3, tally.Total);
        }

        [Test]
        public void ADisposalFailureAloneIsPropagatedAfterEveryBlockIsReleased()
        {
            var tally = new DisposalTally();
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnEnter(() => new ThrowingDisposableAction(tally, "bad-block", "dispose failed"))
                .OnExit(() => new DisposableAction(tally, "good-block"));

            var machine = sample.Build().CreateMachine(new TraceContext());
            machine.Start();

            var error = Assert.Throws<InvalidOperationException>(() => machine.Dispose());

            Assert.AreEqual("dispose failed", error.Message);
            Assert.AreEqual(1, tally.CountOf("bad-block"));
            Assert.AreEqual(1, tally.CountOf("good-block"));
            Assert.AreEqual(FyniteLifecycle.Disposed, machine.Lifecycle);
        }

        [Test]
        public void ADefinitionRemainsUsableAfterItsMachinesAreDisposed()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);
            var definition = sample.Build();

            using (var first = definition.CreateMachine(new TraceContext()))
            {
                first.Start();
                first.Raise(sample.Move);
            }

            using (var second = definition.CreateMachine(new TraceContext()))
            {
                second.Start();

                Assert.AreEqual(sample.Idle, second.ActiveLeaf);
                second.Raise(sample.Move);
                Assert.AreEqual(sample.Moving, second.ActiveLeaf);
            }
        }
    }
}
