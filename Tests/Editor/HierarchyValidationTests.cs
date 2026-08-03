using System;
using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    public sealed class HierarchyValidationTests : MachineFixture
    {
        [Test]
        public void SelfParentIsRejected()
        {
            var builder = Attach().Start<GroundedProbe>();

            var error = Assert.Throws<InvalidOperationException>(
                () => builder.Child<GroundedProbe, GroundedProbe>());

            Assert.That(error.Message, Does.Contain("GroundedProbe"));
            Assert.That(error.Message, Does.Contain("child of itself"));
        }

        [Test]
        public void TwoParentsAreRejected()
        {
            var builder = Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, AttackProbe>();

            var error = Assert.Throws<InvalidOperationException>(
                () => builder.Child<AirborneProbe, AttackProbe>());

            Assert.That(error.Message, Does.Contain("AttackProbe"));
            Assert.That(error.Message, Does.Contain("GroundedProbe"));
            Assert.That(error.Message, Does.Contain("AirborneProbe"));
        }

        [Test]
        public void DirectCycleIsRejected()
        {
            var builder = Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<AttackProbe, GroundedProbe>();

            var error = Assert.Throws<InvalidOperationException>(() => builder.Build());

            Assert.That(error.Message, Does.Contain("hierarchy cycle detected"));
            Assert.That(error.Message, Does.Contain("GroundedProbe"));
            Assert.That(error.Message, Does.Contain("AttackProbe"));
        }

        [Test]
        public void IndirectCycleIsRejected()
        {
            var builder = Attach()
                .Start<LimboProbe>()
                .Child<TopProbe, MidProbe>()
                .Child<MidProbe, LeafProbe>()
                .Child<LeafProbe, TopProbe>();

            var error = Assert.Throws<InvalidOperationException>(() => builder.Build());

            Assert.That(error.Message, Does.Contain("hierarchy cycle detected"));
            Assert.That(error.Message, Does.Contain("MidProbe"));
        }

        [Test]
        public void InitialChildThatIsNotADirectChildIsRejected()
        {
            var builder = Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .InitialChild<GroundedProbe, FallingProbe>();

            var error = Assert.Throws<InvalidOperationException>(() => builder.Build());

            Assert.That(error.Message, Does.Contain("FallingProbe"));
            Assert.That(error.Message, Does.Contain("GroundedProbe"));
            Assert.That(error.Message, Does.Contain("not a direct child"));
        }

        [Test]
        public void InitialChildOfAGrandchildIsRejected()
        {
            var builder = Attach()
                .Start<TopProbe>()
                .Child<TopProbe, MidProbe>()
                .Child<MidProbe, LeafProbe>()
                .InitialChild<TopProbe, LeafProbe>();

            var error = Assert.Throws<InvalidOperationException>(() => builder.Build());

            Assert.That(error.Message, Does.Contain("not a direct child"));
        }

        [Test]
        public void StartOnAChildIsRejected()
        {
            var builder = Attach()
                .Start<LocomotionProbe>()
                .Child<GroundedProbe, LocomotionProbe>();

            var error = Assert.Throws<InvalidOperationException>(() => builder.Build());

            Assert.That(
                error.Message,
                Is.EqualTo(
                    "Fynite: Start<LocomotionProbe>() is invalid because LocomotionProbe is a child " +
                    "of GroundedProbe."));
        }

        [Test]
        public void StartIsStillRequiredWithAHierarchy()
        {
            var builder = Attach().Child<GroundedProbe, LocomotionProbe>();

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Test]
        public void RejectedBuildLeavesNothingRegistered()
        {
            var builder = Attach()
                .Start<LocomotionProbe>()
                .Child<GroundedProbe, LocomotionProbe>();

            Assert.Throws<InvalidOperationException>(() => builder.Build());

            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
            Assert.That(Context.Log, Is.Empty);
        }
    }
}
