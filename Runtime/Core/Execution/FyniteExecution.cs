namespace Fynite
{
    /// <summary>
    /// Everything a block is allowed to know about the operation that is invoking it.
    /// Always passed by <c>in</c>; never allocates.
    /// </summary>
    public readonly struct FyniteExecution
    {
        private readonly FynitePhase _phase;
        private readonly float _deltaTime;
        private readonly StateHandle _state;
        private readonly StateHandle _source;
        private readonly StateHandle _target;
        private readonly FyniteSignal _signal;
        private readonly IFyniteSignalSink _signals;

        internal FyniteExecution(
            FynitePhase phase,
            float deltaTime,
            StateHandle state,
            StateHandle source,
            StateHandle target,
            in FyniteSignal signal,
            IFyniteSignalSink signals)
        {
            _phase = phase;
            _deltaTime = deltaTime;
            _state = state;
            _source = source;
            _target = target;
            _signal = signal;
            _signals = signals;
        }

        /// <summary>Which part of the cycle is running.</summary>
        public FynitePhase Phase => _phase;

        /// <summary>
        /// Elapsed time for <see cref="FynitePhase.Tick"/> and <see cref="FynitePhase.FixedTick"/>;
        /// zero for every structural phase.
        /// </summary>
        public float DeltaTime => _deltaTime;

        /// <summary>The state that owns the running block.</summary>
        public StateHandle State => _state;

        /// <summary>State the current reaction was declared on, when a reaction is being processed.</summary>
        public StateHandle Source => _source;

        /// <summary>Target of the current transition, when one is being processed.</summary>
        public StateHandle Target => _target;

        /// <summary>The signal being processed, when the current work was caused by one.</summary>
        public FyniteSignal Signal => _signal;

        /// <summary>True when <see cref="Signal"/> refers to an actual signal.</summary>
        public bool HasSignal => _signal.IsValid;

        /// <summary>
        /// The machine's signal sink. Raising from a block always queues; the signal is dispatched once
        /// the current operation has finished.
        /// </summary>
        /// <remarks>
        /// Raising during <see cref="FynitePhase.Guard"/> is rejected with an
        /// <see cref="System.InvalidOperationException"/> because guards must be pure predicates.
        /// See <see cref="IFyniteGuard{TContext}"/>.
        /// </remarks>
        public IFyniteSignalSink Signals => _signals;
    }
}
