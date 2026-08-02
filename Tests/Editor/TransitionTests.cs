using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class TransitionTests
    {
        [Test]
        public void CrossBranchTransitionPreservesTheCommonAncestors()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Grounded)
                .On(sample.Jump)
                .Do(() => new TraceAction("effect"))
                .TransitionTo(sample.Rising);
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(sample.Move);
                context.Clear();

                machine.Raise(sample.Jump);

                Assert.AreEqual(
                    "Exit:Moving,Exit:Grounded,effect,Enter:Airborne,Enter:Rising",
                    context.Trace);
                Assert.AreEqual(sample.Rising, machine.ActiveLeaf);
                Assert.IsTrue(machine.IsActive(sample.Root));
                Assert.IsTrue(machine.IsActive(sample.Alive));
                Assert.IsFalse(machine.IsActive(sample.Grounded));
            }
        }

        [Test]
        public void SiblingTransitionOnlySwapsTheLeaf()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .Do(() => new TraceAction("effect"))
                .TransitionTo(sample.Moving);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Raise(sample.Move);

                Assert.AreEqual("Exit:Idle,effect,Enter:Moving", context.Trace);
                Assert.IsTrue(machine.IsActive(sample.Grounded));
                Assert.IsTrue(machine.IsActive(sample.Alive));
                Assert.IsTrue(machine.IsActive(sample.Root));
                Assert.AreEqual(sample.Moving, machine.ActiveLeaf);
            }
        }

        [Test]
        public void CompositeTargetFollowsItsInitialChain()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Airborne);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Raise(sample.Jump);

                Assert.AreEqual(
                    "Exit:Idle,Exit:Grounded,Enter:Airborne,Enter:Rising",
                    context.Trace);
                Assert.AreEqual(sample.Rising, machine.ActiveLeaf);
            }
        }

        [Test]
        public void TransitionToAnActiveAncestorReentersItsInitialChainOnly()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);
            sample.Builder.State(sample.Moving)
                .On(sample.Stop)
                .Do(() => new TraceAction("effect"))
                .TransitionTo(sample.Grounded);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(sample.Move);
                context.Clear();

                machine.Raise(sample.Stop);

                // Grounded is the least common ancestor, so it stays active and is not re-entered.
                Assert.AreEqual("Exit:Moving,effect,Enter:Idle", context.Trace);
                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
            }
        }

        [Test]
        public void TransitionDeclaredOnAnAncestorToItsDescendantKeepsTheAncestorActive()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Grounded)
                .On(sample.Move)
                .Do(() => new TraceAction("effect"))
                .TransitionTo(sample.Moving);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Raise(sample.Move);

                Assert.AreEqual("Exit:Idle,effect,Enter:Moving", context.Trace);
                Assert.IsTrue(machine.IsActive(sample.Grounded));
            }
        }

        [Test]
        public void SelfTransitionOnALeafRestartsIt()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .Do(() => new TraceAction("effect"))
                .TransitionTo(sample.Idle);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Raise(sample.Move);

                Assert.AreEqual("Exit:Idle,effect,Enter:Idle", context.Trace);
                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
            }
        }

        [Test]
        public void SelfTransitionOnACompositeRestartsItAndItsInitialChain()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);
            sample.Builder.State(sample.Grounded)
                .On(sample.Stop)
                .Do(() => new TraceAction("effect"))
                .TransitionTo(sample.Grounded);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(sample.Move);
                context.Clear();

                machine.Raise(sample.Stop);

                Assert.AreEqual(
                    "Exit:Moving,Exit:Grounded,effect,Enter:Grounded,Enter:Idle",
                    context.Trace);
                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
                Assert.IsTrue(machine.IsActive(sample.Alive));
            }
        }

        [Test]
        public void SelfTransitionOnTheRootRestartsTheWholeMachine()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Root).On(sample.Stop).TransitionTo(sample.Root);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Raise(sample.Stop);

                Assert.AreEqual(
                    "Exit:Idle,Exit:Grounded,Exit:Alive,Exit:Root,Enter:Root,Enter:Alive,Enter:Grounded,Enter:Idle",
                    context.Trace);
                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
            }
        }

        [Test]
        public void EffectsRunBetweenExitsAndEnters()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Grounded)
                .On(sample.Jump)
                .Do(() => new TraceAction("effect-one"))
                .Do(() => new TraceAction("effect-two"))
                .TransitionTo(sample.Rising);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Raise(sample.Jump);

                Assert.AreEqual(
                    "Exit:Idle,Exit:Grounded,effect-one,effect-two,Enter:Airborne,Enter:Rising",
                    context.Trace);
            }
        }

        [Test]
        public void TransitionsChangeWhichTickBlocksRun()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(sample.Jump);
                context.Clear();

                machine.Tick(0.1f);

                Assert.AreEqual("Tick:Root,Tick:Alive,Tick:Airborne,Tick:Rising", context.Trace);
            }
        }

        [Test]
        public void DeepTargetEntersEveryIntermediateState()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Root).On(sample.Fall).TransitionTo(sample.Falling);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Raise(sample.Fall);

                // Root is the least common ancestor, so it is preserved while everything below is rebuilt.
                Assert.AreEqual(
                    "Exit:Idle,Exit:Grounded,Exit:Alive,Enter:Alive,Enter:Airborne,Enter:Falling",
                    context.Trace);
                Assert.AreEqual(sample.Falling, machine.ActiveLeaf);
            }
        }
    }
}
