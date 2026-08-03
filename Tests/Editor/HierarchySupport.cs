using Fynite;

namespace FyniteTests
{
    public sealed class GroundedProbe : ProbeState
    {
    }

    public sealed class AirborneProbe : ProbeState
    {
    }

    public sealed class LocomotionProbe : ProbeState
    {
    }

    public sealed class AttackProbe : ProbeState
    {
    }

    public sealed class FallingProbe : ProbeState
    {
    }

    public sealed class TopProbe : ProbeState
    {
    }

    public sealed class MidProbe : ProbeState
    {
    }

    public sealed class LeafProbe : ProbeState
    {
    }

    public sealed class LimboProbe : ProbeState
    {
    }

    public sealed class ToAttack : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context) => context.ToAttack;
    }

    public sealed class ToGrounded : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context) => context.ToGrounded;
    }

    public sealed class ToAirborne : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context) => context.ToAirborne;
    }

    public sealed class ToLocomotion : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context) => context.ToLocomotion;
    }

    public sealed class ToSelf : IPredicate<ProbeContext>
    {
        public bool Evaluate(ProbeContext context) => context.ToSelf;
    }

    /// <summary>
    /// Grounded &gt; Locomotion (initial), Attack. Airborne &gt; Falling.
    /// </summary>
    public sealed class PlayerBranchModule : IFyniteTransitions<ProbeContext>
    {
        public void Configure(FyniteTransitions<ProbeContext> transitions)
        {
            transitions.From<LocomotionProbe>().To<AttackProbe>().When<ToAttack>();
            transitions.From<AttackProbe>().To<GroundedProbe>().When<ToGrounded>();
            transitions.From<GroundedProbe>().To<AirborneProbe>().When<ToAirborne>();
            transitions.From<AirborneProbe>().To<GroundedProbe>().When<ToGrounded>();
        }
    }
}
