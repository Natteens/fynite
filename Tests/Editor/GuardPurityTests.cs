using System;
using NUnit.Framework;

namespace Fynite.Tests
{
    /// <summary>
    /// Guards are predicates. A guard that raises can starve the executor: it re-queues the very signal
    /// whose reaction it just rejected, and because no reaction is ever selected the microstep budget
    /// never advances, so the pump spins forever. The machine closes that hole by refusing the raise.
    /// </summary>
    [TestFixture]
    internal sealed class GuardPurityTests
    {
        [Test]
        public void AGuardThatRaisesTheSignalItRejectsIsRefusedInsteadOfLooping()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new RaisingGuard(sample.Move, false))
                .TransitionTo(sample.Moving);

            var context = new TraceContext();
            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(context, diagnostics))
            {
                machine.Start();

                // Without the guard-purity rule this call never returns.
                var error = Assert.Throws<InvalidOperationException>(() => machine.Raise(sample.Move));

                StringAssert.Contains("Guards must be pure predicates", error.Message);
                StringAssert.Contains("'Idle'", error.Message);
                StringAssert.Contains("'Move'", error.Message);
                Assert.AreEqual("guard-raises", context.Trace);
            }
        }

        [Test]
        public void ARaisingGuardFaultsTheMachineAndClearsTheQueue()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new RaisingGuard(sample.Jump, false))
                .TransitionTo(sample.Moving);
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();

                Assert.Throws<InvalidOperationException>(() => machine.Raise(sample.Move));

                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
                Assert.AreEqual(1, diagnostics.Faults.Count);
                Assert.IsInstanceOf<InvalidOperationException>(diagnostics.Faults[0]);

                // The queue was cleared, and a faulted machine accepts nothing further.
                Assert.Throws<InvalidOperationException>(() => machine.Raise(sample.Jump));
                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
            }
        }

        [Test]
        public void RaisingIsRefusedEvenWhenTheGuardWouldHavePassed()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new RaisingGuard(sample.Jump, true))
                .TransitionTo(sample.Moving);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                var error = Assert.Throws<InvalidOperationException>(() => machine.Raise(sample.Move));

                StringAssert.Contains("Guards must be pure predicates", error.Message);
                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
            }
        }

        [Test]
        public void ARaisingGuardIsRefusedWithATypedPayloadSignalToo()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            var idle = builder.AddState("Idle", root);
            var busy = builder.AddState("Busy", root);
            builder.SetRoot(root);
            builder.SetInitial(root, idle);

            var start = builder.AddSignal("Start");
            var damage = builder.AddSignal<int>("Damage");

            builder.State(idle).On(start).When(() => new PayloadRaisingGuard(damage)).TransitionTo(busy);

            using (var machine = builder.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                var error = Assert.Throws<InvalidOperationException>(() => machine.Raise(start));
                StringAssert.Contains("Guards must be pure predicates", error.Message);
                StringAssert.Contains("'Damage'", error.Message);
            }
        }

        [Test]
        public void EffectsMayStillRaiseFreely()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new ConstantGuard(true, "ok"))
                .Do(() => new RaiseAction("effect-raises", sample.Jump))
                .TransitionTo(sample.Moving);
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();
                machine.Raise(sample.Move);

                Assert.AreEqual(FyniteLifecycle.Running, machine.Lifecycle);
                Assert.AreEqual(sample.Rising, machine.ActiveLeaf);
            }
        }

        [Test]
        public void TheGuardRestrictionIsLiftedAfterEvaluation()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new ConstantGuard(false, "no"))
                .TransitionTo(sample.Moving);
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                machine.Raise(sample.Move);
                Assert.AreEqual(FyniteLifecycle.Running, machine.Lifecycle);

                machine.Raise(sample.Jump);
                Assert.AreEqual(sample.Rising, machine.ActiveLeaf);
            }
        }

        private sealed class PayloadRaisingGuard : IFyniteGuard<ITraceContext>
        {
            private readonly SignalHandle<int> _signal;

            internal PayloadRaisingGuard(SignalHandle<int> signal) => _signal = signal;

            public bool Evaluate(ITraceContext context, in FyniteExecution execution)
            {
                execution.Signals.Raise(_signal, 1);
                return true;
            }
        }
    }
}
