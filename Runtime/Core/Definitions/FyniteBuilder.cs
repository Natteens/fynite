using System;
using System.Collections.Generic;
using System.Text;

namespace Fynite
{
    /// <summary>
    /// Code-first construction of a <see cref="FyniteDefinition{TContext}"/>.
    /// </summary>
    /// <remarks>
    /// Declare states and signals, wire the hierarchy, attach blocks and reactions, then call
    /// <see cref="Build"/>. Everything is validated at build time and the resulting definition is
    /// immutable; the builder rejects any further mutation once it has produced a definition.
    /// A builder is not thread-safe, and neither is anything else in Fynite.
    /// </remarks>
    /// <typeparam name="TContext">Context type the machines will execute against.</typeparam>
    public sealed class FyniteBuilder<TContext> where TContext : class
    {
        private readonly int _owner;
        private readonly List<BuilderState> _states = new List<BuilderState>();
        private readonly List<BuilderSignal> _signals = new List<BuilderSignal>();

        private int _rootIndex = -1;
        private int _reactionOrder;
        private bool _built;

        /// <summary>Creates an empty builder with its own handle namespace.</summary>
        public FyniteBuilder()
        {
            _owner = FyniteHandleOwner.Next();
        }

        /// <summary>Number of states declared so far.</summary>
        public int StateCount => _states.Count;

        /// <summary>Number of signals declared so far.</summary>
        public int SignalCount => _signals.Count;

        /// <summary>Declares a state with no parent. Exactly one such state may remain at build time.</summary>
        /// <param name="name">Diagnostic label; never used for lookup.</param>
        public StateHandle AddState(string name)
        {
            ThrowIfBuilt();
            RequireName(name, nameof(name));

            var state = new BuilderState(name);
            _states.Add(state);
            return new StateHandle(_owner, _states.Count - 1);
        }

        /// <summary>Declares a state as a direct child of <paramref name="parent"/>.</summary>
        /// <param name="name">Diagnostic label; never used for lookup.</param>
        /// <param name="parent">An already declared state of this builder.</param>
        public StateHandle AddState(string name, StateHandle parent)
        {
            ThrowIfBuilt();
            RequireName(name, nameof(name));
            int parentIndex = RequireState(parent, nameof(parent));

            var state = new BuilderState(name) { Parent = parentIndex };
            _states.Add(state);
            return new StateHandle(_owner, _states.Count - 1);
        }

        /// <summary>
        /// Sets or replaces the parent of a state. Replacing an existing parent is allowed while
        /// building; the resulting hierarchy is validated by <see cref="Build"/>.
        /// </summary>
        public FyniteBuilder<TContext> SetParent(StateHandle child, StateHandle parent)
        {
            ThrowIfBuilt();
            int childIndex = RequireState(child, nameof(child));
            int parentIndex = RequireState(parent, nameof(parent));

            _states[childIndex].Parent = parentIndex;
            return this;
        }

        /// <summary>Designates the single root of the tree.</summary>
        public FyniteBuilder<TContext> SetRoot(StateHandle root)
        {
            ThrowIfBuilt();
            _rootIndex = RequireState(root, nameof(root));
            return this;
        }

        /// <summary>
        /// Declares which direct child a composite state enters when it becomes active.
        /// Every composite state needs exactly one; leaf states must not have one.
        /// </summary>
        public FyniteBuilder<TContext> SetInitial(StateHandle composite, StateHandle initialChild)
        {
            ThrowIfBuilt();
            int compositeIndex = RequireState(composite, nameof(composite));
            int childIndex = RequireState(initialChild, nameof(initialChild));

            _states[compositeIndex].InitialChild = childIndex;
            return this;
        }

        /// <summary>Declares a signal that carries no payload.</summary>
        public SignalHandle AddSignal(string name)
        {
            ThrowIfBuilt();
            RequireName(name, nameof(name));

            _signals.Add(new BuilderSignal(name, null));
            return new SignalHandle(_owner, _signals.Count - 1);
        }

        /// <summary>Declares a signal that must be raised with a <typeparamref name="TPayload"/> payload.</summary>
        public SignalHandle<TPayload> AddSignal<TPayload>(string name)
        {
            ThrowIfBuilt();
            RequireName(name, nameof(name));

            _signals.Add(new BuilderSignal(name, typeof(TPayload)));
            return new SignalHandle<TPayload>(new SignalHandle(_owner, _signals.Count - 1));
        }

