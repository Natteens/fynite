using Fynite;

namespace FyniteTests
{
    /// <summary>Idle to Walk, on Alpha.</summary>
    public sealed class AlphaModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
            => transitions
                .From<IdleProbe>()
                .To<WalkProbe>()
                .On(context => context.Alpha);
    }

    /// <summary>Two transitions on the same source, so one subscription is expected.</summary>
    public sealed class AlphaTwiceModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<IdleProbe>().To<WalkProbe>().On(context => context.Alpha);
            transitions.From<WalkProbe>().To<RunProbe>().On(context => context.Alpha);
        }
    }

    /// <summary>Idle to Walk on Alpha, Walk to Run on Beta: two distinct sources.</summary>
    public sealed class AlphaThenBetaModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<IdleProbe>().To<WalkProbe>().On(context => context.Alpha);
            transitions.From<WalkProbe>().To<RunProbe>().On(context => context.Beta);
        }
    }

    /// <summary>Beta is declared first, so publication order is what decides between the two.</summary>
    public sealed class BetaThenAlphaFromIdleModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<IdleProbe>().To<RunProbe>().On(context => context.Beta);
            transitions.From<IdleProbe>().To<WalkProbe>().On(context => context.Alpha);
        }
    }

    /// <summary>Two transitions out of Idle on the same source; the first declared must win.</summary>
    public sealed class AlphaOrderModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<IdleProbe>().To<WalkProbe>().On(context => context.Alpha);
            transitions.From<IdleProbe>().To<RunProbe>().On(context => context.Alpha);
        }
    }

    /// <summary>Any to Dead, on Alpha.</summary>
    public sealed class GlobalAlphaModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
            => transitions
                .Any()
                .To<DeadProbe>()
                .On(context => context.Alpha);
    }

    /// <summary>Counts how often its selector runs.</summary>
    public sealed class CountedAlphaModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
            => transitions
                .From<IdleProbe>()
                .To<WalkProbe>()
                .On(context => context.Counted);
    }

    /// <summary>Subscribes to Alpha and only then asks for a source that does not exist.</summary>
    public sealed class AlphaThenMissingModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<IdleProbe>().To<WalkProbe>().On(context => context.Alpha);
            transitions.From<WalkProbe>().To<RunProbe>().On(context => context.Missing);
        }
    }

    public sealed class NullSelectorModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
            => transitions.From<IdleProbe>().To<WalkProbe>().On(null);
    }

    /// <summary>An event out of Idle next to a predicate out of Idle, to compare the two.</summary>
    public sealed class EventBeforePredicateModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<IdleProbe>().To<WalkProbe>().On(context => context.Alpha);
            transitions.From<IdleProbe>().To<RunProbe>().When<AskedPredicate>();
        }
    }

    /// <summary>A predicate that publishes while it runs, and the event it publishes.</summary>
    public sealed class PublishingPredicateModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<IdleProbe>().To<RunProbe>().When<PublishingPredicate>();
            transitions.From<IdleProbe>().To<WalkProbe>().On(context => context.Alpha);
        }
    }

    /// <summary>
    /// Alpha only leaves Walk, so publishing it from Idle reaches a subscribed machine that has
    /// nothing to do with it. The counting predicate is what says whether the cycle carried on.
    /// </summary>
    public sealed class UnmatchedEventModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<WalkProbe>().To<RunProbe>().On(context => context.Alpha);
            transitions.From<IdleProbe>().To<DeadProbe>().When<AskedPredicate>();
        }
    }

    /// <summary>Reaches Walk by predicate, and only there starts listening for Alpha.</summary>
    public sealed class WalkOnlyAlphaModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<IdleProbe>().To<WalkProbe>().When<ToWalk>();
            transitions.From<WalkProbe>().To<RunProbe>().On(context => context.Alpha);
        }
    }

    public sealed class SelfAlphaModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
            => transitions.From<IdleProbe>().To<IdleProbe>().On(context => context.Alpha);
    }

    /// <summary>
    /// Grounded &gt; Locomotion (initial), Attack. Airborne &gt; Falling. Every route is an event.
    /// </summary>
    public sealed class BranchEventModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<LocomotionProbe>().To<AttackProbe>().On(context => context.Alpha);
            transitions.From<AttackProbe>().To<GroundedProbe>().On(context => context.Gamma);
            transitions.From<GroundedProbe>().To<AirborneProbe>().On(context => context.Beta);
            transitions.From<AirborneProbe>().To<GroundedProbe>().On(context => context.Gamma);
        }
    }

    /// <summary>
    /// The same source leaves the leaf and the parent, so the leaf has to be the one that answers.
    /// </summary>
    public sealed class LeafOverParentModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<GroundedProbe>().To<AirborneProbe>().On(context => context.Alpha);
            transitions.From<LocomotionProbe>().To<AttackProbe>().On(context => context.Alpha);
        }
    }
}
