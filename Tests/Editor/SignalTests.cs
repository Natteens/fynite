using System;
using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class SignalTests
    {
        [Test]
        public void SignalsAreProcessedInFifoOrder()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move).Do(() => new TraceAction("move")).On(sample.Stop).Do(() => new TraceAction("stop"))
                .On(sample.Jump).Do(() => new TraceAction("jump"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(sample.Jump);
                machine.Raise(sample.Move);
                machine.Raise(sample.Stop);

                Assert.AreEqual("jump,move,stop", context.Trace);
            }
        }

        [Test]
        public void SignalsQueuedInsideABlockKeepTheirOrder()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnEnter(() => new RaiseAction("raise-move", sample.Move))
                .OnEnter(() => new RaiseAction("raise-stop", sample.Stop))
                .On(sample.Move).Do(() => new TraceAction("handled-move"))
                .On(sample.Stop).Do(() => new TraceAction("handled-stop"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();

                Assert.AreEqual("raise-move,raise-stop,handled-move,handled-stop", context.Trace);
            }
        }

        [Test]
        public void UnhandledSignalsAreDroppedImmediately()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Airborne).On(sample.Jump).Do(() => new TraceAction("airborne-jump"));
            sample.Builder.State(sample.Grounded).On(sample.Move).TransitionTo(sample.Airborne);

            var context = new TraceContext();
            var diagnostics = new RecordingDiagnostics();
            using (var machine = sample.Build().CreateMachine(context, diagnostics))
            {
                machine.Start();

                machine.Raise(sample.Jump);
                Assert.AreEqual(string.Empty, context.Trace);
                Assert.IsTrue(diagnostics.Contains("unhandled:Jump"));

                // Reaching a state that does handle it must not resurrect the dropped signal.
                machine.Raise(sample.Move);
                Assert.AreEqual(sample.Rising, machine.ActiveLeaf);
                Assert.AreEqual(string.Empty, context.Trace);
            }
        }

        [Test]
        public void SignalsRaisedDuringEnterAreDispatchedAfterTheWholeInitialChain()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Alive).OnEnter(() => new RaiseAction("alive-raises", sample.Jump));
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();

                Assert.AreEqual(
                    "Enter:Root,Enter:Alive,alive-raises,Enter:Grounded,Enter:Idle," +
                    "Exit:Idle,Exit:Grounded,Enter:Airborne,Enter:Rising",
                    context.Trace);
                Assert.AreEqual(sample.Rising, machine.ActiveLeaf);
            }
        }

        [Test]
        public void SignalsRaisedDuringTickAreDispatchedAfterEveryTickBlock()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Grounded).OnTick(() => new RaiseAction("grounded-raises", sample.Jump));
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Tick(0.1f);

                Assert.AreEqual(
                    "Tick:Root,Tick:Alive,Tick:Grounded,grounded-raises,Tick:Idle," +
                    "Exit:Idle,Exit:Grounded,Enter:Airborne,Enter:Rising",
                    context.Trace);
            }
        }

        [Test]
        public void SignalsRaisedDuringFixedTickAreDispatchedAfterEveryFixedTickBlock()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Grounded).OnFixedTick(() => new RaiseAction("grounded-raises", sample.Jump));
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.FixedTick(0.02f);

                Assert.AreEqual(
                    "FixedTick:Root,FixedTick:Alive,FixedTick:Grounded,grounded-raises,FixedTick:Idle," +
                    "Exit:Idle,Exit:Grounded,Enter:Airborne,Enter:Rising",
                    context.Trace);
            }
        }

        [Test]
        public void AnEffectRaisingASignalDoesNotInterruptTheCurrentTransition()
        {
            var sample = new SampleHierarchy().TraceAllPhases();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .Do(() => new RaiseAction("effect-raises", sample.Jump))
                .Do(() => new TraceAction("effect-after-raise"))
                .TransitionTo(sample.Moving);
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                context.Clear();

                machine.Raise(sample.Move);

                Assert.AreEqual(
                    "Exit:Idle,effect-raises,effect-after-raise,Enter:Moving," +
                    "Exit:Moving,Exit:Grounded,Enter:Airborne,Enter:Rising",
                    context.Trace);
            }
        }

        [Test]
        public void SeparateRaisesAreHandledOneAfterTheOther()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .When(() => new ConstantGuard(true, "ok"))
                .Do(() => new TraceAction("moved"))
                .TransitionTo(sample.Moving);
            sample.Builder.State(sample.Moving).On(sample.Stop).Do(() => new TraceAction("stopped"));

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(sample.Move);
                machine.Raise(sample.Stop);

                Assert.AreEqual("guard:ok=True,moved,stopped", context.Trace);
            }
        }

        [Test]
        public void RaisingFromOutsideDrainsTheQueueImmediately()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();
                machine.Raise(sample.Move);

                Assert.AreEqual(sample.Moving, machine.ActiveLeaf);
            }
        }

        [Test]
        public void SignalsRaisedBeforeStartAreDiscarded()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Raise(sample.Move);
                machine.Start();

                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
            }
        }

        [Test]
        public void SignalsRaisedWhileStoppingAreDiscarded()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .OnExit(() => new RaiseAction("idle-exit-raises", sample.Move))
                .On(sample.Move).TransitionTo(sample.Moving);

            var context = new TraceContext();
            using (var machine = sample.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Stop();

                Assert.AreEqual("idle-exit-raises", context.Trace);
                Assert.AreEqual(FyniteLifecycle.Stopped, machine.Lifecycle);
            }
        }

        [Test]
        public void TypedPayloadReachesTheEffect()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            builder.SetRoot(root);
            var damage = builder.AddSignal<int>("Damage");
            builder.State(root).On(damage).Do(() => new PayloadAction("damaged"));

            var context = new TraceContext();
            using (var machine = builder.Build().CreateMachine(context))
            {
                machine.Start();
                machine.Raise(damage, 42);

                Assert.AreEqual("damaged:42", context.Trace);
            }
        }

        [Test]
        public void PayloadTypeIsValidated()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            builder.SetRoot(root);
            var damage = builder.AddSignal<int>("Damage");
            var plain = builder.AddSignal("Plain");

            using (var machine = builder.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                var wrongType = Assert.Throws<ArgumentException>(() => machine.Raise(damage.Handle, "not an int"));
                StringAssert.Contains("expects a payload of type", wrongType.Message);

                var missing = Assert.Throws<ArgumentException>(() => machine.Raise(damage.Handle));
                StringAssert.Contains("requires a payload", missing.Message);

                var unexpected = Assert.Throws<ArgumentException>(() => machine.Raise(plain, 1));
                StringAssert.Contains("declared without a payload", unexpected.Message);
            }
        }

        [Test]
        public void SignalStructExposesItsPayload()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            builder.SetRoot(root);
            var damage = builder.AddSignal<int>("Damage");

            var probe = new ExecutionProbeAction();
            builder.State(root).On(damage).Do(() => probe);

            using (var machine = builder.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();
                machine.Raise(damage, 7);

                Assert.IsTrue(probe.Last.HasSignal);
                Assert.IsTrue(probe.Last.Signal.HasPayload);
                Assert.AreEqual(7, probe.Last.Signal.GetPayload<int>());
                Assert.IsTrue(probe.Last.Signal.TryGetPayload<int>(out int value));
                Assert.AreEqual(7, value);
                Assert.IsFalse(probe.Last.Signal.TryGetPayload<string>(out _));
                Assert.Throws<InvalidOperationException>(() => probe.Last.Signal.GetPayload<string>());
            }
        }

        [Test]
        public void RaisingASignalFromAnotherDefinitionIsRejected()
        {
            var sample = new SampleHierarchy();
            var foreign = new SampleHierarchy();
            foreign.Build();

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                var error = Assert.Throws<ArgumentException>(() => machine.Raise(foreign.Move));
                StringAssert.Contains("does not belong to this definition", error.Message);

                Assert.Throws<ArgumentException>(() => machine.Raise(SignalHandle.Invalid));
            }
        }

        [Test]
        public void MicrostepLimitStopsAnEnterFeedbackLoop()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            var a = builder.AddState("A", root);
            var b = builder.AddState("B", root);
            builder.SetRoot(root);
            builder.SetInitial(root, a);

            var goA = builder.AddSignal("GoA");
            var goB = builder.AddSignal("GoB");

            builder.State(a).OnEnter(() => new RaiseAction("enter-a", goB));
            builder.State(b).OnEnter(() => new RaiseAction("enter-b", goA));
            builder.State(a).On(goB).TransitionTo(b);
            builder.State(b).On(goA).TransitionTo(a);

            var diagnostics = new RecordingDiagnostics();
            var options = new FyniteMachineOptions { MaxMicrostepsPerPump = 8 };
            using (var machine = builder.Build().CreateMachine(new TraceContext(), diagnostics, options))
            {
                var error = Assert.Throws<FyniteCycleException>(() => machine.Start());

                Assert.AreEqual(8, error.Limit);
                Assert.AreEqual(9, error.Microsteps);
                StringAssert.Contains("microsteps", error.Message);
                Assert.AreEqual(1, diagnostics.MicrostepOverflows.Count);
                Assert.AreEqual(FyniteLifecycle.Faulted, machine.Lifecycle);
                Assert.IsTrue(diagnostics.Contains("faulted:FyniteCycleException"));
            }
        }

        [Test]
        public void MicrostepLimitCountsEffectOnlyReactions()
        {
            var builder = new FyniteBuilder<TraceContext>();
            var root = builder.AddState("Root");
            builder.SetRoot(root);
            var ping = builder.AddSignal("Ping");

            builder.State(root).On(ping).Do(() => new RaiseAction("ping", ping));

            var options = new FyniteMachineOptions { MaxMicrostepsPerPump = 4 };
            using (var machine = builder.Build().CreateMachine(new TraceContext(), null, options))
            {
                machine.Start();

                var error = Assert.Throws<FyniteCycleException>(() => machine.Raise(ping));
                Assert.AreEqual(4, error.Limit);
            }
        }

        [Test]
        public void MicrostepBudgetIsPerPumpNotPerLifetime()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle).On(sample.Move).TransitionTo(sample.Moving);
            sample.Builder.State(sample.Moving).On(sample.Stop).TransitionTo(sample.Idle);

            var options = new FyniteMachineOptions { MaxMicrostepsPerPump = 2 };
            using (var machine = sample.Build().CreateMachine(new TraceContext(), null, options))
            {
                machine.Start();

                for (int i = 0; i < 10; i++)
                {
                    machine.Raise(sample.Move);
                    machine.Raise(sample.Stop);
                }

                Assert.AreEqual(sample.Idle, machine.ActiveLeaf);
                Assert.AreEqual(FyniteLifecycle.Running, machine.Lifecycle);
            }
        }
    }
}