        /// <summary>
        /// Declares a signal whose payload type is only known as a <see cref="Type"/>.
        /// </summary>
        /// <remarks>
        /// The generic overloads are the normal way to declare signals. This one exists for hosts that
        /// rebuild a definition from persisted data, where the payload type arrives as a
        /// <see cref="Type"/> obtained without reflection (for example from a serialized typed carrier).
        /// Passing <c>null</c> declares a signal without payload and is identical to
        /// <see cref="AddSignal(string)"/>.
        /// </remarks>
        /// <param name="name">Diagnostic label; never used for lookup.</param>
        /// <param name="payloadType">Declared payload type, or <c>null</c> for a payload-less signal.</param>
        public SignalHandle AddSignal(string name, Type payloadType)
        {
            ThrowIfBuilt();
            RequireName(name, nameof(name));

            _signals.Add(new BuilderSignal(name, payloadType));
            return new SignalHandle(_owner, _signals.Count - 1);
        }

        /// <summary>Starts configuring blocks and reactions of a state.</summary>
        public FyniteStateBuilder<TContext> State(StateHandle state)
        {
            ThrowIfBuilt();
            return new FyniteStateBuilder<TContext>(this, RequireState(state, nameof(state)));
        }

        /// <summary>
        /// Validates the described machine and bakes it into an immutable definition.
        /// </summary>
        /// <exception cref="FyniteDefinitionException">The description is not a valid HFSM.</exception>
        public FyniteDefinition<TContext> Build()
        {
            ThrowIfBuilt();

            ValidateRoot();
            DetectParentCycles();
            var children = BuildChildren();
            ValidateReachability(children);
            ValidateInitialChildren(children);
            ValidateReactions();

            var states = BakeStates(children, out int maxDepth);
            var actionFactories = new List<Func<IFyniteAction<TContext>>>();
            var guardFactories = new List<Func<IFyniteGuard<TContext>>>();
            var reactions = new List<ReactionRuntime>();

            BakeBlocks(states, actionFactories);
            BakeReactions(states, reactions, actionFactories, guardFactories);

            var signals = new SignalRuntime[_signals.Count];
            for (int i = 0; i < _signals.Count; i++)
            {
                signals[i] = new SignalRuntime
                {
                    Handle = new SignalHandle(_owner, i),
                    Name = _signals[i].Name,
                    PayloadType = _signals[i].PayloadType
                };
            }

            _built = true;

            return new FyniteDefinition<TContext>(
                _owner,
                states,
                signals,
                reactions.ToArray(),
                actionFactories.ToArray(),
                guardFactories.ToArray(),
                _rootIndex,
                maxDepth);
        }

        internal void AddBlock(int stateIndex, FynitePhase phase, Func<IFyniteAction<TContext>> factory)
        {
            ThrowIfBuilt();
            if (factory == null)
            {
                throw new ArgumentNullException(
                    nameof(factory),
                    "A " + phase + " block factory of state '" + _states[stateIndex].Name + "' is null.");
            }

            var state = _states[stateIndex];
            switch (phase)
            {
                case FynitePhase.Enter:
                    state.Enter.Add(factory);
                    break;
                case FynitePhase.Tick:
                    state.Tick.Add(factory);
                    break;
                case FynitePhase.FixedTick:
                    state.FixedTick.Add(factory);
                    break;
                case FynitePhase.Exit:
                    state.Exit.Add(factory);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, "Only Enter, Tick, FixedTick and Exit blocks can be attached to a state.");
            }
        }

        internal int AddReaction(int stateIndex, SignalHandle signal)
        {
            ThrowIfBuilt();
            RequireSignal(signal, nameof(signal));

            var reaction = new BuilderReaction(stateIndex, signal.Index, _reactionOrder++);
            _states[stateIndex].Reactions.Add(reaction);
            return _states[stateIndex].Reactions.Count - 1;
        }

        internal void AddGuard(int stateIndex, int reactionIndex, Func<IFyniteGuard<TContext>> factory)
        {
            ThrowIfBuilt();
            if (factory == null)
            {
                throw new ArgumentNullException(
                    nameof(factory),
                    "A guard factory of a reaction on state '" + _states[stateIndex].Name + "' is null.");
            }

            _states[stateIndex].Reactions[reactionIndex].Guards.Add(factory);
        }

