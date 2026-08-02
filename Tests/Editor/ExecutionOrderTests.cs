using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class ExecutionOrderTests
    {
        [Test]
        public void StartEntersFromRootDownToTheInitialLeaf()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();

                Assert.AreEqual("Enter:Root,Enter:Alive,Enter:Grounded,Enter:Idle", context.Trace);
            }
        }

        [Test]
        public void StartEstablishesTheFullActivePath()
        {
            var sample = new SampleHierarchy();
            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                Assert.AreEqual(4, machine.ActiveDepth);
                Assert.AreEqual(sample.Root, machine.GetActiveState(0));
                Assert.AreEqual(sample.Alive, machine.GetActiveState(1));
                Assert.AreEqual(sample.Grounded, machine.GetActiveState(2));
                Assert.AreEqual(sample.Idle, machine.GetActiveState(3));
                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);

                Assert.IsTrue(machine.IsActive(sample.Root));
                Assert.IsTrue(machine.IsActive(sample.Grounded));
                Assert.IsTrue(machine.IsActive(sample.Idle));
                Assert.IsFalse(machine.IsActive(sample.Moving));
                Assert.IsFalse(machine.IsActive(sample.Airborne));
            }
        }

        [Test]
        public void EnterBlocksOfOneStateRunInRegistrationOrder()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnEnter(() => new TraceAction("first"))
                .OnEnter(() => new TraceAction("second"))
                .OnEnter(() => new TraceAction("third"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();

                Assert.AreEqual("first,second,third", context.Trace);
            }
        }

        [Test]
        public void TickRunsFromRootToLeaf()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Tick(0.5f);

                Assert.AreEqual("Tick:Root,Tick:Alive,Tick:Grounded,Tick:Idle", context.Trace);
            }
        }

        [Test]
        public void TickBlocksOfOneStateRunInRegistrationOrder()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Grounded)
                .OnTick(() => new TraceAction("movement"))
                .OnTick(() => new TraceAction("animation"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Tick(0.1f);

                Assert.AreEqual("movement,animation", context.Trace);
            }
        }

        [Test]
        public void FixedTickIsSeparateFromTick()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Grounded)
                .OnTick(() => new TraceAction("tick"))
                .OnFixedTick(() => new TraceAction("fixed"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();

                machine.Tick(0.1f);
                Assert.AreEqual("tick", context.Trace);

                context.Clear();
                machine.FixedTick(0.02f);
                Assert.AreEqual("fixed", context.Trace);
            }
        }

        [Test]
        public void FixedTickRunsFromRootToLeaf()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.FixedTick(0.02f);

                Assert.AreEqual("FixedTick:Root,FixedTick:Alive,FixedTick:Grounded,FixedTick:Idle", context.Trace);
            }
        }

        [Test]
        public void DeltaTimeReachesTheBlocks()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnTick(() => new DeltaAction("tick"))
                .OnFixedTick(() => new DeltaAction("fixed"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Tick(0.25f);
                machine.FixedTick(0.02f);

                Assert.AreEqual("tick@0.25,fixed@0.02", context.Trace);
            }
        }

        [Test]
        public void StopExitsFromLeafToRoot()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Stop();

                Assert.AreEqual("Exit:Idle,Exit:Grounded,Exit:Alive,Exit:Root", context.Trace);
            }
        }

        [Test]
        public void ExitBlocksOfOneStateRunInRegistrationOrder()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnExit(() => new TraceAction("first"))
                .OnExit(() => new TraceAction("second"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Stop();

                Assert.AreEqual("first,second", context.Trace);
            }
        }

        [Test]
        public void StopClearsThePathAndTheQueue()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);
            sample.Builder.State(sample.Root).OnExit(() => new RaiseAction("exit-raises", sample.Move));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Stop();

                Assert.AreEqual(0, machine.ActiveDepth);
                Assert.IsFalse(machine.ActiveLeaf.IsValid);
                Assert.AreEqual(FyniteLifecycle.Stopped, machine.Lifecycle);
                Assert.IsFalse(machine.IsRunning);

                // The signal raised while exiting was discarded, so restarting is a clean run.
                context.Clear();
                machine.Start();
                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
            }
        }

        [Test]
        public void ActivePathSpanMatchesTheIndexedAccessors()
        {
            var sample = new SampleHierarchy();
            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                var path = machine.ActivePath;
                Assert.AreEqual(machine.ActiveDepth, path.Length);
                for (int depth = 0; depth < path.Length; depth++)
                {
                    Assert.AreEqual(machine.GetActiveState(depth), path[depth]);
                }
            }
        }

        [Test]
        public void TickOnAMachineThatIsNotRunningThrows()
        {
            var sample = new SampleHierarchy();
            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                Assert.Throws<System.InvalidOperationException>(() => machine.Tick(0.1f));
                Assert.Throws<System.InvalidOperationException>(() => machine.FixedTick(0.02f));

                machine.Start();
                machine.Stop();

                Assert.Throws<System.InvalidOperationException>(() => machine.Tick(0.1f));
                Assert.Throws<System.InvalidOperationException>(() => machine.FixedTick(0.02f));
            }
        }

        [Test]
        public void GetActiveStateRejectsOutOfRangeDepth()
        {
            var sample = new SampleHierarchy();
            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                Assert.Throws<System.ArgumentOutOfRangeException>(() => machine.GetActiveState(0));

                machine.Start();
                Assert.Throws<System.ArgumentOutOfRangeException>(() => machine.GetActiveState(4));
                Assert.Throws<System.ArgumentOutOfRangeException>(() => machine.GetActiveState(-1));
            }
        }
    }
}
