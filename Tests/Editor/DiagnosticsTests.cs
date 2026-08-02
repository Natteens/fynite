using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class DiagnosticsTests
    {
        [Test]
        public void StartAndStopReportEveryStateTransitionOfTheActivePath()
        {
            var sample = new SampleHierarchy();
            var diagnostics = new RecordingDiagnostics();

            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();
                machine.Stop();
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "entered:Root", "entered:Alive", "entered:Grounded", "entered:Idle",
                    "exited:Idle", "exited:Grounded", "exited:Alive", "exited:Root"
                },
                diagnostics.Events);
        }

        [Test]
        public void ATransitionReportsQueueDispatchSelectionAndBoundaries()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();
                diagnostics.Events.Clear();

                machine.Raise(sample.Jump);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "queued:Jump",
                    "dispatch:Jump",
                    "selected:Grounded",
                    "transition:Grounded->Rising",
                    "exited:Idle",
                    "exited:Grounded",
                    "entered:Airborne",
                    "entered:Rising",
                    "completed:Rising",
                    "exited:Rising",
                    "exited:Airborne",
                    "exited:Alive",
                    "exited:Root"
                },
                diagnostics.Events);
        }

        [Test]
        public void SelectionCarriesPriorityDepthAndRegistrationOrder()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising).Priority(7);

            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();
                machine.Raise(sample.Jump);
            }

            Assert.AreEqual(1, diagnostics.Selections.Count);
            var selected = diagnostics.Selections[0];
            Assert.AreEqual(sample.Grounded, selected.Source);
            Assert.AreEqual(sample.Rising, selected.Target);
            Assert.AreEqual(7, selected.Priority);
            Assert.AreEqual(2, selected.Depth);
            Assert.AreEqual(0, selected.RegistrationOrder);
            Assert.AreEqual(-1, selected.RejectedGuardIndex);
            Assert.IsTrue(selected.IsTransition);
        }

        [Test]
        public void RejectionReportsWhichGuardFailed()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new ConstantGuard(true, "a"))
                .When(() => new ConstantGuard(false, "b"))
                .TransitionTo(sample.Moving);

            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();
                machine.Raise(sample.Move);
            }

            Assert.AreEqual(1, diagnostics.Rejections.Count);
            Assert.AreEqual(1, diagnostics.Rejections[0].RejectedGuardIndex);
            Assert.AreEqual(sample.Idle, diagnostics.Rejections[0].Source);
            Assert.IsTrue(diagnostics.Contains("unhandled:Move"));
        }

        [Test]
        public void EffectOnlyReactionsAreReportedWithoutATransition()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Interact).Do(() => new TraceAction("interacted"));

            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();
                diagnostics.Events.Clear();

                machine.Raise(sample.Interact);
            }

            CollectionAssert.AreEqual(
                new[] { "queued:Interact", "dispatch:Interact", "selected:Idle" },
                diagnostics.Events.GetRange(0, 3));
            Assert.AreEqual(0, diagnostics.TransitionsStarted.Count);
            Assert.IsFalse(diagnostics.Selections[0].IsTransition);
        }

        [Test]
        public void TransitionEventsCarryTheLeastCommonAncestor()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);

            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();
                machine.Raise(sample.Move);
                machine.Raise(sample.Jump);
            }

            Assert.AreEqual(2, diagnostics.TransitionsStarted.Count);
            Assert.AreEqual(sample.Grounded, diagnostics.TransitionsStarted[0].LeastCommonAncestor);
            Assert.AreEqual(sample.Alive, diagnostics.TransitionsStarted[1].LeastCommonAncestor);
        }

        [Test]
        public void UnhandledSignalsAreReported()
        {
            var sample = new SampleHierarchy();
            var diagnostics = new RecordingDiagnostics();

            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();
                machine.Raise(sample.Damage);
            }

            Assert.AreEqual(1, diagnostics.Unhandled.Count);
            Assert.AreEqual(sample.Damage, diagnostics.Unhandled[0].Signal.Handle);
            Assert.IsFalse(diagnostics.Unhandled[0].Signal.HasPayload);
        }

        [Test]
        public void DiagnosticEventsCanResolveNamesThroughTheDefinition()
        {
            var sample = new SampleHierarchy();
            var diagnostics = new RecordingDiagnostics();

            using (var machine = sample.Build().CreateMachine(new TraceContext(), diagnostics))
            {
                machine.Start();
            }

            Assert.IsTrue(diagnostics.Contains("entered:Grounded"));
        }

        [Test]
        public void NoDiagnosticsSinkIsRequired()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                Assert.DoesNotThrow(() =>
                {
                    machine.Start();
                    machine.Raise(sample.Move);
                    machine.Tick(0.1f);
                    machine.Stop();
                });
            }
        }
    }
}
