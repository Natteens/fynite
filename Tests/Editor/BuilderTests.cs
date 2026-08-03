using System;
using Fynite;
using NUnit.Framework;
using UnityEngine;

namespace FyniteTests
{
    public sealed class BuilderTests : MachineFixture
    {
        [Test]
        public void Attach_RejectsNullOwner()
        {
            Assert.Throws<ArgumentNullException>(() => Machine.Attach(null, Context));
        }

        [Test]
        public void Attach_RejectsDestroyedOwner()
        {
            var owner = NewOwner();
            UnityEngine.Object.DestroyImmediate(owner.gameObject);

            Assert.Throws<ArgumentNullException>(() => Machine.Attach(owner, Context));
        }

        [Test]
        public void Attach_RejectsNullContext()
        {
            Assert.Throws<ArgumentNullException>(() => Machine.Attach<ProbeContext>(Owner, null));
        }

        [Test]
        public void Build_WithoutStart_Throws()
        {
            var builder = Attach().Use<LocomotionModule>();

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Test]
        public void Start_CalledTwice_Throws()
        {
            var builder = Attach().Start<IdleProbe>();

            Assert.Throws<InvalidOperationException>(() => builder.Start<WalkProbe>());
        }

        [Test]
        public void Build_CalledTwice_Throws()
        {
            var builder = Attach().Start<IdleProbe>();
            Track(builder.Build());

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Test]
        public void Builder_RejectsChangesAfterBuild()
        {
            var builder = Attach().Start<IdleProbe>();
            Track(builder.Build());

            Assert.Throws<InvalidOperationException>(() => builder.Use<LocomotionModule>());
        }

        [Test]
        public void Transitions_RejectChangesAfterBuild()
        {
            Track(Attach()
                .Start<IdleProbe>()
                .Use<CapturingModule>()
                .Build());

            var captured = CapturingModule.Captured;

            Assert.Throws<InvalidOperationException>(
                () => captured.From<IdleProbe>().To<WalkProbe>().When<Always>());
        }

        [Test]
        public void ReferencedStates_AreRegisteredAutomatically()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Use<DeathModule>()
                .Build());

            Context.ToDead = true;
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<DeadProbe>(), Is.True);
        }

        [Test]
        public void EachStateIsInstantiatedOnce()
        {
            Track(Attach()
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            Assert.That(ProbeState.Instances, Is.EqualTo(2));
        }

        [Test]
        public void EachModuleIsConfiguredOnce()
        {
            Track(Attach()
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Use<DeathModule>()
                .Build());

            Assert.That(LocomotionModule.Configured, Is.EqualTo(1));
            Assert.That(DeathModule.Configured, Is.EqualTo(1));
        }

        [Test]
        public void When_RejectsNullDelegate()
        {
            Assert.Throws<ArgumentNullException>(
                () => Track(Attach().Start<IdleProbe>().Use<NullPredicateModule>().Build()));
        }

        private sealed class CapturingModule : IFyniteTransitions<ProbeContext>
        {
            public static FyniteTransitions<ProbeContext> Captured;

            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => Captured = transitions;
        }

        private sealed class NullPredicateModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions.From<IdleProbe>().To<WalkProbe>().When(null);
        }
    }
}
