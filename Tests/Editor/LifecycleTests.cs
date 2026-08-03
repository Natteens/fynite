using Fynite;
using NUnit.Framework;

namespace FyniteTests
{
    public sealed class LifecycleTests : MachineFixture
    {
        [Test]
        public void Build_EntersInitialState()
        {
            var machine = BuildLocomotion();

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Enter"));
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
            Assert.That(machine.CurrentStateType, Is.EqualTo(typeof(IdleProbe)));
            Assert.That(machine.IsRunning, Is.True);
        }

        [Test]
        public void Update_RunsActiveState()
        {
            var machine = BuildLocomotion();
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Update"));
        }

        [Test]
        public void FixedUpdate_RunsActiveState()
        {
            var machine = BuildLocomotion();
            Context.Log.Clear();

            machine.TickFixed(0.02f);

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.FixedUpdate"));
        }

        [Test]
        public void FixedUpdate_DoesNotEvaluateTransitions()
        {
            var machine = BuildLocomotion();
            Context.Log.Clear();
            Context.ToWalk = true;

            machine.TickFixed(0.02f);

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.FixedUpdate"));
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }

        [Test]
        public void Transition_RunsExitThenEnterThenUpdateOfTarget()
        {
            var machine = BuildLocomotion();
            Context.Log.Clear();
            Context.ToWalk = true;

            machine.Tick(0.1f);

            Assert.That(
                Context.Trace,
                Is.EqualTo("IdleProbe.Exit,WalkProbe.Enter,WalkProbe.Update"));
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void Dispose_RunsExitOnce()
        {
            var machine = BuildLocomotion();
            Context.Log.Clear();

            machine.Dispose();

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Exit"));
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var machine = BuildLocomotion();
            Context.Log.Clear();

            machine.Dispose();
            machine.Dispose();
            machine.Dispose();

            Assert.That(Context.CountOf("IdleProbe.Exit"), Is.EqualTo(1));
        }

        [Test]
        public void Dispose_StopsFurtherCallbacks()
        {
            var machine = BuildLocomotion();
            machine.Dispose();
            Context.Log.Clear();

            machine.Tick(0.1f);
            machine.TickFixed(0.02f);

            Assert.That(Context.Log, Is.Empty);
            Assert.That(machine.CurrentStateType, Is.Null);
            Assert.That(machine.IsIn<IdleProbe>(), Is.False);
        }

        [Test]
        public void Update_WithoutTransition_KeepsState()
        {
            var machine = BuildLocomotion();
            Context.Log.Clear();

            machine.Tick(0.1f);
            machine.Tick(0.1f);

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Update,IdleProbe.Update"));
            Assert.That(machine.IsIn<IdleProbe>(), Is.True);
        }
    }
}
