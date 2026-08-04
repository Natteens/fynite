using Fynite;

namespace FyniteSamples.CodeFirst
{
    /// <summary>
    /// The action is entered on a request and left on the event its own activity publishes, so the
    /// state never names what follows it.
    /// </summary>
    public sealed class ActionTransitions : IFyniteTransitions<ExampleContext>
    {
        public void Configure(FyniteTransitions<ExampleContext> transitions)
        {
            transitions
                .From<GroundedState, ActionState>()
                .On(context => context.Input.ActionRequested);

            transitions
                .From<ActionState, GroundedState>()
                .On(context => context.ActionFinished);
        }
    }
}
