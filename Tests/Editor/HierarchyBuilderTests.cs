using System;
using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    public sealed class HierarchyBuilderTests : MachineFixture
    {
        [Test]
        public void FlatMachineNeedsNoChildDeclaration()
        {
            var machine = BuildLocomotion();

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Enter"));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(IdleProbe)));
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        [Test]
        public void Child_RegistersParentAndChild()
        {
            var machine = Track(Attach()
                .Child<GroundedProbe, LocomotionProbe>()
                .Start<GroundedProbe>()
                .Build());

            Assert.That(Context.Trace, Is.EqualTo("GroundedProbe.Enter,LocomotionProbe.Enter"));
            Assert.That(machine.IsIn<GroundedProbe>(), Is.True);
            Assert.That(machine.IsIn<LocomotionProbe>(), Is.True);
        }

        [Test]
        public void FirstDeclaredChildIsTheInitialChild()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Build());

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(AttackProbe)));
            Assert.That(Context.Trace, Is.EqualTo("GroundedProbe.Enter,AttackProbe.Enter"));
        }

        [Test]
        public void InitialChild_OverridesTheConvention()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .InitialChild<GroundedProbe, AttackProbe>()
                .Build());

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(AttackProbe)));
            Assert.That(Context.Trace, Is.EqualTo("GroundedProbe.Enter,AttackProbe.Enter"));
        }

        [Test]
        public void InitialChild_CanBeDeclaredBeforeTheRelation()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .InitialChild<GroundedProbe, AttackProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Build());

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(AttackProbe)));
        }

        [Test]
        public void InitialChild_LastCallWins()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .InitialChild<GroundedProbe, AttackProbe>()
                .InitialChild<GroundedProbe, LocomotionProbe>()
                .Build());

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LocomotionProbe)));
        }

        [Test]
        public void EachStateIsInstantiatedOnce()
        {
            BuildBranches();

            Assert.That(ProbeState.Instances, Is.EqualTo(5));
        }

        [Test]
        public void RelationsCanBeDeclaredBeforeTheTransitionModules()
        {
            var machine = Track(Attach()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Start<GroundedProbe>()
                .Use<PlayerBranchModule>()
                .Build());

            Context.ToAttack = true;
            machine.Tick(0.1f);

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(AttackProbe)));
        }

        [Test]
        public void RelationsCanReuseTypesRegisteredByStart()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Build());

            Assert.That(ProbeState.Instances, Is.EqualTo(2));
            Assert.That(machine.IsIn<GroundedProbe>(), Is.True);
        }

        [Test]
        public void RelationsCanReuseTypesRegisteredByTransitions()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Use<PlayerBranchModule>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<AirborneProbe, FallingProbe>()
                .Build());

            Assert.That(ProbeState.Instances, Is.EqualTo(5));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LocomotionProbe)));
        }

        [Test]
        public void RepeatingTheSameRelationIsIdempotent()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Build());

            Assert.That(ProbeState.Instances, Is.EqualTo(3));
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LocomotionProbe)));
            Assert.That(Context.Trace, Is.EqualTo("GroundedProbe.Enter,LocomotionProbe.Enter"));
        }

        [Test]
        public void ThreeLevelChainIsAccepted()
        {
            var machine = Track(Attach()
                .Start<TopProbe>()
                .Child<TopProbe, MidProbe>()
                .Child<MidProbe, LeafProbe>()
                .Build());

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LeafProbe)));
        }

        [Test]
        public void Builder_RejectsChildAfterBuild()
        {
            var builder = Attach().Start<GroundedProbe>();
            Track(builder.Build());

            Assert.Throws<InvalidOperationException>(
                () => builder.Child<GroundedProbe, LocomotionProbe>());
        }

        [Test]
        public void Builder_RejectsInitialChildAfterBuild()
        {
            var builder = Attach().Start<GroundedProbe>();
            Track(builder.Build());

            Assert.Throws<InvalidOperationException>(
                () => builder.InitialChild<GroundedProbe, LocomotionProbe>());
        }
    }
}
