using System;
using Fynite.Diagnostics;

namespace Fynite
{
    /// <summary>
    /// An immutable, shareable HFSM description: the state tree, the declared signals, and the
    /// factories that produce per-machine block instances.
    /// </summary>
    /// <remarks>
    /// A definition holds no mutable runtime state, so one instance can back any number of machines
    /// (for example a single compiled enemy archetype driving fifty entities). Everything mutable —
    /// active path, signal queue, block instances — lives in <see cref="FyniteMachine{TContext}"/>.
    /// Create definitions through <see cref="FyniteBuilder{TContext}"/>.
    /// </remarks>
    /// <typeparam name="TContext">Context type the machines execute against.</typeparam>
    public sealed class FyniteDefinition<TContext> : IFyniteDefinition where TContext : class
    {
        private readonly int _owner;
        private readonly StateRuntime[] _states;
        private readonly SignalRuntime[] _signals;
        private readonly ReactionRuntime[] _reactions;
        private readonly Func<IFyniteAction<TContext>>[] _actionFactories;
        private readonly Func<IFyniteGuard<TContext>>[] _guardFactories;

        internal FyniteDefinition(
            int owner,
            StateRuntime[] states,
            SignalRuntime[] signals,
            ReactionRuntime[] reactions,
            Func<IFyniteAction<TContext>>[] actionFactories,
            Func<IFyniteGuard<TContext>>[] guardFactories,
            int rootIndex,
            int maxDepth)
        {
            _owner = owner;
            _states = states;
            _signals = signals;
            _reactions = reactions;
            _actionFactories = actionFactories;
            _guardFactories = guardFactories;
            Root = states[rootIndex].Handle;
            MaxDepth = maxDepth;
        }

        /// <inheritdoc />
        public int StateCount => _states.Length;

        /// <inheritdoc />
        public int SignalCount => _signals.Length;

        /// <inheritdoc />
        public StateHandle Root { get; }

        /// <inheritdoc />
        public int MaxDepth { get; }

        /// <inheritdoc />
        public Type ContextType => typeof(TContext);

        internal StateRuntime[] States => _states;

        internal ReactionRuntime[] Reactions => _reactions;

        internal SignalRuntime[] Signals => _signals;

        internal Func<IFyniteAction<TContext>>[] ActionFactories => _actionFactories;

        internal Func<IFyniteGuard<TContext>>[] GuardFactories => _guardFactories;

        /// <summary>Creates an independent machine bound to <paramref name="context"/>.</summary>
        /// <param name="context">Entity state the blocks operate on. Never discovered automatically.</param>
        /// <param name="diagnostics">Optional observer; <see cref="NullFyniteDiagnostics"/> when omitted.</param>
        /// <param name="options">Optional tuning; values are copied.</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
        public FyniteMachine<TContext> CreateMachine(
            TContext context,
            IFyniteDiagnostics diagnostics = null,
            FyniteMachineOptions options = null)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new FyniteMachine<TContext>(this, context, diagnostics, options);
        }

        /// <inheritdoc />
        public bool Owns(StateHandle state) => state.IsValid && state.Owner == _owner && (uint)state.Index < (uint)_states.Length;

        /// <inheritdoc />
        public bool Owns(SignalHandle signal) => signal.IsValid && signal.Owner == _owner && (uint)signal.Index < (uint)_signals.Length;

        /// <inheritdoc />
        public string GetStateName(StateHandle state) => _states[RequireState(state, nameof(state))].Name;

        /// <inheritdoc />
        public string GetSignalName(SignalHandle signal) => _signals[RequireSignal(signal, nameof(signal))].Name;

        /// <inheritdoc />
        public StateHandle GetParent(StateHandle state)
        {
            int parent = _states[RequireState(state, nameof(state))].Parent;
            return parent < 0 ? StateHandle.Invalid : _states[parent].Handle;
        }

        /// <inheritdoc />
        public StateHandle GetInitialChild(StateHandle state)
        {
            int initial = _states[RequireState(state, nameof(state))].InitialChild;
            return initial < 0 ? StateHandle.Invalid : _states[initial].Handle;
        }

        /// <inheritdoc />
        public int GetDepth(StateHandle state) => _states[RequireState(state, nameof(state))].Depth;

        /// <inheritdoc />
        public int GetChildCount(StateHandle state) => _states[RequireState(state, nameof(state))].Children.Length;

        /// <inheritdoc />
        public StateHandle GetChild(StateHandle state, int childIndex)
        {
            var children = _states[RequireState(state, nameof(state))].Children;
            if ((uint)childIndex >= (uint)children.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(childIndex),
                    childIndex,
                    "State '" + GetStateName(state) + "' has " + children.Length + " children.");
            }

            return _states[children[childIndex]].Handle;
        }

        /// <inheritdoc />
        public Type GetPayloadType(SignalHandle signal) => _signals[RequireSignal(signal, nameof(signal))].PayloadType;

        /// <inheritdoc />
        public SignalHandle GetSignal(int index)
        {
            if ((uint)index >= (uint)_signals.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, "This definition declares " + _signals.Length + " signals.");
            }

            return _signals[index].Handle;
        }

        /// <inheritdoc />
        public StateHandle GetState(int index)
        {
            if ((uint)index >= (uint)_states.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, "This definition declares " + _states.Length + " states.");
            }

            return _states[index].Handle;
        }

        internal int RequireState(StateHandle state, string parameterName)
        {
            if (!Owns(state))
            {
                throw new ArgumentException(
                    state.IsValid
                        ? "State handle " + state + " does not belong to this definition."
                        : "State handle is invalid.",
                    parameterName);
            }

            return state.Index;
        }

        internal int RequireSignal(SignalHandle signal, string parameterName)
        {
            if (!Owns(signal))
            {
                throw new ArgumentException(
                    signal.IsValid
                        ? "Signal handle " + signal + " does not belong to this definition."
                        : "Signal handle is invalid.",
                    parameterName);
            }

            return signal.Index;
        }
    }
}
