using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;

namespace FyniteTests
{
    public sealed class LoopTests : MachineFixture
    {
        private PlayerLoopSystem original;

        [SetUp]
        public void CapturePlayerLoop() => original = PlayerLoop.GetCurrentPlayerLoop();

        [TearDown]
        public void RestorePlayerLoop() => PlayerLoop.SetPlayerLoop(original);

        [Test]
        public void Bootstrap_InstallsUpdateSystemOnce()
        {
            FyniteLoopBootstrapper.Install();

            Assert.That(CountUpdateSystems(), Is.EqualTo(1));
        }

        [Test]
        public void Bootstrap_InstallsFixedUpdateSystemOnce()
        {
            FyniteLoopBootstrapper.Install();

            Assert.That(CountFixedUpdateSystems(), Is.EqualTo(1));
        }

        [Test]
        public void Bootstrap_IsIdempotent()
        {
            FyniteLoopBootstrapper.Install();
            FyniteLoopBootstrapper.Install();
            FyniteLoopBootstrapper.Install();

            Assert.That(CountUpdateSystems(), Is.EqualTo(1));
            Assert.That(CountFixedUpdateSystems(), Is.EqualTo(1));
        }

        [Test]
        public void Uninstall_RemovesBothSystems()
        {
            FyniteLoopBootstrapper.Install();
            FyniteLoopBootstrapper.Uninstall();

            Assert.That(CountUpdateSystems(), Is.EqualTo(0));
            Assert.That(CountFixedUpdateSystems(), Is.EqualTo(0));
        }

        [Test]
        public void UpdateSystemRunsAfterBehaviourUpdate()
        {
            FyniteLoopBootstrapper.Install();

            var root = PlayerLoop.GetCurrentPlayerLoop();
            var phase = FindPhase(root, typeof(UnityEngine.PlayerLoop.Update));
            var anchor = IndexOf(phase, typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate));
            var fynite = IndexOf(phase, typeof(FyniteLoopBootstrapper.FyniteUpdate));

            Assert.That(anchor, Is.GreaterThanOrEqualTo(0));
            Assert.That(fynite, Is.EqualTo(anchor + 1));
        }

        [Test]
        public void FixedUpdateSystemRunsAfterBehaviourFixedUpdate()
        {
            FyniteLoopBootstrapper.Install();

            var root = PlayerLoop.GetCurrentPlayerLoop();
            var phase = FindPhase(root, typeof(UnityEngine.PlayerLoop.FixedUpdate));
            var anchor = IndexOf(
                phase,
                typeof(UnityEngine.PlayerLoop.FixedUpdate.ScriptRunBehaviourFixedUpdate));
            var fynite = IndexOf(phase, typeof(FyniteLoopBootstrapper.FyniteFixedUpdate));

            Assert.That(anchor, Is.GreaterThanOrEqualTo(0));
            Assert.That(fynite, Is.EqualTo(anchor + 1));
        }

