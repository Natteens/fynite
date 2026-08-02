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