        internal void AddEffect(int stateIndex, int reactionIndex, Func<IFyniteAction<TContext>> factory)
        {
            ThrowIfBuilt();
            if (factory == null)
            {
                throw new ArgumentNullException(
                    nameof(factory),
                    "An effect factory of a reaction on state '" + _states[stateIndex].Name + "' is null.");
            }

            _states[stateIndex].Reactions[reactionIndex].Effects.Add(factory);
        }

        internal void SetReactionTarget(int stateIndex, int reactionIndex, StateHandle target)
        {
            ThrowIfBuilt();
            _states[stateIndex].Reactions[reactionIndex].Target = RequireState(target, nameof(target));
        }

        internal void SetReactionPriority(int stateIndex, int reactionIndex, int priority)
        {
            ThrowIfBuilt();
            _states[stateIndex].Reactions[reactionIndex].Priority = priority;
        }

        internal int RequireState(StateHandle state, string parameterName)
        {
            if (!state.IsValid)
            {
                throw new ArgumentException("State handle is invalid.", parameterName);
            }

            if (state.Owner != _owner || (uint)state.Index >= (uint)_states.Count)
            {
                throw new ArgumentException(
                    "State handle " + state + " was not produced by this builder.",
                    parameterName);
            }

            return state.Index;
        }

        internal int RequireSignal(SignalHandle signal, string parameterName)
        {
            if (!signal.IsValid)
            {
                throw new ArgumentException("Signal handle is invalid.", parameterName);
            }

            if (signal.Owner != _owner || (uint)signal.Index >= (uint)_signals.Count)
            {
                throw new ArgumentException(
                    "Signal handle " + signal + " was not produced by this builder.",
                    parameterName);
            }

            return signal.Index;
        }

        private void ThrowIfBuilt()
        {
            if (_built)
            {
                throw new InvalidOperationException(
                    "This builder has already produced a definition; a definition is immutable and the builder cannot be reused.");
            }
        }

