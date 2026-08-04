using NUnit.Framework;

namespace FyniteTests
{
    /// <summary>
    /// Events go through the same resolution as predicates: globals, then the leaf, then up through
    /// the parents — and through the same switching, so no second way of changing state exists.
    /// </summary>
    public sealed class EventHierarchyTests : MachineFixture
    {
        [Test]
        public void SiblingTransition_KeepsTheParent()
        {
            var machine = BuildEventBranches();
            Context.Log.Clear();

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<AttackProbe>(), Is.True);
            Assert.That(machine.IsIn<GroundedProbe>(), Is.True);
            Assert.That(Context.CountOf("GroundedProbe.Exit"), Is.Zero);
            Assert.That(Context.CountOf("GroundedProbe.Enter"), Is.Zero);
        }

        [Test]
        public void CrossBranchTransition_UsesTheCommonAncestor()
        {
            var machine = BuildEventBranches();
            Context.Log.Clear();

            Context.Beta.Publish();
            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Does.StartWith(
                    "LocomotionProbe.Exit,GroundedProbe.Exit,AirborneProbe.Enter,FallingProbe.Enter"));
            Assert.That(machine.IsIn<AirborneProbe>(), Is.True);
        }

        [Test]
        public void CompositeTarget_EntersItsInitialChild()
        {
            var machine = BuildEventBranches();

            Context.Beta.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(FallingProbe)));
        }

        [Test]
        public void AncestorTarget_RestartsTheInitialChild()
        {
            var machine = BuildEventBranches();

            Context.Alpha.Publish();
            machine.Tick(0.1f);
            Assert.That(machine.IsIn<AttackProbe>(), Is.True);

            Context.Log.Clear();
            Context.Gamma.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LocomotionProbe)));
            Assert.That(Context.CountOf("GroundedProbe.Exit"), Is.Zero, "the active ancestor restarted");
            Assert.That(Context.CountOf("AttackProbe.Exit"), Is.EqualTo(1));
            Assert.That(Context.CountOf("LocomotionProbe.Enter"), Is.EqualTo(1));
        }

        [Test]
        public void ParentTransition_IsUsedWhenTheLeafHasNone()
        {
            var machine = BuildEventBranches();

            // Locomotion says nothing about Beta; Grounded does.
            Context.Beta.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<AirborneProbe>(), Is.True);
        }

        [Test]
        public void LeafTransition_BeatsTheParentOnTheSameSource()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<AirborneProbe, FallingProbe>()
                .Use<LeafOverParentModule>()
                .Build());

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<AttackProbe>(), Is.True);
            Assert.That(machine.IsIn<GroundedProbe>(), Is.True);
        }

        [Test]
        public void GlobalTransition_BeatsBothLeafAndParent()
        {
            var machine = Track(Attach()
                .Start<GroundedProbe>()
                .Child<GroundedProbe, LocomotionProbe>()
                .Child<GroundedProbe, AttackProbe>()
                .Child<AirborneProbe, FallingProbe>()
                .Use<LeafOverParentModule>()
                .Use<GlobalAlphaModule>()
                .Build());

            Context.Alpha.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<DeadProbe>(), Is.True);
        }

        [Test]
        public void OnlyOneTransitionRunsPerUpdate()
        {
            var machine = BuildEventBranches();
            Context.Log.Clear();

            Context.Alpha.Publish();
            Context.Beta.Publish();
            machine.Tick(0.1f);

            Assert.That(machine.IsIn<AttackProbe>(), Is.True);

            machine.Tick(0.1f);
            Assert.That(machine.IsIn<AirborneProbe>(), Is.True);
        }
    }
}
