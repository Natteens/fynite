using System;
using NUnit.Framework;

namespace Fynite.Tests
{
    [TestFixture]
    internal sealed class MachineOptionsTests
    {
        [Test]
        public void DefaultReturnsAFreshInstanceEveryTime()
        {
            var first = FyniteMachineOptions.Default;
            var second = FyniteMachineOptions.Default;

            Assert.AreNotSame(first, second);

            first.MaxMicrostepsPerPump = 1;
            first.InitialSignalCapacity = 1;

            Assert.AreEqual(128, second.MaxMicrostepsPerPump);
            Assert.AreEqual(16, second.InitialSignalCapacity);
        }

        [Test]
        public void MutatingDefaultDoesNotRetuneMachinesCreatedAfterwards()
        {
            // A shared mutable Default would let this line shrink every later machine's budget to one
            // microstep, turning the two-step pump below into a FyniteCycleException.
            FyniteMachineOptions.Default.MaxMicrostepsPerPump = 1;
            FyniteMachineOptions.Default.InitialSignalCapacity = 1;

            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .Do(() => new RaiseAction("chain", sample.Jump))
                .TransitionTo(sample.Moving);
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            using (var machine = sample.Build().CreateMachine(new TraceContext()))
            {
                machine.Start();

                Assert.DoesNotThrow(() => machine.Raise(sample.Move));

                Assert.AreEqual(sample.Rising, machine.ActiveLeaf);
                Assert.AreEqual(FyniteLifecycle.Running, machine.Lifecycle);
            }
        }

        [Test]
        public void ExplicitOptionsAreStillHonoured()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .Do(() => new RaiseAction("chain", sample.Jump))
                .TransitionTo(sample.Moving);
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            var options = new FyniteMachineOptions { MaxMicrostepsPerPump = 1 };
            using (var machine = sample.Build().CreateMachine(new TraceContext(), null, options))
            {
                machine.Start();

                var error = Assert.Throws<FyniteCycleException>(() => machine.Raise(sample.Move));
                Assert.AreEqual(1, error.Limit);
            }
        }

        [Test]
        public void OptionsAreCopiedSoLaterMutationDoesNotAffectARunningMachine()
        {
            var sample = new SampleHierarchy();
            sample.Builder.State(sample.Idle)
                .On(sample.Move)
                .Do(() => new RaiseAction("chain", sample.Jump))
                .TransitionTo(sample.Moving);
            sample.Builder.State(sample.Grounded).On(sample.Jump).TransitionTo(sample.Rising);

            var options = new FyniteMachineOptions { MaxMicrostepsPerPump = 64 };
            using (var machine = sample.Build().CreateMachine(new TraceContext(), null, options))
            {
                machine.Start();
                options.MaxMicrostepsPerPump = 1;

                Assert.DoesNotThrow(() => machine.Raise(sample.Move));
                Assert.AreEqual(sample.Rising, machine.ActiveLeaf);
            }
        }

        [Test]
        public void InvalidOptionsAreRejectedAtMachineCreation()
        {
            var definition = new SampleHierarchy().Build();

            Assert.Throws<ArgumentOutOfRangeException>(() => definition.CreateMachine(
                new TraceContext(),
                null,
                new FyniteMachineOptions { InitialSignalCapacity = 0 }));

            Assert.Throws<ArgumentOutOfRangeException>(() => definition.CreateMachine(
                new TraceContext(),
                null,
                new FyniteMachineOptions { MaxMicrostepsPerPump = -1 }));
        }
    }
}
