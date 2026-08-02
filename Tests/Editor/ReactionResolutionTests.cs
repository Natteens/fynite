using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class ReactionResolutionTests
    {
        [Test]
        public void HigherPriorityWinsEvenWhenItsSourceIsShallower()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising).Priority(10);
            sample.Builder.State(sample.Idle).On(sample.Jump).TransitionTo(sample.Moving).Priority(0);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();
                machine.Raise(sample.Jump);

                Assert.AreEqual(sample.Rising, machine.ActiveLeaf);
            }
        }

        [Test]
        public void DeeperSourceWinsOnEqualPriority()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Alive).On(sample.Damage).TransitionTo(sample.Falling);
            sample.Builder.State(sample.Grounded).On(sample.Damage).TransitionTo(sample.Moving);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();
                machine.Raise(sample.Damage);

                Assert.AreEqual(sample.Moving, machine.ActiveLeaf);
            }
        }

        [Test]
        public void EarlierRegistrationWinsOnEqualPriorityAndDepth()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Falling);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();
                machine.Raise(sample.Move);

                Assert.AreEqual(sample.Moving, machine.ActiveLeaf);
            }
        }

        [Test]
        public void PriorityBeatsRegistrationOrderWithinAState()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving).Priority(0);
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Falling).Priority(5);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();
                machine.Raise(sample.Move);

                Assert.AreEqual(sample.Falling, machine.ActiveLeaf);
            }
        }

        [Test]
        public void DepthBeatsRegistrationOrder()
        {
            var sample = new SampleHierarchy();
            // The shallower reaction is registered first, yet the deeper source still wins.
            sample.Builder.State(sample.Alive).On(sample.Damage).TransitionTo(sample.Falling);
            sample.Builder.State(sample.Idle).On(sample.Damage).TransitionTo(sample.Moving);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();
                machine.Raise(sample.Damage);

                Assert.AreEqual(sample.Moving, machine.ActiveLeaf);
            }
        }

        [Test]
        public void AllGuardsMustPassForAReactionToRun()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new ConstantGuard(true, "a"))
                .When(() => new ConstantGuard(true, "b"))
                .TransitionTo(sample.Moving);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(sample.Move);

                Assert.AreEqual("guard:a=True,guard:b=True", context.Trace);
                Assert.AreEqual(sample.Moving, machine.ActiveLeaf);
            }
        }

        [Test]
        public void GuardsShortCircuitOnTheFirstFalse()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new ConstantGuard(false, "first"))
                .When(() => new ConstantGuard(true, "second"))
                .TransitionTo(sample.Moving);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(sample.Move);

                Assert.AreEqual("guard:first=False", context.Trace);
                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
            }
        }

        [Test]
        public void ARejectedHighPriorityReactionLetsALowerOneWin()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Grounded)
                .On(sample.Jump)
                .When(() => new ConstantGuard(false, "cannot-jump"))
                .TransitionTo(sample.Rising)
                .Priority(10);
            sample.Builder.State(sample.Idle)
                .On(sample.Jump)
                .TransitionTo(sample.Moving)
                .Priority(0);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(sample.Jump);

                Assert.AreEqual("guard:cannot-jump=False", context.Trace);
                Assert.AreEqual(sample.Moving, machine.ActiveLeaf);
            }
        }

        [Test]
        public void GuardVerdictCanChangeBetweenDispatches()
        {
            var sample = new SampleHierarchy();
            var allow = new GuardSwitch { Allow = false };
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new SwitchableGuard(allow, "gate"))
                .TransitionTo(sample.Moving);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                machine.Raise(sample.Move);
                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);

                allow.Allow = true;
                machine.Raise(sample.Move);
                Assert.AreEqual(sample.Moving, machine.ActiveLeaf);
            }
        }

        [Test]
        public void GuardsReceiveTheContextSignalAndTransitionEndpoints()
        {
            var sample = new SampleHierarchy();
            var probe = new ProbeGuard();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => probe)
                .TransitionTo(sample.Moving);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(sample.Move);

                Assert.AreSame(context, probe.SeenContext);
                Assert.AreEqual(sample.Move, probe.SeenSignal);
                Assert.AreEqual(sample.Idle, probe.SeenSource);
                Assert.AreEqual(sample.Moving, probe.SeenTarget);
                Assert.AreEqual(FynitePhase.Guard, probe.SeenPhase);
            }
        }

        [Test]
        public void ReactionWithoutTargetRunsEffectsAndLeavesThePathUntouched()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Grounded)
                .On(sample.Interact)
                .When(() => new ConstantGuard(true, "has-target"))
                .Do(() => new TraceAction("interact-one"))
                .Do(() => new TraceAction("interact-two"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Raise(sample.Interact);

                Assert.AreEqual("guard:has-target=True,interact-one,interact-two", context.Trace);
                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
                Assert.AreEqual(4, machine.ActiveDepth);
            }
        }

        [Test]
        public void OnlyStatesOnTheActivePathCanReact()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Airborne).On(sample.Move).TransitionTo(sample.Falling);

            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();
                machine.Raise(sample.Move);

                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
                Assert.IsTrue(diagnostics.Contains("unhandled:Move"));
            }
        }

        [Test]
        public void ExactlyOneReactionRunsPerSignal()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).Do(() => new TraceAction("idle-effect"));
            sample.Builder.State(sample.Grounded).On(sample.Move).Do(() => new TraceAction("grounded-effect"));
            sample.Builder.State(sample.Alive).On(sample.Move).Do(() => new TraceAction("alive-effect"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(sample.Move);

                Assert.AreEqual("idle-effect", context.Trace);
            }
        }
    }
}
