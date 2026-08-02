using System;

namespace Fynite
{
    /// <summary>
    /// Configures one reaction: its guards, its effects, its optional target and its priority.
    /// </summary>
    /// <remarks>
    /// A reaction with a target is a transition; a reaction without one only runs effects and leaves
    /// the active path untouched. There is deliberately no separate transition type.
    /// </remarks>
    /// <typeparam name="TContext">Context type the machines will execute against.</typeparam>
    public readonly struct FyniteReactionBuilder<TContext> where TContext : class
    {
        private readonly FyniteBuilder<TContext> _builder;
        private readonly int _stateIndex;
        private readonly int _reactionIndex;

        internal FyniteReactionBuilder(FyniteBuilder<TContext> builder, int stateIndex, int reactionIndex)
        {
            _builder = builder;
            _stateIndex = stateIndex;
            _reactionIndex = reactionIndex;
        }

        /// <summary>Appends a guard. Guards run in registration order and short-circuit on the first false.</summary>
        public FyniteReactionBuilder<TContext> When(Func<IFyniteGuard<TContext>> factory)
        {
            _builder.AddGuard(_stateIndex, _reactionIndex, factory);
            return this;
        }

        /// <summary>Appends a default-constructed guard.</summary>
        public FyniteReactionBuilder<TContext> When<TGuard>() where TGuard : IFyniteGuard<TContext>, new() =>
            When(() => new TGuard());

        /// <summary>Appends an effect. Effects run in registration order, between exits and entries.</summary>
        public FyniteReactionBuilder<TContext> Do(Func<IFyniteAction<TContext>> factory)
        {
            _builder.AddEffect(_stateIndex, _reactionIndex, factory);
            return this;
        }

        /// <summary>Appends a default-constructed effect.</summary>
        public FyniteReactionBuilder<TContext> Do<TAction>() where TAction : IFyniteAction<TContext>, new() =>
            Do(() => new TAction());

        /// <summary>
        /// Makes this reaction a transition to <paramref name="target"/>. Targeting the declaring state
        /// itself is a self-transition and restarts it.
        /// </summary>
        public FyniteReactionBuilder<TContext> TransitionTo(StateHandle target)
        {
            _builder.SetReactionTarget(_stateIndex, _reactionIndex, target);
            return this;
        }

        /// <summary>Sets the resolution priority; higher wins. Defaults to zero.</summary>
        public FyniteReactionBuilder<TContext> Priority(int priority)
        {
            _builder.SetReactionPriority(_stateIndex, _reactionIndex, priority);
            return this;
        }

        /// <summary>Declares another reaction on the same state.</summary>
        public FyniteReactionBuilder<TContext> On(SignalHandle signal) =>
            new FyniteReactionBuilder<TContext>(_builder, _stateIndex, _builder.AddReaction(_stateIndex, signal));

        /// <summary>Declares another reaction on the same state.</summary>
        public FyniteReactionBuilder<TContext> On<TPayload>(SignalHandle<TPayload> signal) => On(signal.Handle);
    }
}