        [Test]
        public void BuiltMachineIsRegistered()
        {
            BuildLocomotion();

            Assert.That(FyniteLoop.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisposeUnregistersMachine()
        {
            var machine = BuildLocomotion();

            machine.Dispose();

            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void DestroyedOwnerUnregistersMachineOnNextTick()
        {
            var machine = BuildLocomotion();
            Context.Log.Clear();
            Object.DestroyImmediate(Owner.gameObject);

            FyniteLoop.Tick(0.1f, false);

            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
            Assert.That(machine.IsRunning, Is.False);
            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Exit"));
        }

        [Test]
        public void DisabledOwnerPausesTicks()
        {
            BuildLocomotion();
            Context.Log.Clear();
            Owner.gameObject.SetActive(false);

            FyniteLoop.Tick(0.1f, false);
            FyniteLoop.Tick(0.02f, true);

            Assert.That(Context.Log, Is.Empty);
            Assert.That(FyniteLoop.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReenabledOwnerResumesInTheSameState()
        {
            var machine = BuildLocomotion();
            Context.ToWalk = true;
            FyniteLoop.Tick(0.1f, false);
            Context.ToWalk = false;

            Owner.gameObject.SetActive(false);
            FyniteLoop.Tick(0.1f, false);
            Owner.gameObject.SetActive(true);
            Context.Log.Clear();

            FyniteLoop.Tick(0.1f, false);

            Assert.That(Context.Trace, Is.EqualTo("WalkProbe.Update"));
            Assert.That(machine.IsIn<WalkProbe>(), Is.True);
        }

        [Test]
        public void DisabledBehaviourOwnerPausesTicks()
        {
            BuildLocomotion();
            Context.Log.Clear();
            Owner.enabled = false;

            FyniteLoop.Tick(0.1f, false);

            Assert.That(Context.Log, Is.Empty);
        }

        [Test]
        public void ClearRemovesEveryRegistration()
        {
            BuildLocomotion();
            Track(Attach(NewOwner(), new ProbeContext()).Start<IdleProbe>().Build());

            FyniteLoop.Clear();

            Assert.That(FyniteLoop.Count, Is.EqualTo(0));
        }

        [Test]
        public void DisposingDuringTickDoesNotBreakIteration()
        {
            var first = BuildLocomotion();
            var secondContext = new ProbeContext();
            var second = Track(Attach(NewOwner(), secondContext)
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            Context.OnUpdate = () => first.Dispose();
            Context.Log.Clear();
            secondContext.Log.Clear();

            Assert.DoesNotThrow(() => FyniteLoop.Tick(0.1f, false));

            Assert.That(secondContext.Trace, Is.EqualTo("IdleProbe.Update"));
            Assert.That(FyniteLoop.Count, Is.EqualTo(1));
            Assert.That(second.IsRunning, Is.True);

            Context.OnUpdate = null;
            secondContext.Log.Clear();

            Assert.DoesNotThrow(() => FyniteLoop.Tick(0.1f, false));
            Assert.That(secondContext.Trace, Is.EqualTo("IdleProbe.Update"));
        }

        [Test]
        public void RegisteringDuringTickDefersToNextCycle()
        {
            BuildLocomotion();
            var lateContext = new ProbeContext();
            var lateOwner = NewOwner();

            Context.OnUpdate = () =>
            {
                Context.OnUpdate = null;
                Track(Attach(lateOwner, lateContext).Start<IdleProbe>().Build());
            };

            Assert.DoesNotThrow(() => FyniteLoop.Tick(0.1f, false));

            Assert.That(lateContext.Trace, Is.EqualTo("IdleProbe.Enter"));
            Assert.That(FyniteLoop.Count, Is.EqualTo(2));

            lateContext.Log.Clear();
            FyniteLoop.Tick(0.1f, false);

            Assert.That(lateContext.Trace, Is.EqualTo("IdleProbe.Update"));
        }

        [Test]
        public void NestedTickIsIgnored()
        {
            BuildLocomotion();
            Context.OnUpdate = () => FyniteLoop.Tick(0.1f, false);
            Context.Log.Clear();

            Assert.DoesNotThrow(() => FyniteLoop.Tick(0.1f, false));

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Update"));
        }

        private static int CountUpdateSystems()
            => FynitePlayerLoopUtility.Count(
                PlayerLoop.GetCurrentPlayerLoop(),
                typeof(FyniteLoopBootstrapper.FyniteUpdate));

        private static int CountFixedUpdateSystems()
            => FynitePlayerLoopUtility.Count(
                PlayerLoop.GetCurrentPlayerLoop(),
                typeof(FyniteLoopBootstrapper.FyniteFixedUpdate));

        private static PlayerLoopSystem FindPhase(PlayerLoopSystem parent, System.Type phase)
        {
            var children = parent.subSystemList;
            if (children == null)
            {
                return default;
            }

            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].type == phase)
                {
                    return children[i];
                }

                var nested = FindPhase(children[i], phase);
                if (nested.type != null)
                {
                    return nested;
                }
            }

            return default;
        }

        private static int IndexOf(PlayerLoopSystem parent, System.Type systemType)
        {
            var children = parent.subSystemList;
            if (children == null)
            {
                return -1;
            }

            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].type == systemType)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
