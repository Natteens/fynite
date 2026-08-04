using Fynite;

namespace FyniteSamples.CodeFirst
{
    /// <summary>
    /// Leaving the ground and reaching it again are occurrences, so these two are events. Because
    /// they start at <c>Grounded</c>, they cover its children without naming them.
    /// </summary>
    public sealed class AirTransitions : IFyniteTransitions<ExampleContext>
    {
        public void Configure(FyniteTransitions<ExampleContext> transitions)
        {
            transitions
                .From<GroundedState, AirborneState>()
                .On(context => context.Input.JumpRequested);

            transitions
                .From<AirborneState, GroundedState>()
                .On(context => context.Input.Landed);
        }
    }
}
