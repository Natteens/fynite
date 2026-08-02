using System;
using Fynite.Diagnostics;

namespace Fynite
{
    /// <summary>
    /// One running instance of a <see cref="FyniteDefinition{TContext}"/>: a single active path, its own
    /// signal queue and its own block instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Machines are not thread-safe. Drive one from a single thread — under Unity, the main thread.
    /// </para>
    /// <para>
    /// Signals never re-enter the executor. <c>Raise</c> from outside any machine operation drains the
    /// queue immediately; <c>Raise</c> from inside a block, guard or effect only enqueues, and the
    /// queue is drained once the current operation has completed.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContext">Context type the blocks operate on.</typeparam>
    public sealed class FyniteMachine<TContext> : IFyniteMachine, IFyniteSignalSink where TContext : class
    {
        private readonly FyniteDefinition<TContext> _definition;
        private readonly TContext _context;
        private readonly IFyniteDiagnostics _diagnostics;
        private readonly int _maxMicrosteps;

        private readonly IFyniteAction<TContext>[] _actions;
        private readonly IFyniteGuard<TContext>[] _guards;

        private readonly StateHandle[] _activePath;
        private readonly int[] _cursors;
        private readonly int[] _cursorEnds;
        private readonly SignalQueue _queue;

        private int _activeDepth;
        private int _executing;
        private bool _stopping;
        private FyniteLifecycle _lifecycle;

        private FyniteSignal _currentSignal;
        private StateHandle _currentSource;
        private StateHandle _currentTarget;

        internal FyniteMachine(
            FyniteDefinition<TContext> definition,
            TContext context,
            IFyniteDiagnostics diagnostics,
            FyniteMachineOptions options)
        {
            _definition = definition;
            _context = context;
            _diagnostics = diagnostics ?? NullFyniteDiagnostics.Instance;

            var effectiveOptions = options ?? FyniteMachineOptions.Default;
            effectiveOptions.Validate();
            _maxMicrosteps = effectiveOptions.MaxMicrostepsPerPump;

            _queue = new SignalQueue(effectiveOptions.InitialSignalCapacity);
            _activePath = new StateHandle[definition.MaxDepth];
            _cursors = new int[definition.MaxDepth];
            _cursorEnds = new int[definition.MaxDepth];

            _actions = new IFyniteAction<TContext>[definition.ActionFactories.Length];
            _guards = new IFyniteGuard<TContext>[definition.GuardFactories.Length];
            InstantiateBlocks();

            _lifecycle = FyniteLifecycle.Created;
        }

        /// <inheritdoc />
        public FyniteLifecycle Lifecycle => _lifecycle;

        /// <inheritdoc />
        public bool IsRunning => _lifecycle == FyniteLifecycle.Running;

        /// <summary>Structure this machine executes.</summary>
        public FyniteDefinition<TContext> Definition => _definition;

        /// <summary>The context supplied when the machine was created.</summary>
        public TContext Context => _context;

        IFyniteDefinition IFyniteMachine.Definition => _definition;

        /// <inheritdoc />
        public int ActiveDepth => _activeDepth;

        /// <inheritdoc />
        public StateHandle ActiveLeaf => _activeDepth == 0 ? StateHandle.Invalid : _activePath[_activeDepth - 1];

        /// <summary>
        /// The active path from root to leaf. The span is a view over the machine's own buffer, so
        /// reading it never copies and never allocates.
        /// </summary>
        public ReadOnlySpan<StateHandle> ActivePath => new ReadOnlySpan<StateHandle>(_activePath, 0, _activeDepth);

        /// <inheritdoc />
        public StateHandle GetActiveState(int depth)
        {
            if ((uint)depth >= (uint)_activeDepth)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(depth),
                    depth,
                    "The active path currently holds " + _activeDepth + " states.");
            }

