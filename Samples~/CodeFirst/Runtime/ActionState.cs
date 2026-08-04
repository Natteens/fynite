using Fynite;

namespace FyniteSamples.CodeFirst
{
    /// <summary>
    /// A state that takes time. The chain runs itself: start, half a second, the point where the
    /// action lands, and finally the event that lets a transition take the machine somewhere else.
    /// </summary>
    public sealed class ActionState : FyniteState<ExampleContext>
    {
        protected override void ConfigureActivity(FyniteActivityBuilder<ExampleContext> activity)
            => activity
                .Do(context => context.BeginAction())
                .Wait(0.5f)
                .Do(context => context.MarkActionPoint())
                .Publish(context => context.ActionFinished);

        /// <summary>
        /// Leaving cancels the chain on its own; gameplay cleanup is what still belongs here, because
        /// the state can be left before the chain ever reaches its end.
        /// </summary>
        protected override void Exit() => Context.CancelAction();
    }
}
