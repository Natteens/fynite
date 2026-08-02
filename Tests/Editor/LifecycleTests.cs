using System;
using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class LifecycleTests
    {
        [Test]
        public void ANewMachineIsCreatedAndIdle()
        {
            var sample = new SampleHierarchy();
            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                Assert.AreEqual(FyniteLifecycle.Created, machine.Lifecycle);
                Assert.IsFalse(machine.IsRunning);
                Assert.AreEqual(0, machine.ActiveDepth);
                Assert.IsFalse(machine.ActiveLeaf.IsValid);
            }
        }

        [Test]
        public void StartMovesTheMachineToRunning()
        {
            var sample = new SampleHierarchy();
            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                Assert.AreEqual(FyniteLifecycle.Running, machine.Lifecycle);
                Assert.IsTrue(machine.IsRunning);
            }
        }

        [Test]
        public void StartingTwiceIsRejected()
        {
            var sample = new SampleHierarchy();
            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                var error = Assert.Throws<InvalidOperationException>(() => machine.Start());
                StringAssert.Contains("already running", error.Message);
            }
        }

        [Test]
        public void StopIsIdempotent()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Stop();
                context.Clear();

                machine.Stop();

                Assert.AreEqual(string.Empty, context.Trace);
                Assert.AreEqual(FyniteLifecycle.Stopped, machine.Lifecycle);
            }
        }

        [Test]
        public void StopOnANeverStartedMachineDoesNothing()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Stop();

                Assert.AreEqual(string.Empty, context.Trace);
                Assert.AreEqual(FyniteLifecycle.Created, machine.Lifecycle);
            }
        }

        [Test]
        public void AMachineCanBeRestartedAfterStop()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Stop();
                context.Clear();

                machine.Start();

                Assert.AreEqual("Enter:Root,Enter:Alive,Enter:Grounded,Enter:Idle", context.Trace);
                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
                Assert.AreEqual(FyniteLifecycle.Running, machine.Lifecycle);
            }
        }

        [Test]
        public void RestartReturnsToTheInitialConfiguration()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();
                machine.Raise(sample.Move);
                Assert.AreEqual(sample.Moving, machine.ActiveLeaf);

                machine.Stop();
                machine.Start();

                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
            }
        }

        [Test]
        public void DisposeStopsARunningMachine()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            var context = new TraceContext();
            var machine = sample.Build().CreateMachine(context);

            machine.Start();
            context.Clear();
            machine.Dispose();

            Assert.AreEqual("Exit:Idle,Exit:Grounded,Exit:Alive,Exit:Root", context.Trace);
            Assert.AreEqual(FyniteLifecycle.Disposed, machine.Lifecycle);
        }

        [Test]
        public void DisposeIsIdempotent()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            var context = new TraceContext();
            var machine = sample.Build().CreateMachine(context);

            machine.Start();
            machine.Dispose();
            context.Clear();

            Assert.DoesNotThrow(() => machine.Dispose());
            Assert.AreEqual(string.Empty, context.Trace);
        }

        [Test]
        public void EveryOperationIsRejectedAfterDispose()
        {
            var sample = new SampleHierarchy();
            var machine = sample.Build().CreateMachine(new TraceContext());
            machine.Dispose();

            Assert.Throws<ObjectDisposedException>(() => machine.Start());
            Assert.Throws<ObjectDisposedException>(() => machine.Stop());
            Assert.Throws<ObjectDisposedException>(() => machine.Tick(0.1f));
            Assert.Throws<ObjectDisposedException>(() => machine.FixedTick(0.02f));
            Assert.Throws<ObjectDisposedException>(() => machine.Raise(sample.Move));
        }

        [Test]
        public void StartStopAndDisposeAreRejectedFromInsideABlock()
        {
            var sample = new SampleHierarchy();
            var reentrant = new ReentrantAction();
            sample.Builder.State(sample.Idle).OnTick(() => reentrant);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                reentrant.Machine = machine;
                machine.Start();

                Assert.Throws<InvalidOperationException>(() => machine.Tick(0.1f));
                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
            }
        }

        [Test]
        public void RaiseIsRejectedAfterAFault()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).OnTick(() => new ThrowingAction("boom"));

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();
                Assert.Throws<InvalidOperationException>(() => machine.Tick(0.1f));

                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
                Assert.Throws<InvalidOperationException>(() => machine.Raise(sample.Move));
                Assert.Throws<InvalidOperationException>(() => machine.Start());
                Assert.Throws<InvalidOperationException>(() => machine.Tick(0.1f));
                Assert.Throws<InvalidOperationException>(() => machine.FixedTick(0.02f));
            }
        }

        [Test]
        public void StopOnAFaultedMachineDoesNotRunExitBlocksAgain()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Idle).OnTick(() => new ThrowingAction("boom"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                Assert.Throws<InvalidOperationException>(() => machine.Tick(0.1f));
                context.Clear();

                machine.Stop();

                Assert.AreEqual(string.Empty, context.Trace);
                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
            }
        }

        private sealed class ReentrantAction : IFyniteAction<ITraceContext>
        {
            internal IFyniteMachine Machine { get; set; }

            public void Execute(ITraceContext context, in FyniteExecution execution) => Machine.Stop();
        }
    }
}
