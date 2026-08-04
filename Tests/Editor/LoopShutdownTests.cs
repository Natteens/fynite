using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.TestTools;

namespace FyniteTests
{
    /// <summary>
    /// Resetting the loop is not just forgetting the machines: every one of them has to end properly,
    /// or a subsystem reset leaves states half-entered and event sources holding dead subscribers.
    /// </summary>
    public sealed class LoopShutdownTests : MachineFixture
    {
        private static readonly Regex Boom = new Regex("fynite-test-boom");

        private PlayerLoopSystem original;

        [SetUp]
        public void CapturePlayerLoop() => original = PlayerLoop.GetCurrentPlayerLoop();

        [TearDown]
        public void RestorePlayerLoop() => PlayerLoop.SetPlayerLoop(original);

        [Test]
        public void Clear_EndsARunningMachine()
        {
            var machine = BuildLocomotion();
            Context.Log.Clear();

            FyniteLoop.Clear();

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Exit"));
            Assert.That(machine.IsRunning, Is.False);
            Assert.That(machine.CurrentStateType, Is.Null);
            Assert.That(machine.IsIn<IdleProbe>(), Is.False);
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void Clear_RunsExitExactlyOnce()
        {
            BuildLocomotion();
            Context.Log.Clear();

            FyniteLoop.Clear();
            FyniteLoop.Clear();
            FyniteLoop.Clear();

            Assert.That(Context.CountOf("IdleProbe.Exit"), Is.EqualTo(1));
        }

        [Test]
        public void Clear_OnAnEmptyLoopDoesNothing()
        {
            Assert.DoesNotThrow(() => FyniteLoop.Clear());
            Assert.DoesNotThrow(() => FyniteLoop.Clear());
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void Dispose_AfterClearIsSafeAndQuiet()
        {
            var machine = BuildLocomotion();
            FyniteLoop.Clear();
            Context.Log.Clear();

            Assert.DoesNotThrow(() => machine.Dispose());
            Assert.DoesNotThrow(() => machine.Dispose());

            Assert.That(Context.Log, Is.Empty, "Dispose ran Exit a second time");
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void Clear_EndsEveryMachineInTheLoop()
        {
            var first = BuildLocomotion();

            var secondContext = new ProbeContext();
            var second = Track(Attach(NewOwner(), secondContext)
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            Context.Log.Clear();
            secondContext.Log.Clear();

            FyniteLoop.Clear();

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Exit"));
            Assert.That(secondContext.Trace, Is.EqualTo("IdleProbe.Exit"));
            Assert.That(first.IsRunning, Is.False);
            Assert.That(second.IsRunning, Is.False);
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void Clear_ExitsLeafBeforeParent()
        {
            var machine = BuildShell();
            machine.Tick(0.1f);
            Context.Log.Clear();

            FyniteLoop.Clear();

            Assert.That(Context.Trace, Is.EqualTo("LeftProbe.Exit,ShellProbe.Exit"));
            Assert.That(machine.IsIn<ShellProbe>(), Is.False);
            Assert.That(machine.IsIn<LeftProbe>(), Is.False);
            Assert.That(machine.CurrentStateType, Is.Null);
        }

        [Test]
        public void Clear_CancelsTheActivitiesOfTheWholePath()
        {
            var machine = BuildShell();
            machine.Tick(0.1f);

            // Left waits on Alpha, Shell waits on Gamma.
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));
            Assert.That(Context.Gamma.SubscriberCount, Is.EqualTo(1));

            FyniteLoop.Clear();

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(Context.Gamma.SubscriberCount, Is.Zero);
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void Clear_StopsATimedActivity()
        {
            var machine = Track(Attach().Start<WaitProbe>().Build());

            machine.Tick(0.2f);
            Assert.That(Context.CountOf("Begin"), Is.EqualTo(1));

            FyniteLoop.Clear();
            Context.Log.Clear();

            machine.Tick(0.2f);
            machine.Tick(0.2f);
            machine.Tick(0.2f);

            Assert.That(Context.Log, Is.Empty, "the activity kept going after the reset");
        }

        [Test]
        public void Clear_ReleasesAFinishedActivityToo()
        {
            var machine = Track(Attach().Start<ImmediateProbe>().Build());

            machine.Tick(0.1f);
            Assert.That(Context.CountOf("End"), Is.EqualTo(1));
            Context.Log.Clear();

            FyniteLoop.Clear();

            Assert.That(Context.Trace, Is.EqualTo("ImmediateProbe.Exit"));
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void Clear_RemovesEventTransitionSubscriptions()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            FyniteLoop.Clear();
            Context.Log.Clear();

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.DoesNotThrow(() => Context.Alpha.Publish());

            machine.Tick(0.1f);

            Assert.That(Context.Log, Is.Empty);
            Assert.That(machine.IsIn<WalkProbe>(), Is.False);
        }

        [Test]
        public void Clear_RemovesActivityWaitForSubscriptions()
        {
            var machine = Track(Attach().Start<WaitForProbe>().Build());

            machine.Tick(0.1f);
            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(1));

            FyniteLoop.Clear();

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.DoesNotThrow(() => Context.Alpha.Publish());
            Assert.That(Context.CountOf("End"), Is.Zero);
        }

        [Test]
        public void Clear_DropsEventsThatWereStillWaiting()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            Context.Alpha.Publish();

            FyniteLoop.Clear();
            Context.Log.Clear();

            machine.Tick(0.1f);

            Assert.That(Context.Log, Is.Empty, "a queued event survived the reset");
            Assert.That(machine.IsRunning, Is.False);
        }

        [Test]
        public void Clear_RemovesEveryMachineListeningToTheSameSource()
        {
            Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());
            Track(Attach(NewOwner(), Context).Start<IdleProbe>().Use<AlphaModule>().Build());

            Assert.That(Context.Alpha.SubscriberCount, Is.EqualTo(2));

            FyniteLoop.Clear();

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void ClearDuringTick_LetsTheCurrentCallbackFinish()
        {
            BuildLocomotion();
            Context.Log.Clear();

            Context.OnUpdate = () =>
            {
                FyniteLoop.Clear();
                Context.Mark("after-clear");
            };

            FyniteLoop.Tick(0.1f, false);

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Update,after-clear,IdleProbe.Exit"));
        }

        [Test]
        public void ClearDuringTick_SkipsTheMachinesAfterIt()
        {
            BuildLocomotion();

            var lateContext = new ProbeContext();
            Track(Attach(NewOwner(), lateContext).Start<IdleProbe>().Build());

            Context.OnUpdate = () => FyniteLoop.Clear();
            lateContext.Log.Clear();

            FyniteLoop.Tick(0.1f, false);

            Assert.That(
                lateContext.Trace,
                Is.EqualTo("IdleProbe.Exit"),
                "a machine after the request still received Update");
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void ClearDuringTick_ResetsInTheFinally()
        {
            var machine = BuildLocomotion();
            Context.OnUpdate = () => FyniteLoop.Clear();

            FyniteLoop.Tick(0.1f, false);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(FyniteLoop.Count, Is.Zero);

            // The loop is usable again, and the reset did not leave the ticking flag stuck.
            Context.OnUpdate = null;
            var freshContext = new ProbeContext();
            var fresh = Track(Attach(NewOwner(), freshContext).Start<IdleProbe>().Build());
            freshContext.Log.Clear();

            FyniteLoop.Tick(0.1f, false);

            Assert.That(freshContext.Trace, Is.EqualTo("IdleProbe.Update"));
            Assert.That(fresh.IsRunning, Is.True);
        }

        [Test]
        public void ClearDuringTick_RefusesAMachineBuiltAfterTheRequest()
        {
            BuildLocomotion();

            var lateContext = new ProbeContext();
            var lateOwner = NewOwner();
            Exception refusal = null;

            Context.OnUpdate = () =>
            {
                Context.OnUpdate = null;
                FyniteLoop.Clear();

                try
                {
                    Attach(lateOwner, lateContext).Start<IdleProbe>().Build();
                }
                catch (Exception exception)
                {
                    refusal = exception;
                }
            };

            FyniteLoop.Tick(0.1f, false);

            // Once a reset is asked for there is no loop left to join, so the build is refused rather
            // than half-created and immediately torn down again.
            Assert.That(refusal, Is.TypeOf<InvalidOperationException>());
            Assert.That(lateContext.Log, Is.Empty, "a refused build still entered a state");
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void ClearDuringTick_DoesNotFlushPendingRegistrations()
        {
            BuildLocomotion();

            var lateContext = new ProbeContext();
            var lateOwner = NewOwner();

            Context.OnUpdate = () =>
            {
                Context.OnUpdate = null;
                Track(Attach(lateOwner, lateContext).Start<IdleProbe>().Build());
                FyniteLoop.Clear();
            };

            FyniteLoop.Tick(0.1f, false);

            Assert.That(FyniteLoop.Count, Is.Zero, "a pending registration was flushed into the loop");

            lateContext.Log.Clear();
            FyniteLoop.Tick(0.1f, false);

            Assert.That(lateContext.Log, Is.Empty, "the pending machine came back in a later cycle");
        }

        [Test]
        public void PendingMachine_NeverReceivesUpdate()
        {
            BuildLocomotion();

            var lateContext = new ProbeContext();
            var lateOwner = NewOwner();

            Context.OnUpdate = () =>
            {
                Context.OnUpdate = null;
                Track(Attach(lateOwner, lateContext).Start<IdleProbe>().Build());
                FyniteLoop.Clear();
            };

            FyniteLoop.Tick(0.1f, false);
            FyniteLoop.Tick(0.1f, false);

            Assert.That(lateContext.CountOf("IdleProbe.Update"), Is.Zero);
            Assert.That(lateContext.CountOf("IdleProbe.Exit"), Is.EqualTo(1));
        }

        [Test]
        public void Clear_CleansTheOtherMachinesWhenOneExitThrows()
        {
            var faultingContext = new ProbeContext();
            var faulting = Track(Attach(NewOwner(), faultingContext)
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            var healthyContext = new ProbeContext();
            var healthy = Track(Attach(NewOwner(), healthyContext)
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            faultingContext.ThrowOn = "IdleProbe.Exit";
            healthyContext.Log.Clear();

            LogAssert.Expect(LogType.Exception, Boom);
            FyniteLoop.Clear();

            Assert.That(healthyContext.Trace, Is.EqualTo("IdleProbe.Exit"));
            Assert.That(faulting.IsRunning, Is.False);
            Assert.That(healthy.IsRunning, Is.False);
            Assert.That(faultingContext.Alpha.SubscriberCount, Is.Zero, "the failing machine leaked");
            Assert.That(healthyContext.Alpha.SubscriberCount, Is.Zero);
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void Clear_HandlesAMachineThatAlreadyFaulted()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            Context.OnUpdate = () => throw new InvalidOperationException("fynite-test-boom");

            LogAssert.Expect(LogType.Exception, Boom);
            machine.Tick(0.1f);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);

            Assert.DoesNotThrow(() => FyniteLoop.Clear());
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void Clear_HandlesAMachineThatWasAlreadyDisposed()
        {
            var machine = BuildLocomotion();
            machine.Dispose();
            Context.Log.Clear();

            Assert.DoesNotThrow(() => FyniteLoop.Clear());

            Assert.That(Context.Log, Is.Empty, "Clear ran Exit on a disposed machine");
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        /// <summary>
        /// A reset is bounded by what is registered, not by a number: many more machines than any
        /// plausible sweep limit all end in the one pass.
        /// </summary>
        [Test]
        public void Clear_EndsEveryMachineHoweverManyThereAre()
        {
            const int count = 50;

            var contexts = new List<ProbeContext>();
            var built = new List<FyniteMachine<ProbeContext>>();

            for (var i = 0; i < count; i++)
            {
                var context = new ProbeContext();
                contexts.Add(context);
                built.Add(Track(Attach(NewOwner(), context)
                    .Start<IdleProbe>()
                    .Use<AlphaModule>()
                    .Build()));
            }

            Assert.That(FyniteLoop.Count, Is.EqualTo(count));

            FyniteLoop.Clear();

            Assert.That(FyniteLoop.Count, Is.Zero);

            for (var i = 0; i < count; i++)
            {
                Assert.That(built[i].IsRunning, Is.False, $"machine {i} survived");
                Assert.That(contexts[i].CountOf("IdleProbe.Exit"), Is.EqualTo(1), $"machine {i} exits");
                Assert.That(contexts[i].Alpha.SubscriberCount, Is.Zero, $"machine {i} still listening");
            }
        }

        [Test]
        public void Bootstrap_EndsTheMachinesThatWereStillRunning()
        {
            var machine = Track(Attach()
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            Context.Log.Clear();

            FyniteLoopBootstrapper.Bootstrap();

            Assert.That(Context.Trace, Is.EqualTo("IdleProbe.Exit"));
            Assert.That(machine.IsRunning, Is.False);
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void Bootstrap_KeepsExactlyOneOfEachSystem()
        {
            FyniteLoopBootstrapper.Bootstrap();
            FyniteLoopBootstrapper.Bootstrap();
            FyniteLoopBootstrapper.Bootstrap();

            Assert.That(
                FynitePlayerLoopUtility.Count(
                    PlayerLoop.GetCurrentPlayerLoop(),
                    typeof(FyniteLoopBootstrapper.FyniteUpdate)),
                Is.EqualTo(1));
            Assert.That(
                FynitePlayerLoopUtility.Count(
                    PlayerLoop.GetCurrentPlayerLoop(),
                    typeof(FyniteLoopBootstrapper.FyniteFixedUpdate)),
                Is.EqualTo(1));
        }

        [Test]
        public void Bootstrap_KeepsTheSystemsWhereTheyBelong()
        {
            FyniteLoopBootstrapper.Bootstrap();
            FyniteLoopBootstrapper.Bootstrap();

            var root = PlayerLoop.GetCurrentPlayerLoop();

            var update = FindPhase(root, typeof(UnityEngine.PlayerLoop.Update));
            var updateAnchor = IndexOf(
                update,
                typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate));

            Assert.That(updateAnchor, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                IndexOf(update, typeof(FyniteLoopBootstrapper.FyniteUpdate)),
                Is.EqualTo(updateAnchor + 1));

            var fixedUpdate = FindPhase(root, typeof(UnityEngine.PlayerLoop.FixedUpdate));
            var fixedAnchor = IndexOf(
                fixedUpdate,
                typeof(UnityEngine.PlayerLoop.FixedUpdate.ScriptRunBehaviourFixedUpdate));

            Assert.That(fixedAnchor, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                IndexOf(fixedUpdate, typeof(FyniteLoopBootstrapper.FyniteFixedUpdate)),
                Is.EqualTo(fixedAnchor + 1));
        }

        [Test]
        public void ANewMachineWorksAfterBootstrap()
        {
            BuildLocomotion();
            FyniteLoopBootstrapper.Bootstrap();

            Assert.That(FyniteLoop.Count, Is.Zero);

            var freshContext = new ProbeContext();
            var fresh = Track(Attach(NewOwner(), freshContext)
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            Assert.That(FyniteLoop.Count, Is.EqualTo(1));

            freshContext.Log.Clear();
            FyniteLoop.Tick(0.1f, false);
            FyniteLoop.Tick(0.02f, true);

            Assert.That(freshContext.Trace, Is.EqualTo("IdleProbe.Update,IdleProbe.FixedUpdate"));

            freshContext.ToWalk = true;
            FyniteLoop.Tick(0.1f, false);
            Assert.That(fresh.IsIn<WalkProbe>(), Is.True);

            fresh.Dispose();
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void SimulatedReload_LeavesTheOldSourcesWithoutSubscribers()
        {
            var oldContext = new ProbeContext();
            Track(Attach(NewOwner(), oldContext)
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            var waiting = Track(Attach(NewOwner(), oldContext)
                .Start<WaitForProbe>()
                .Build());

            waiting.Tick(0.1f);
            oldContext.Beta.Publish();

            Assert.That(oldContext.Alpha.SubscriberCount, Is.EqualTo(2));

            FyniteLoopBootstrapper.Bootstrap();

            Assert.That(oldContext.Alpha.SubscriberCount, Is.Zero);
            Assert.That(oldContext.Beta.SubscriberCount, Is.Zero);
            Assert.That(oldContext.Gamma.SubscriberCount, Is.Zero);

            oldContext.Log.Clear();
            Assert.DoesNotThrow(() => oldContext.Alpha.Publish());
            FyniteLoop.Tick(0.1f, false);

            Assert.That(oldContext.Log, Is.Empty);
        }

        [Test]
        public void SimulatedReload_StartsACleanSessionAfterwards()
        {
            var oldContext = new ProbeContext();
            var stale = Track(Attach(NewOwner(), oldContext)
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            FyniteLoopBootstrapper.Bootstrap();

            var freshContext = new ProbeContext();
            var fresh = Track(Attach(NewOwner(), freshContext)
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            oldContext.Log.Clear();
            freshContext.Alpha.Publish();
            FyniteLoop.Tick(0.1f, false);

            Assert.That(fresh.IsIn<WalkProbe>(), Is.True);
            Assert.That(stale.IsRunning, Is.False);
            Assert.That(oldContext.Log, Is.Empty, "the old session reacted to the new one");
        }

        [Test]
        public void SimulatedReload_LetsTheOldContextBeCollected()
        {
            var weak = BuildAndForget();

            FyniteLoopBootstrapper.Bootstrap();

            // Conservative collectors need more than one pass before a just-dropped graph goes away.
            for (var attempt = 0; attempt < 6 && weak.IsAlive; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Assert.That(weak.IsAlive, Is.False, "the reset kept the old context alive");
        }

        /// <summary>
        /// Builds a machine whose context nothing else refers to, and keeps no strong reference to
        /// either. Separate method so the locals are gone by the time the collector looks.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private WeakReference BuildAndForget()
        {
            var doomed = new ProbeContext();
            var machine = Machine.Attach(NewOwner(), doomed).Start<IdleProbe>().Build();

            machine.Tick(0.1f);

            return new WeakReference(doomed);
        }

        private static PlayerLoopSystem FindPhase(PlayerLoopSystem parent, Type phase)
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

        private static int IndexOf(PlayerLoopSystem parent, Type systemType)
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