            return _activePath[depth];
        }

        /// <inheritdoc />
        public bool IsActive(StateHandle state)
        {
            int index = _definition.RequireState(state, nameof(state));
            int depth = _definition.States[index].Depth;
            return depth < _activeDepth && _activePath[depth] == state;
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Already running, faulted, or called from inside a block.</exception>
        /// <exception cref="ObjectDisposedException">The machine was disposed.</exception>
        public void Start()
        {
            RequireUsable(nameof(Start));
            RequireNotExecuting(nameof(Start));

            if (_lifecycle == FyniteLifecycle.Running)
            {
                throw new InvalidOperationException("The machine is already running; call Stop before starting it again.");
            }

            _queue.Clear();
            _activeDepth = 0;
            _lifecycle = FyniteLifecycle.Running;

            _executing++;
            try
            {
                int current = _definition.Root.Index;
                EnterState(current);
                while (_definition.States[current].InitialChild >= 0)
                {
                    current = _definition.States[current].InitialChild;
                    EnterState(current);
                }
            }
            catch (Exception exception)
            {
                _executing--;
                Fault(exception);
                throw;
            }

            _executing--;

            // Signals raised by enter blocks are only dispatched once the whole initial chain exists.
            Pump();
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Called from inside a block.</exception>
        /// <exception cref="ObjectDisposedException">The machine was disposed.</exception>
        public void Stop()
        {
            if (_lifecycle == FyniteLifecycle.Disposed)
            {
                throw new ObjectDisposedException(nameof(FyniteMachine<TContext>));
            }

            RequireNotExecuting(nameof(Stop));

            if (_lifecycle != FyniteLifecycle.Running)
            {
                // Idempotent for Created, Stopped and Faulted: a faulted machine must not run
                // structural callbacks again.
                return;
            }

            StopCore();
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">The machine is not running, or the call is re-entrant.</exception>
        public void Tick(float deltaTime)
        {
            RequireRunning(nameof(Tick));
            RequireNotExecuting(nameof(Tick));

            Pump();
            if (_lifecycle != FyniteLifecycle.Running)
            {
                return;
            }

            RunPhaseOverActivePath(FynitePhase.Tick, deltaTime);
            Pump();
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">The machine is not running, or the call is re-entrant.</exception>
        public void FixedTick(float fixedDeltaTime)
        {
            RequireRunning(nameof(FixedTick));
            RequireNotExecuting(nameof(FixedTick));

            Pump();
            if (_lifecycle != FyniteLifecycle.Running)
            {
                return;
            }

            RunPhaseOverActivePath(FynitePhase.FixedTick, fixedDeltaTime);
            Pump();
        }

        /// <inheritdoc />
        public void Raise(SignalHandle signal) => RaiseCore(signal, null, false);

        /// <inheritdoc />
        public void Raise(SignalHandle signal, object payload) => RaiseCore(signal, payload, true);

        /// <inheritdoc />
        public void Raise<TPayload>(SignalHandle<TPayload> signal, TPayload payload) =>
            RaiseCore(signal.Handle, payload, true);

        /// <summary>
        /// Stops the machine if needed, then disposes every block instance exactly once and makes the
        /// machine permanently unusable. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            if (_lifecycle == FyniteLifecycle.Disposed)
            {
                return;
            }

            RequireNotExecuting(nameof(Dispose));

            try
            {
                if (_lifecycle == FyniteLifecycle.Running)
                {
                    StopCore();
                }
            }
            finally
            {
                _activeDepth = 0;
                _queue.Clear();
                _lifecycle = FyniteLifecycle.Disposed;
                DisposeBlocks();
            }
        }

        private void InstantiateBlocks()
        {
            try
            {
                var actionFactories = _definition.ActionFactories;
                for (int i = 0; i < actionFactories.Length; i++)
                {
                    _actions[i] = actionFactories[i]()
                                  ?? throw new InvalidOperationException(
                                      "An action factory returned null (block slot " + i + ").");
                }

                var guardFactories = _definition.GuardFactories;
                for (int i = 0; i < guardFactories.Length; i++)
                {
                    _guards[i] = guardFactories[i]()
                                 ?? throw new InvalidOperationException(
                                     "A guard factory returned null (guard slot " + i + ").");
                }
            }
            catch
            {
                // Release whatever was already created without masking the original failure.
                try
                {
                    DisposeBlocks();
                }
                catch (Exception)
                {
                    // Ignored: the construction failure is the meaningful one.
                }

                throw;
            }
        }

        private void DisposeBlocks()
        {
            Exception first = null;

            for (int i = 0; i < _actions.Length; i++)
            {
                first = DisposeSlot(_actions[i], first);
                _actions[i] = null;
            }

            for (int i = 0; i < _guards.Length; i++)
            {
                first = DisposeSlot(_guards[i], first);
                _guards[i] = null;
            }

            if (first != null)
            {
                throw first;
            }
        }

        private static Exception DisposeSlot(object instance, Exception first)
        {
            if (!(instance is IDisposable disposable))
            {
                return first;
            }

            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                return first ?? exception;
            }

            return first;
        }

        private void StopCore()
        {
            _stopping = true;
            _executing++;
            try
            {
                while (_activeDepth > 0)
                {
                    ExitState(_activePath[_activeDepth - 1].Index);
                }
            }
            catch (Exception exception)
            {
                _executing--;
                _stopping = false;
                _activeDepth = 0;
                Fault(exception);
                throw;
            }

            _executing--;
            _stopping = false;
            _activeDepth = 0;
            _queue.Clear();
            _lifecycle = FyniteLifecycle.Stopped;
        }

        private void RunPhaseOverActivePath(FynitePhase phase, float deltaTime)
        {
            _executing++;
            try
            {
                // The active path cannot change here: transitions only happen while pumping.
                for (int depth = 0; depth < _activeDepth; depth++)
                {
                    var state = _definition.States[_activePath[depth].Index];
                    var slots = phase == FynitePhase.Tick ? state.Tick : state.FixedTick;
                    RunActions(slots, phase, deltaTime, state.Handle);
                }
            }
            catch (Exception exception)
            {
                _executing--;
                Fault(exception);
                throw;
            }

            _executing--;
        }

        private void RaiseCore(SignalHandle signal, object payload, bool payloadSupplied)
        {
            if (_lifecycle == FyniteLifecycle.Disposed)
            {
                throw new ObjectDisposedException(nameof(FyniteMachine<TContext>));
            }

            if (_lifecycle == FyniteLifecycle.Faulted)
            {
                throw new InvalidOperationException(
                    "The machine has faulted and cannot accept signals; dispose it and create a new one.");
            }

            int index = _definition.RequireSignal(signal, nameof(signal));
            var declared = _definition.Signals[index];

            if (declared.PayloadType == null)
            {
                if (payloadSupplied && payload != null)
                {
                    throw new ArgumentException(
                        "Signal '" + declared.Name + "' was declared without a payload but one was supplied.",
                        nameof(payload));
                }
            }
            else if (payload == null)
            {
                throw new ArgumentException(
                    "Signal '" + declared.Name + "' requires a payload of type '" + declared.PayloadType.FullName + "'.",
                    nameof(payload));
            }
            else if (!declared.PayloadType.IsInstanceOfType(payload))
            {
                throw new ArgumentException(
                    "Signal '" + declared.Name + "' expects a payload of type '" + declared.PayloadType.FullName +
                    "' but received '" + payload.GetType().FullName + "'.",
                    nameof(payload));
            }

            if (_lifecycle != FyniteLifecycle.Running || _stopping)
            {
                // Nothing can react while there is no active path, so the signal is dropped rather
                // than kept for a later run. Fynite never buffers input implicitly.
                return;
            }

            var occurrence = new FyniteSignal(signal, payload);
            _queue.Enqueue(occurrence);
            _diagnostics.SignalQueued(new FyniteSignalEvent(_definition, occurrence, _queue.Count));

            if (_executing == 0)
            {
                Pump();
            }
        }

        private void Pump()
        {
            if (_executing > 0 || _lifecycle != FyniteLifecycle.Running)
            {
                return;
            }

            _executing++;
            int microsteps = 0;

            try
            {
                while (_lifecycle == FyniteLifecycle.Running && _queue.TryDequeue(out var signal))
                {
                    _diagnostics.SignalDispatchStarted(new FyniteSignalEvent(_definition, signal, _queue.Count));

                    var reaction = Resolve(signal);
                    if (reaction == null)
                    {
                        _diagnostics.SignalUnhandled(new FyniteSignalEvent(_definition, signal, _queue.Count));
                        continue;
                    }

                    microsteps++;
                    if (microsteps > _maxMicrosteps)
                    {
                        _diagnostics.MicrostepLimitExceeded(
                            new FyniteMicrostepEvent(_definition, microsteps, _maxMicrosteps, signal));
                        _queue.Clear();
                        throw new FyniteCycleException(
                            "Signal processing exceeded " + _maxMicrosteps +
                            " microsteps in a single pump while handling signal '" +
                            _definition.GetSignalName(signal.Handle) +
                            "'. Enter blocks or effects are raising signals that keep re-triggering transitions.",
                            microsteps,
                            _maxMicrosteps);
                    }

                    ExecuteReaction(reaction, signal);
                }
            }
            catch (Exception exception)
            {
                _executing--;
                ClearCurrentReaction();
                if (_lifecycle != FyniteLifecycle.Faulted)
                {
                    Fault(exception);
                }

                throw;
            }

            _executing--;
        }

        private ReactionRuntime Resolve(in FyniteSignal signal)
        {
            var states = _definition.States;
            var reactions = _definition.Reactions;
            int signalIndex = signal.Handle.Index;

            for (int depth = 0; depth < _activeDepth; depth++)
            {
                var candidates = states[_activePath[depth].Index].ReactionsBySignal;
                if (candidates != null && candidates.TryGetValue(signalIndex, out var range))
                {
                    _cursors[depth] = range.Start;
                    _cursorEnds[depth] = range.Start + range.Count;
                }
                else
                {
                    _cursors[depth] = 0;
                    _cursorEnds[depth] = 0;
                }
            }

            while (true)
            {
                int bestDepth = -1;
                ReactionRuntime best = null;

                for (int depth = 0; depth < _activeDepth; depth++)
                {
                    if (_cursors[depth] >= _cursorEnds[depth])
                    {
                        continue;
                    }

                    var candidate = reactions[_cursors[depth]];
                    if (best == null || Wins(candidate, depth, best, bestDepth))
                    {
                        best = candidate;
                        bestDepth = depth;
                    }
                }

                if (best == null)
                {
                    return null;
                }

                if (GuardsPass(best, signal))
                {
                    return best;
                }

                _cursors[bestDepth]++;
            }
        }

        /// <summary>Higher priority wins; then the deeper source; then the earlier registration.</summary>
        private static bool Wins(ReactionRuntime candidate, int candidateDepth, ReactionRuntime best, int bestDepth)
        {
            if (candidate.Priority != best.Priority)
            {
                return candidate.Priority > best.Priority;
            }

            if (candidateDepth != bestDepth)
            {
                return candidateDepth > bestDepth;
            }

            return candidate.RegistrationOrder < best.RegistrationOrder;
        }

        private bool GuardsPass(ReactionRuntime reaction, in FyniteSignal signal)
        {
            if (reaction.Guards.Count == 0)
            {
                return true;
            }

            var execution = new FyniteExecution(
                FynitePhase.Guard,
                0f,
                reaction.SourceHandle,
                reaction.SourceHandle,
                reaction.TargetHandle,
                signal,
                this);

            for (int i = 0; i < reaction.Guards.Count; i++)
            {
                if (!_guards[reaction.Guards.Start + i].Evaluate(_context, in execution))
                {
                    _diagnostics.ReactionRejected(new FyniteReactionEvent(
                        _definition,
                        signal,
                        reaction.SourceHandle,
                        reaction.TargetHandle,
                        reaction.Priority,
                        reaction.SourceDepth,
                        reaction.RegistrationOrder,
                        i));
                    return false;
                }
            }

            return true;
        }

        private void ExecuteReaction(ReactionRuntime reaction, in FyniteSignal signal)
        {
            _currentSignal = signal;
            _currentSource = reaction.SourceHandle;
            _currentTarget = reaction.TargetHandle;

            _diagnostics.ReactionSelected(new FyniteReactionEvent(
                _definition,
                signal,
                reaction.SourceHandle,
                reaction.TargetHandle,
                reaction.Priority,
                reaction.SourceDepth,
                reaction.RegistrationOrder,
                -1));

            if (!reaction.IsTransition)
            {
                RunActions(reaction.Effects, FynitePhase.Effect, 0f, reaction.SourceHandle);
                ClearCurrentReaction();
                return;
            }

            var states = _definition.States;

            // A self-transition must restart its state, so the preserved boundary is the parent of the
            // source rather than the source itself.
            int lca = reaction.Source == reaction.Target
                ? states[reaction.Source].Parent
                : LeastCommonAncestor(reaction.Source, reaction.Target);
            int lcaDepth = lca < 0 ? -1 : states[lca].Depth;

            _diagnostics.TransitionStarted(new FyniteTransitionEvent(
                _definition,
                signal,
                reaction.SourceHandle,
                reaction.TargetHandle,
                lca < 0 ? StateHandle.Invalid : states[lca].Handle));

            while (_activeDepth - 1 > lcaDepth)
            {
                ExitState(_activePath[_activeDepth - 1].Index);
            }

            RunActions(reaction.Effects, FynitePhase.Effect, 0f, reaction.SourceHandle);

            var targetPath = states[reaction.Target].AncestorPath;
            for (int depth = lcaDepth + 1; depth < targetPath.Length; depth++)
            {
                EnterState(targetPath[depth]);
            }

            int current = reaction.Target;
            while (states[current].InitialChild >= 0)
            {
                current = states[current].InitialChild;
                EnterState(current);
            }

            _diagnostics.TransitionCompleted(new FyniteTransitionEvent(
                _definition,
                signal,
                reaction.SourceHandle,
                reaction.TargetHandle,
                lca < 0 ? StateHandle.Invalid : states[lca].Handle));

            ClearCurrentReaction();
        }

        /// <summary>Walks both branches up to their common ancestor; proportional to tree depth, never allocates.</summary>
        private int LeastCommonAncestor(int a, int b)
        {
            var states = _definition.States;

            while (states[a].Depth > states[b].Depth)
            {
                a = states[a].Parent;
            }

            while (states[b].Depth > states[a].Depth)
            {
                b = states[b].Parent;
            }

            while (a != b)
            {
                a = states[a].Parent;
                b = states[b].Parent;
            }

            return a;
        }

        private void EnterState(int index)
        {
            var state = _definition.States[index];
            _activePath[_activeDepth] = state.Handle;
            _activeDepth++;

            _diagnostics.StateEntered(new FyniteStateEvent(_definition, state.Handle, state.Depth));
            RunActions(state.Enter, FynitePhase.Enter, 0f, state.Handle);
        }

        private void ExitState(int index)
        {
            var state = _definition.States[index];
            RunActions(state.Exit, FynitePhase.Exit, 0f, state.Handle);

            _activeDepth--;
            _activePath[_activeDepth] = StateHandle.Invalid;
            _diagnostics.StateExited(new FyniteStateEvent(_definition, state.Handle, state.Depth));
        }

        private void RunActions(SlotRange slots, FynitePhase phase, float deltaTime, StateHandle state)
        {
            if (slots.Count == 0)
            {
                return;
            }

            var execution = new FyniteExecution(
                phase,
                deltaTime,
                state,
                _currentSource,
                _currentTarget,
                _currentSignal,
                this);

            for (int i = 0; i < slots.Count; i++)
            {
                _actions[slots.Start + i].Execute(_context, in execution);
            }
        }

        private void ClearCurrentReaction()
        {
            _currentSignal = default;
            _currentSource = StateHandle.Invalid;
            _currentTarget = StateHandle.Invalid;
        }

        private void Fault(Exception exception)
        {
            _lifecycle = FyniteLifecycle.Faulted;
            _queue.Clear();
            ClearCurrentReaction();
            _diagnostics.MachineFaulted(new FyniteFaultEvent(
                _definition,
                FyniteLifecycle.Faulted,
                ActiveLeaf,
                exception));
        }

        private void RequireUsable(string operation)
        {
            switch (_lifecycle)
            {
                case FyniteLifecycle.Disposed:
                    throw new ObjectDisposedException(nameof(FyniteMachine<TContext>));
                case FyniteLifecycle.Faulted:
                    throw new InvalidOperationException(
                        "The machine has faulted and cannot " + operation + "; dispose it and create a new one.");
            }
        }

        private void RequireRunning(string operation)
        {
            RequireUsable(operation);

            if (_lifecycle != FyniteLifecycle.Running)
            {
                throw new InvalidOperationException(
                    operation + " requires a running machine, but its lifecycle is " + _lifecycle + ".");
            }
        }

        private void RequireNotExecuting(string operation)
        {
            if (_executing > 0)
            {
                throw new InvalidOperationException(
                    operation + " cannot be called from inside a block, guard or effect. Raise a signal instead.");
            }
        }
    }
}
