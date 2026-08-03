using Fynite;

namespace FyniteSamples.CodeFirst
{
    public sealed class AirTransitions : IFyniteTransitions<ExampleContext>
    {
        public void Configure(FyniteTransitions<ExampleContext> transitions)
        {
            transitions
                .From<GroundedState>()
                .To<AirborneState>()
                .When<IsAirborne>();

            transitions
                .From<AirborneState>()
                .To<GroundedState>()
                .When<IsGrounded>();
        }
    }
}
