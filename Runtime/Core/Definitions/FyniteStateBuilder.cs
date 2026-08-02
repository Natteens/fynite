using System;

namespace Fynite
{
    /// <summary>
    /// Attaches blocks and reactions to one state. Obtained from
    /// <see cref="FyniteBuilder{TContext}.State"/>; every call registers immediately, so there is no
    /// half-configured object that could escape validation.
    /// </summary>
    /// <typeparam name="TContext">Context type the machines will execute against.</typeparam>
    public readonly struct FyniteStateBuilder<TContext> where TContext : class
    {
        private readonly FyniteBuilder<TContext> _builder;
        private readonly int _stateIndex;

        internal FyniteStateBuilder(FyniteBuilder<TContext> builder, int stateIndex)
        {
            _builder = builder;
            _stateIndex = stateIndex;
        }

        /// <summary>Appends a block that runs when this state is entered.</summary>
        public FyniteStateBuilder<TContext> OnEnter(Func<IFyniteAction<TContext>> factory)
        {
            _builder.AddBlock(_stateIndex, FynitePhase.Enter, factory);
            return this;
        }

        /// <summary>Appends a default-constructed block that runs when this state is entered.</summary>
        public FyniteStateBuilder<TContext> OnEnter<TAction>() where TAction : IFyniteAction<TContext>, new() =>
            OnEnter(() => new TAction());

        /// <summary>Appends a block that runs every <c>Tick</c> while this state is active.</summary>
        public FyniteStateBuilder<TContext> OnTick(Func<IFyniteAction<TContext>> factory)
        {
            _builder.AddBlock(_stateIndex, FynitePhase.Tick, factory);
            return this;
        }

        /// <summary>Appends a default-constructed block that runs every <c>Tick</c>.</summary>
        public FyniteStateBuilder<TContext> OnTick<TAction>() where TAction : IFyniteAction<TContext>, new() =>
            OnTick(() => new TAction());

        /// <summary>Appends a block that runs every <c>FixedTick</c> while this state is active.</summary>
        public FyniteStateBuilder<TContext> OnFixedTick(Func<IFyniteAction<TContext>> factory)
        {
            _builder.AddBlock(_stateIndex, FynitePhase.FixedTick, factory);
            return this;
        }

        /// <summary>Appends a default-constructed block that runs every <c>FixedTick</c>.</summary>
        public FyniteStateBuilder<TContext> OnFixedTick<TAction>() where TAction : IFyniteAction<TContext>, new() =>
            OnFixedTick(() => new TAction());

        /// <summary>Appends a block that runs when this state is exited.</summary>
        public FyniteStateBuilder<TContext> OnExit(Func<IFyniteAction<TContext>> factory)
        {
            _builder.AddBlock(_stateIndex, FynitePhase.Exit, factory);
            return this;
        }

        /// <summary>Appends a default-constructed block that runs when this state is exited.</summary>
        public FyniteStateBuilder<TContext> OnExit<TAction>() where TAction : IFyniteAction<TContext>, new() =>
            OnExit(() => new TAction());

        /// <summary>
        /// Declares a reaction to <paramref name="signal"/> on this state. Without a target it only runs
        /// its effects; with one it becomes a transition.
        /// </summary>
        public FyniteReactionBuilder<TContext> On(SignalHandle signal) =>
            new FyniteReactionBuilder<TContext>(_builder, _stateIndex, _builder.AddReaction(_stateIndex, signal));

        /// <summary>Declares a reaction to a typed signal on this state.</summary>
        public FyniteReactionBuilder<TContext> On<TPayload>(SignalHandle<TPayload> signal) => On(signal.Handle);
    }
}
