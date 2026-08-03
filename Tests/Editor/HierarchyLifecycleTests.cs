using Fynite;
using NUnit.Framework;
using UnityEngine;

namespace FyniteTests
{
    public sealed class HierarchyLifecycleTests : MachineFixture
    {
        [Test]
        public void Build_EntersTheParentBeforeTheChild()
        {
            BuildBranches();

            Assert.That(Context.Trace, Is.EqualTo("GroundedProbe.Enter,LocomotionProbe.Enter"));
        }

        [Test]
        public void Build_EntersAThreeLevelChainTopDown()
        {
            Track(Attach()
                .Start<TopProbe>()
                .Child<TopProbe, MidProbe>()
                .Child<MidProbe, LeafProbe>()
                .Build());

            Assert.That(
                Context.Trace,
                Is.EqualTo("TopProbe.Enter,MidProbe.Enter,LeafProbe.Enter"));
        }

        [Test]
        public void CurrentStateTypeIsTheLeaf()
        {
            var machine = BuildBranches();

            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LocomotionProbe)));
        }

        [Test]
        public void IsInIsTrueForTheParentAndTheLeaf()
        {
            var machine = BuildBranches();

            Assert.That(machine.IsIn<GroundedProbe>(), Is.True);
            Assert.That(machine.IsIn<LocomotionProbe>(), Is.True);
        }

        [Test]
        public void IsInIsFalseForAnInactiveState()
        {
            var machine = BuildBranches();

            Assert.That(machine.IsIn<AttackProbe>(), Is.False);
            Assert.That(machine.IsIn<AirborneProbe>(), Is.False);
            Assert.That(machine.IsIn<FallingProbe>(), Is.False);
        }

        [Test]
        public void IsInIsTrueForEveryLevelOfAThreeLevelChain()
        {
            var machine = Track(Attach()
                .Start<TopProbe>()
                .Child<TopProbe, MidProbe>()
                .Child<MidProbe, LeafProbe>()
                .Build());

            Assert.That(machine.IsIn<TopProbe>(), Is.True);
            Assert.That(machine.IsIn<MidProbe>(), Is.True);
            Assert.That(machine.IsIn<LeafProbe>(), Is.True);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(LeafProbe)));
        }

        [Test]
        public void Update_RunsFromTheTopDownToTheLeaf()
        {
            var machine = BuildBranches();
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(Context.Trace, Is.EqualTo("GroundedProbe.Update,LocomotionProbe.Update"));
        }

        [Test]
        public void FixedUpdate_RunsFromTheTopDownToTheLeaf()
        {
            var machine = BuildBranches();
            Context.Log.Clear();

            machine.TickFixed(0.02f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("GroundedProbe.FixedUpdate,LocomotionProbe.FixedUpdate"));
        }

        [Test]
        public void FlatMachineStillRunsExactlyOnce()
        {
            var machine = BuildLocomotion();
            Context.Log.Clear();

            machine.Tick(0.1f);
            machine.TickFixed(0.02f);

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Update,IdleProbe.FixedUpdate"));
        }

        [Test]
        public void InactiveStatesReceiveNoCallback()
        {
            var machine = BuildBranches();
            Context.Log.Clear();

            machine.Tick(0.1f);
            machine.TickFixed(0.02f);

            Assert.That(Context.CountOf("AttackProbe.Update"), Is.Zero);
            Assert.That(Context.CountOf("AirborneProbe.Update"), Is.Zero);
            Assert.That(Context.CountOf("FallingProbe.FixedUpdate"), Is.Zero);
        }

        [Test]
        public void Dispose_ExitsFromTheLeafUpToTheTop()
        {
            var machine = BuildBranches();
            Context.Log.Clear();

            machine.Dispose();

            Assert.That(Context.Trace, Is.EqualTo("LocomotionProbe.Exit,GroundedProbe.Exit"));
            Assert.That(machine.IsRunning, Is.False);
            Assert.That(machine.CurrentStateType, Is.Null);
        }

        [Test]
        public void Dispose_ExitsAThreeLevelChainLeafToTop()
        {
            var machine = Track(Attach()
                .Start<TopProbe>()
                .Child<TopProbe, MidProbe>()
                .Child<MidProbe, LeafProbe>()
                .Build());

            Context.Log.Clear();
            machine.Dispose();

            Assert.That(Context.Trace, Is.EqualTo("LeafProbe.Exit,MidProbe.Exit,TopProbe.Exit"));
        }

        [Test]
        public void Dispose_IsIdempotentOnAHierarchy()
        {
            var machine = BuildBranches();
            Context.Log.Clear();

            machine.Dispose();
            machine.Dispose();
            machine.Dispose();

            Assert.That(Context.CountOf("LocomotionProbe.Exit"), Is.EqualTo(1));
            Assert.That(Context.CountOf("GroundedProbe.Exit"), Is.EqualTo(1));
        }

        [Test]
        public void DestroyedOwner_ExitsFromTheLeafUpToTheTop()
        {
            var machine = BuildBranches();
            Context.Log.Clear();
            Object.DestroyImmediate(Owner.gameObject);

            FyniteLoop.Tick(0.1f, false);

            Assert.That(Context.Trace, Is.EqualTo("LocomotionProbe.Exit,GroundedProbe.Exit"));
            Assert.That(machine.IsRunning, Is.False);
            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void DisabledOwner_RunsNoExit()
        {
            BuildBranches();
            Context.Log.Clear();
            Owner.gameObject.SetActive(false);

            FyniteLoop.Tick(0.1f, false);
            FyniteLoop.Tick(0.02f, true);

            Assert.That(Context.Log, Is.Empty);
            Assert.That(FyniteLoop.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReenabledOwner_KeepsTheSamePath()
        {
            var machine = BuildBranches();
            Context.ToAttack = true;
            FyniteLoop.Tick(0.1f, false);
            Context.ToAttack = false;

            Owner.gameObject.SetActive(false);
            FyniteLoop.Tick(0.1f, false);
            Owner.gameObject.SetActive(true);
            Context.Log.Clear();

            FyniteLoop.Tick(0.1f, false);

            Assert.That(Context.Trace, Is.EqualTo("GroundedProbe.Update,AttackProbe.Update"));
            Assert.That(machine.IsIn<GroundedProbe>(), Is.True);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(AttackProbe)));
        }
    }
}
