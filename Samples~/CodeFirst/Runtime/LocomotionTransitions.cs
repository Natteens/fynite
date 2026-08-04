using Fynite;

namespace FyniteSamples.CodeFirst
{
    public sealed class LocomotionTransitions : IFyniteTransitions<ExampleContext>
    {
        public void Configure(FyniteTransitions<ExampleContext> transitions)
        {
            transitions
                .From<IdleState, WalkState>()
                .When<HasMovement>();

            transitions
                .From<WalkState, IdleState>()
                .When<HasNoMovement>();
        }
    }
}