        private static void RequireName(string name, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A diagnostic name is required.", parameterName);
            }
        }

        private void ValidateRoot()
        {
            if (_states.Count == 0)
            {
                throw new FyniteDefinitionException("The definition is empty: at least one state is required.");
            }

            if (_rootIndex < 0)
            {
                throw new FyniteDefinitionException(
                    "No root state was set. Call SetRoot with the state that owns the whole hierarchy.");
            }

            if (_states[_rootIndex].Parent >= 0)
            {
                throw new FyniteDefinitionException(
                    "Root state '" + _states[_rootIndex].Name + "' must not have a parent, but its parent is '" +
                    _states[_states[_rootIndex].Parent].Name + "'.");
            }
        }

        private void DetectParentCycles()
        {
            // 0 = unvisited, 1 = on the chain currently being walked, 2 = proven to terminate.
            var status = new byte[_states.Count];
            var chain = new List<int>();

            for (int i = 0; i < _states.Count; i++)
            {
                if (status[i] != 0)
                {
                    continue;
                }

                chain.Clear();
                int current = i;
                while (current >= 0 && status[current] == 0)
                {
                    status[current] = 1;
                    chain.Add(current);
                    current = _states[current].Parent;
                }

                if (current >= 0 && status[current] == 1)
                {
                    throw new FyniteDefinitionException(
                        "A parent cycle was detected: " + DescribeCycle(chain, current) + ".");
                }

                for (int c = 0; c < chain.Count; c++)
                {
                    status[chain[c]] = 2;
                }
            }
        }

        private string DescribeCycle(List<int> chain, int cycleStart)
        {
            var text = new StringBuilder();
            int start = chain.IndexOf(cycleStart);
            for (int i = start; i < chain.Count; i++)
            {
                text.Append('\'').Append(_states[chain[i]].Name).Append("' → ");
            }

            text.Append('\'').Append(_states[cycleStart].Name).Append('\'');
            return text.ToString();
        }

        private List<int>[] BuildChildren()
        {
            var children = new List<int>[_states.Count];
            for (int i = 0; i < _states.Count; i++)
            {
                children[i] = new List<int>();
            }

            for (int i = 0; i < _states.Count; i++)
            {
                int parent = _states[i].Parent;
                if (parent >= 0)
                {
                    children[parent].Add(i);
                }
            }

            return children;
        }

        private void ValidateReachability(List<int>[] children)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                if (i != _rootIndex && _states[i].Parent < 0)
                {
                    throw new FyniteDefinitionException(
                        "State '" + _states[i].Name + "' is not reachable from root '" + _states[_rootIndex].Name +
                        "' because it has no parent. Only the root state may be parentless.");
                }
            }

            var reached = new bool[_states.Count];
            var pending = new Stack<int>();
            pending.Push(_rootIndex);
            reached[_rootIndex] = true;

            while (pending.Count > 0)
            {
                int current = pending.Pop();
                var childList = children[current];
                for (int c = 0; c < childList.Count; c++)
                {
                    int child = childList[c];
                    if (!reached[child])
                    {
                        reached[child] = true;
                        pending.Push(child);
                    }
                }
            }

            for (int i = 0; i < _states.Count; i++)
            {
                if (!reached[i])
                {
                    throw new FyniteDefinitionException(
                        "State '" + _states[i].Name + "' is not reachable from root '" + _states[_rootIndex].Name + "'.");
                }
            }
        }

        private void ValidateInitialChildren(List<int>[] children)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                var state = _states[i];
                bool hasChildren = children[i].Count > 0;

                if (hasChildren && state.InitialChild < 0)
                {
                    throw new FyniteDefinitionException(
                        "State '" + state.Name + "' has children but no initial child. Call SetInitial for every composite state.");
                }

                if (!hasChildren && state.InitialChild >= 0)
                {
                    throw new FyniteDefinitionException(
                        "State '" + state.Name + "' is a leaf but declares '" + _states[state.InitialChild].Name +
                        "' as its initial child.");
                }

                if (state.InitialChild >= 0 && _states[state.InitialChild].Parent != i)
                {
                    throw new FyniteDefinitionException(
                        "State '" + state.Name + "' cannot use '" + _states[state.InitialChild].Name +
                        "' as initial child because it is not its direct child.");
                }
            }
        }

        private void ValidateReactions()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                var state = _states[i];
                for (int r = 0; r < state.Reactions.Count; r++)
                {
                    var reaction = state.Reactions[r];

                    if ((uint)reaction.Signal >= (uint)_signals.Count)
                    {
                        throw new FyniteDefinitionException(
                            "A reaction registered on '" + state.Name + "' refers to a signal that does not belong to this definition.");
                    }

                    if (reaction.Target >= 0 && (uint)reaction.Target >= (uint)_states.Count)
                    {
                        throw new FyniteDefinitionException(
                            "A reaction registered on '" + state.Name + "' targets a state that does not belong to this definition.");
                    }
                }
            }
        }

        private StateRuntime[] BakeStates(List<int>[] children, out int maxDepth)
        {
            var states = new StateRuntime[_states.Count];
            for (int i = 0; i < _states.Count; i++)
            {
                states[i] = new StateRuntime
                {
                    Handle = new StateHandle(_owner, i),
                    Name = _states[i].Name,
                    Index = i,
                    Parent = _states[i].Parent,
                    InitialChild = _states[i].InitialChild,
                    Children = children[i].ToArray(),
                    ReactionsBySignal = null
                };
            }

            maxDepth = 0;
            var pending = new Stack<int>();
            pending.Push(_rootIndex);
            states[_rootIndex].Depth = 0;
            states[_rootIndex].AncestorPath = new[] { _rootIndex };

            while (pending.Count > 0)
            {
                int current = pending.Pop();
                var parentPath = states[current].AncestorPath;
                if (states[current].Depth + 1 > maxDepth)
                {
                    maxDepth = states[current].Depth + 1;
                }

                var childIndices = states[current].Children;
                for (int c = 0; c < childIndices.Length; c++)
                {
                    int child = childIndices[c];
                    states[child].Depth = states[current].Depth + 1;

                    var path = new int[parentPath.Length + 1];
                    Array.Copy(parentPath, path, parentPath.Length);
                    path[parentPath.Length] = child;
                    states[child].AncestorPath = path;

                    pending.Push(child);
                }
            }

            return states;
        }

        private void BakeBlocks(StateRuntime[] states, List<Func<IFyniteAction<TContext>>> actionFactories)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                var source = _states[i];
                states[i].Enter = Append(actionFactories, source.Enter);
                states[i].Tick = Append(actionFactories, source.Tick);
                states[i].FixedTick = Append(actionFactories, source.FixedTick);
                states[i].Exit = Append(actionFactories, source.Exit);
            }
        }

        private void BakeReactions(
            StateRuntime[] states,
            List<ReactionRuntime> reactions,
            List<Func<IFyniteAction<TContext>>> actionFactories,
            List<Func<IFyniteGuard<TContext>>> guardFactories)
        {
            var perState = new List<ReactionRuntime>();

            for (int i = 0; i < _states.Count; i++)
            {
                var source = _states[i];
                if (source.Reactions.Count == 0)
                {
                    continue;
                }

                perState.Clear();
                for (int r = 0; r < source.Reactions.Count; r++)
                {
                    var declared = source.Reactions[r];
                    perState.Add(new ReactionRuntime
                    {
                        SourceHandle = states[i].Handle,
                        TargetHandle = declared.Target >= 0 ? states[declared.Target].Handle : StateHandle.Invalid,
                        SignalHandle = new SignalHandle(_owner, declared.Signal),
                        Source = i,
                        Signal = declared.Signal,
                        Target = declared.Target,
                        Priority = declared.Priority,
                        RegistrationOrder = declared.RegistrationOrder,
                        SourceDepth = states[i].Depth,
                        Guards = Append(guardFactories, declared.Guards),
                        Effects = Append(actionFactories, declared.Effects)
                    });
                }

                // Resolution reads the head of each state's slice, so ordering by descending priority
                // and then by registration order makes that head the state's best candidate.
                perState.Sort(CompareReactions);

                var ranges = new Dictionary<int, ReactionRange>();
                int sliceStart = reactions.Count;
                for (int r = 0; r < perState.Count; r++)
                {
                    reactions.Add(perState[r]);
                }

                int index = 0;
                while (index < perState.Count)
                {
                    int signal = perState[index].Signal;
                    int count = 0;
                    while (index + count < perState.Count && perState[index + count].Signal == signal)
                    {
                        count++;
                    }

                    ranges[signal] = new ReactionRange(sliceStart + index, count);
                    index += count;
                }

                states[i].ReactionsBySignal = ranges;
            }
        }

        private static int CompareReactions(ReactionRuntime a, ReactionRuntime b)
        {
            if (a.Signal != b.Signal)
            {
                return a.Signal.CompareTo(b.Signal);
            }

            if (a.Priority != b.Priority)
            {
                return b.Priority.CompareTo(a.Priority);
            }

            return a.RegistrationOrder.CompareTo(b.RegistrationOrder);
        }

        private static SlotRange Append<T>(List<T> target, List<T> source)
        {
            if (source.Count == 0)
            {
                return default;
            }

            int start = target.Count;
            target.AddRange(source);
            return new SlotRange(start, source.Count);
        }

        private sealed class BuilderState
        {
            internal BuilderState(string name)
            {
                Name = name;
            }

            internal string Name { get; }

            internal int Parent { get; set; } = -1;

            internal int InitialChild { get; set; } = -1;

            internal List<Func<IFyniteAction<TContext>>> Enter { get; } = new List<Func<IFyniteAction<TContext>>>();

            internal List<Func<IFyniteAction<TContext>>> Tick { get; } = new List<Func<IFyniteAction<TContext>>>();

            internal List<Func<IFyniteAction<TContext>>> FixedTick { get; } = new List<Func<IFyniteAction<TContext>>>();

            internal List<Func<IFyniteAction<TContext>>> Exit { get; } = new List<Func<IFyniteAction<TContext>>>();

            internal List<BuilderReaction> Reactions { get; } = new List<BuilderReaction>();
        }

        private sealed class BuilderReaction
        {
            internal BuilderReaction(int source, int signal, int registrationOrder)
            {
                Source = source;
                Signal = signal;
                RegistrationOrder = registrationOrder;
            }

            internal int Source { get; }

            internal int Signal { get; }

            internal int RegistrationOrder { get; }

            internal int Target { get; set; } = -1;

            internal int Priority { get; set; }

            internal List<Func<IFyniteGuard<TContext>>> Guards { get; } = new List<Func<IFyniteGuard<TContext>>>();

            internal List<Func<IFyniteAction<TContext>>> Effects { get; } = new List<Func<IFyniteAction<TContext>>>();
        }

        private sealed class BuilderSignal
        {
            internal BuilderSignal(string name, Type payloadType)
            {
                Name = name;
                PayloadType = payloadType;
            }

            internal string Name { get; }

            internal Type PayloadType { get; }
        }
    }
}
