using System;
using System.Text.RegularExpressions;
using Fynite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FyniteTests
{
    /// <summary>
    /// Building a machine while the loop is tearing itself down, or while a reset runs straight
    /// through the build, is refused. A machine only ever exists fully built or not at all.
    /// </summary>
    public sealed class LoopBuildGuardTests : MachineFixture
    {
        private static readonly Regex ShuttingDown = new Regex("shutting down");
        private static readonly Regex Boom = new Regex("fynite-test-boom");

        [SetUp]
        public void ResetProbes() => GuardProbe.Reset();

        [Test]
        public void Build_DuringShutdownIsRejectedBeforeAnythingIsCreated()
        {
            var machine = Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());

            var otherContext = new ProbeContext();
            var otherOwner = NewOwner();
            Exception refusal = null;

            Context.OnExit = () =>
            {
                try
                {
                    Attach(otherOwner, otherContext)
                        .Start<GuardProbe>()
                        .Use<GuardModule>()
                        .Build();
                }
                catch (Exception exception)
                {
                    refusal = exception;
                }
            };

            FyniteLoop.Clear();

            Assert.That(refusal, Is.TypeOf<InvalidOperationException>());
            Assert.That(refusal.Message, Does.Contain("shutting down"));

            Assert.That(GuardModule.Configured, Is.Zero, "a transition module was configured");
            Assert.That(GuardProbe.Selectors, Is.Zero, "an event selector ran");
            Assert.That(GuardProbe.Activities, Is.Zero, "ConfigureActivity ran");
            Assert.That(GuardProbe.Enters, Is.Zero, "Enter ran");
            Assert.That(otherContext.Alpha.SubscriberCount, Is.Zero, "a subscription was created");
            Assert.That(otherContext.Log, Is.Empty);

            Assert.That(machine.IsRunning, Is.False);
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void Build_ThatEscapesFromExitIsReportedAndTheResetFinishes()
        {
            var failingContext = new ProbeContext();
            var failingOwner = NewOwner();
            var failing = Track(Attach(failingOwner, failingContext)
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            var healthyContext = new ProbeContext();
            var healthy = Track(Attach(NewOwner(), healthyContext)
                .Start<IdleProbe>()
                .Use<AlphaModule>()
                .Build());

            // Letting the refusal escape Exit must not strand the rest of the reset.
            failingContext.OnExit = () => Attach(NewOwner(), new ProbeContext())
                .Start<IdleProbe>()
                .Build();

            healthyContext.Log.Clear();

            LogAssert.Expect(LogType.Exception, ShuttingDown);
            FyniteLoop.Clear();

            Assert.That(failing.IsRunning, Is.False);
            Assert.That(healthy.IsRunning, Is.False);
            Assert.That(healthyContext.Trace, Is.EqualTo("IdleProbe.Exit"));
            Assert.That(failingContext.Alpha.SubscriberCount, Is.Zero);
            Assert.That(healthyContext.Alpha.SubscriberCount, Is.Zero);
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void Build_IsAllowedAgainOnceTheResetIsOver()
        {
            Track(Attach().Start<IdleProbe>().Use<AlphaModule>().Build());

            FyniteLoop.Clear();

            var freshContext = new ProbeContext();
            var fresh = Track(Attach(NewOwner(), freshContext)
                .Start<IdleProbe>()
                .Use<LocomotionModule>()
                .Build());

            Assert.That(FyniteLoop.Count, Is.EqualTo(1));

            freshContext.Log.Clear();
            FyniteLoop.Tick(0.1f, false);

            Assert.That(freshContext.Trace, Is.EqualTo("IdleProbe.Update"));
            Assert.That(fresh.IsRunning, Is.True);
        }

        [Test]
        public void Build_IsRejectedAfterAResetWasAskedForDuringATick()
        {
            BuildLocomotion();

            Exception refusal = null;

            Context.OnUpdate = () =>
            {
                Context.OnUpdate = null;
                FyniteLoop.Clear();

                try
                {
                    Attach(NewOwner(), new ProbeContext()).Start<IdleProbe>().Build();
                }
                catch (Exception exception)
                {
                    refusal = exception;
                }
            };

            FyniteLoop.Tick(0.1f, false);

            Assert.That(refusal, Is.TypeOf<InvalidOperationException>());
            Assert.That(refusal.Message, Does.Contain("shutting down"));
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void ResetDuringModuleConfigure_InvalidatesTheBuild()
        {
            var failure = Assert.Throws<InvalidOperationException>(
                () => Attach().Start<GuardProbe>().Use<ResettingModule>().Build());

            Assert.That(failure.Message, Does.Contain("reset"));
            AssertNothingSurvived();
            Assert.That(GuardProbe.Activities, Is.Zero, "ConfigureActivity ran after the reset");
            Assert.That(GuardProbe.Enters, Is.Zero, "Enter ran after the reset");
        }

        [Test]
        public void ResetDuringAnEventSelector_InvalidatesTheBuild()
        {
            var failure = Assert.Throws<InvalidOperationException>(
                () => Attach().Start<GuardProbe>().Use<ResettingSelectorModule>().Build());

            Assert.That(failure.Message, Does.Contain("reset"));
            AssertNothingSurvived();
            Assert.That(GuardProbe.Enters, Is.Zero, "Enter ran after the reset");
        }

        [Test]
        public void ResetDuringConfigureActivity_InvalidatesTheBuild()
        {
            GuardProbe.ResetOnConfigureActivity = true;

            var failure = Assert.Throws<InvalidOperationException>(
                () => Attach().Start<GuardProbe>().Use<GuardModule>().Build());

            Assert.That(failure.Message, Does.Contain("reset"));
            Assert.That(GuardProbe.Activities, Is.EqualTo(1));
            Assert.That(GuardProbe.Enters, Is.Zero, "Enter ran after the reset");
            AssertNothingSurvived();
        }

        [Test]
        public void ResetDuringTheInitialEnter_InvalidatesTheBuildAndUndoesIt()
        {
            GuardProbe.ResetOnEnter = true;

            var failure = Assert.Throws<InvalidOperationException>(
                () => Attach().Start<GuardProbe>().Use<GuardModule>().Build());

            Assert.That(failure.Message, Does.Contain("reset"));
            Assert.That(GuardProbe.Enters, Is.EqualTo(1));
            Assert.That(GuardProbe.Exits, Is.EqualTo(1), "the entered state was not left again");
            AssertNothingSurvived();
        }

        [Test]
        public void FailingInitialEnter_ExitsEveryStateThatEntered()
        {
            Context.ThrowOn = "LeftProbe.Enter";

            var failure = Assert.Throws<InvalidOperationException>(
                () => Attach()
                    .Start<ShellProbe>()
                    .Child<ShellProbe, LeftProbe>()
                    .Use<ShellModule>()
                    .Build());

            Assert.That(failure.Message, Does.Contain("fynite-test-boom"));

            // Both entered, so both are left again — leaf first, and the failing one included.
            Assert.That(
                Context.Trace,
                Is.EqualTo("ShellProbe.Enter,LeftProbe.Enter,LeftProbe.Exit,ShellProbe.Exit"));

            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.That(Context.Gamma.SubscriberCount, Is.Zero);
            Assert.That(FyniteLoop.Count, Is.Zero);
        }

        [Test]
        public void FailingInitialEnter_KeepsTheOriginalExceptionWhenExitAlsoFails()
        {
            Context.ThrowOn = "LeftProbe.Enter";
            Context.OnExit = () => throw new InvalidOperationException("fynite-test-secondary");

            // Both states fail on the way out: one is reported by the cleanup, the other by the abort.
            LogAssert.Expect(LogType.Exception, new Regex("fynite-test-secondary"));
            LogAssert.Expect(LogType.Exception, new Regex("fynite-test-secondary"));

            var failure = Assert.Throws<InvalidOperationException>(
                () => Attach()
                    .Start<ShellProbe>()
                    .Child<ShellProbe, LeftProbe>()
                    .Use<ShellModule>()
                    .Build());

            Assert.That(
                failure.Message,
                Does.Contain("fynite-test-boom"),
                "a cleanup failure replaced the original one");
            Assert.That(FyniteLoop.Count, Is.Zero);
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
        }

        [Test]
        public void FailingInitialEnter_LeavesNothingRegisteredOrSubscribed()
        {
            Context.ThrowOn = "WaitForProbe.Enter";

            Assert.Throws<InvalidOperationException>(
                () => Attach().Start<WaitForProbe>().Use<AlphaModule>().Build());

            Assert.That(FyniteLoop.Count, Is.Zero);
            Assert.That(Context.Alpha.SubscriberCount, Is.Zero);
            Assert.DoesNotThrow(() => Context.Alpha.Publish());
        }

        private static void AssertNothingSurvived()
        {
            Assert.That(FyniteLoop.Count, Is.Zero, "the invalidated build registered anyway");
            Assert.That(GuardProbe.Sources.Alpha.SubscriberCount, Is.Zero, "a subscription survived");
        }

        /// <summary>Counts every step of a build, so a refused one can be shown to have done none.</summary>
        private sealed class GuardProbe : FyniteState<ProbeContext>
        {
            internal static readonly ProbeContext Sources = new ProbeContext();

            internal static int Selectors;
            internal static int Activities;
            internal static int Enters;
            internal static int Exits;
            internal static bool ResetOnConfigureActivity;
            internal static bool ResetOnEnter;

            internal static void Reset()
            {
                Selectors = 0;
                Activities = 0;
                Enters = 0;
                Exits = 0;
                ResetOnConfigureActivity = false;
                ResetOnEnter = false;
                GuardModule.Configured = 0;
            }

            internal static FyniteEvent Select()
            {
                Selectors++;
                return Sources.Alpha;
            }

            protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            {
                Activities++;

                if (ResetOnConfigureActivity)
                {
                    FyniteLoop.Clear();
                }

                activity.Do(context => context.Mark("guard"));
            }

            protected override void Enter()
            {
                Enters++;

                if (ResetOnEnter)
                {
                    FyniteLoop.Clear();
                }
            }

            protected override void Exit() => Exits++;
        }

        private sealed class GuardModule : IFyniteTransitions<ProbeContext>
        {
            internal static int Configured;

            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                Configured++;
                transitions.From<GuardProbe, IdleProbe>().On(context => GuardProbe.Select());
            }
        }

        private sealed class ResettingModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
            {
                transitions.From<GuardProbe, IdleProbe>().When<Never>();
                FyniteLoop.Clear();
            }
        }

        private sealed class ResettingSelectorModule : IFyniteTransitions<ProbeContext>
        {
            public void Configure(FyniteTransitions<ProbeContext> transitions)
                => transitions
                    .From<GuardProbe, IdleProbe>()
                    .On(context =>
                    {
                        FyniteLoop.Clear();
                        return GuardProbe.Select();
                    });
        }
    }
}
