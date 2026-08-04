using Fynite;

namespace FyniteTests
{
    /// <summary>Do, Do — two immediate steps that must chain within one tick.</summary>
    public sealed class ImmediateProbe : ProbeState
    {
        public static int Configured;

        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
        {
            Configured++;

            activity
                .Do(context => context.Mark("Begin"))
                .Do(context => context.Mark("End"));
        }
    }

    /// <summary>Do, Wait(0.5), Do.</summary>
    public sealed class WaitProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Begin"))
                .Wait(0.5f)
                .Do(context => context.Mark("End"));
    }

    /// <summary>Do, Wait(0), Do — the wait must not cost a frame.</summary>
    public sealed class ZeroWaitProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Begin"))
                .Wait(0f)
                .Do(context => context.Mark("End"));
    }

    /// <summary>Do, WaitUntil, Do — the condition also counts how often it was asked.</summary>
    public sealed class UntilProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Begin"))
                .WaitUntil(context =>
                {
                    context.Conditions++;
                    return context.Unlocked;
                })
                .Do(context => context.Mark("End"));
    }

    /// <summary>Do, WaitFor(Alpha), Do.</summary>
    public sealed class WaitForProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Begin"))
                .WaitFor(context => context.Alpha)
                .Do(context => context.Mark("End"));
    }

    /// <summary>Do, WaitFor(Alpha), Do, WaitFor(Alpha), Do — the same source used twice.</summary>
    public sealed class TwiceWaitForProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Begin"))
                .WaitFor(context => context.Alpha)
                .Do(context => context.Mark("Middle"))
                .WaitFor(context => context.Alpha)
                .Do(context => context.Mark("End"));
    }

    /// <summary>Do, Publish(Beta) — whatever listens to Beta must not react inside this tick.</summary>
    public sealed class PublishProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Begin"))
                .Publish(context => context.Beta);
    }

    /// <summary>Resolves its source through the counted property, to prove the selector runs once.</summary>
    public sealed class CountedWaitForProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity.WaitFor(context => context.Counted);
    }

    /// <summary>Do, Wait(0.5), Publish(Beta) — the shape the sample uses.</summary>
    public sealed class TimedPublishProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Begin"))
                .Wait(0.5f)
                .Publish(context => context.Beta);
    }

    public sealed class NullDoProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity.Do(null);
    }

    public sealed class NegativeWaitProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity.Wait(-1f);
    }

    public sealed class NanWaitProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity.Wait(float.NaN);
    }

    public sealed class InfiniteWaitProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity.Wait(float.PositiveInfinity);
    }

    public sealed class NullUntilProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity.WaitUntil(null);
    }

    public sealed class NullWaitForProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity.WaitFor(null);
    }

    public sealed class MissingWaitForProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Begin"))
                .WaitFor(context => context.Missing);
    }

    public sealed class NullPublishProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity.Publish(null);
    }

    public sealed class MissingPublishProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity.Publish(context => context.Missing);
    }

    /// <summary>A parent whose activity has to survive its children coming and going.</summary>
    public sealed class ShellProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Shell.Begin"))
                .WaitFor(context => context.Gamma)
                .Do(context => context.Mark("Shell.End"));
    }

    public sealed class LeftProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity
                .Do(context => context.Mark("Left.Begin"))
                .WaitFor(context => context.Alpha)
                .Do(context => context.Mark("Left.End"));
    }

    public sealed class RightProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity.Do(context => context.Mark("Right.Begin"));
    }

    public sealed class OutsideProbe : ProbeState
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ProbeContext> activity)
            => activity.Do(context => context.Mark("Outside.Begin"));
    }

    /// <summary>Idle to the given activity state on Alpha, and back out on Beta.</summary>
    public sealed class ImmediateModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<IdleProbe>().To<ImmediateProbe>().When<ToWalk>();
            transitions.From<ImmediateProbe>().To<IdleProbe>().When<ToIdle>();
        }
    }

    public sealed class WaitForModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<IdleProbe>().To<WaitForProbe>().When<ToWalk>();
            transitions.From<WaitForProbe>().To<IdleProbe>().When<ToIdle>();
        }
    }

    /// <summary>Beta is what the publishing activity publishes, and what leaves the state.</summary>
    public sealed class PublishModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
            => transitions.From<PublishProbe>().To<IdleProbe>().On(context => context.Beta);
    }

    public sealed class TimedPublishModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
            => transitions.From<TimedPublishProbe>().To<IdleProbe>().On(context => context.Beta);
    }

    public sealed class SelfWaitModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
            => transitions.From<WaitProbe>().To<WaitProbe>().When<ToSelf>();
    }

    /// <summary>
    /// Shell &gt; Left (initial), Right. Outside sits next to Shell. Everything is routed by
    /// predicates so the events stay free for the activities themselves.
    /// </summary>
    public sealed class ShellModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<LeftProbe>().To<RightProbe>().When<ToWalk>();
            transitions.From<RightProbe>().To<LeftProbe>().When<ToIdle>();
            transitions.From<ShellProbe>().To<OutsideProbe>().When<ToAirborne>();
            transitions.From<OutsideProbe>().To<ShellProbe>().When<ToGrounded>();
            transitions.From<RightProbe>().To<ShellProbe>().When<ToLocomotion>();
            transitions.From<ShellProbe>().To<ShellProbe>().When<ToSelf>();
        }
    }

    public static class ActivityReset
    {
        public static void All() => ImmediateProbe.Configured = 0;
    }
}
