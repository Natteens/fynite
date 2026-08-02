using System;

namespace Fynite.Diagnostics
{
    /// <summary>A state was entered or exited.</summary>
    public readonly struct FyniteStateEvent
    {
        internal FyniteStateEvent(IFyniteDefinition definition, StateHandle state, int depth)
        {
            Definition = definition;
            State = state;
            Depth = depth;
        }

        /// <summary>Definition the handles belong to; use it to resolve diagnostic names.</summary>
        public IFyniteDefinition Definition { get; }

        /// <summary>The state that was entered or exited.</summary>
        public StateHandle State { get; }

        /// <summary>Depth of the state in the tree; the root is zero.</summary>
        public int Depth { get; }
    }

    /// <summary>A signal was queued, dispatched, or dropped as unhandled.</summary>
    public readonly struct FyniteSignalEvent
    {
        internal FyniteSignalEvent(IFyniteDefinition definition, in FyniteSignal signal, int queueLength)
        {
            Definition = definition;
            Signal = signal;
            QueueLength = queueLength;
        }

        /// <summary>Definition the handles belong to.</summary>
        public IFyniteDefinition Definition { get; }

        /// <summary>The signal occurrence.</summary>
        public FyniteSignal Signal { get; }

        /// <summary>Number of signals pending after the reported operation.</summary>
        public int QueueLength { get; }
    }

    /// <summary>A reaction was selected, or rejected by one of its guards.</summary>
    public readonly struct FyniteReactionEvent
    {
        internal FyniteReactionEvent(
            IFyniteDefinition definition,
            in FyniteSignal signal,
            StateHandle source,
            StateHandle target,
            int priority,
            int depth,
            int registrationOrder,
            int rejectedGuardIndex)
        {
            Definition = definition;
            Signal = signal;
            Source = source;
            Target = target;
            Priority = priority;
            Depth = depth;
            RegistrationOrder = registrationOrder;
            RejectedGuardIndex = rejectedGuardIndex;
        }

        /// <summary>Definition the handles belong to.</summary>
        public IFyniteDefinition Definition { get; }

        /// <summary>Signal that triggered the resolution.</summary>
        public FyniteSignal Signal { get; }

        /// <summary>State the reaction was declared on.</summary>
        public StateHandle Source { get; }

        /// <summary>Transition target, or an invalid handle for an effect-only reaction.</summary>
        public StateHandle Target { get; }

        /// <summary>Configured priority; higher wins.</summary>
        public int Priority { get; }

        /// <summary>Depth of <see cref="Source"/>; deeper wins on equal priority.</summary>
        public int Depth { get; }

        /// <summary>Global registration index; lower wins on equal priority and depth.</summary>
        public int RegistrationOrder { get; }

        /// <summary>Index of the guard that returned false, or -1 when the reaction was selected.</summary>
        public int RejectedGuardIndex { get; }

        /// <summary>True when the reaction changes the active path.</summary>
        public bool IsTransition => Target.IsValid;
    }

    /// <summary>A transition started or completed.</summary>
    public readonly struct FyniteTransitionEvent
    {
        internal FyniteTransitionEvent(
            IFyniteDefinition definition,
            in FyniteSignal signal,
            StateHandle source,
            StateHandle target,
            StateHandle leastCommonAncestor)
        {
            Definition = definition;
            Signal = signal;
            Source = source;
            Target = target;
            LeastCommonAncestor = leastCommonAncestor;
        }

        /// <summary>Definition the handles belong to.</summary>
        public IFyniteDefinition Definition { get; }

        /// <summary>Signal that caused the transition.</summary>
        public FyniteSignal Signal { get; }

        /// <summary>State the reaction was declared on.</summary>
        public StateHandle Source { get; }

        /// <summary>State the transition targets.</summary>
        public StateHandle Target { get; }

        /// <summary>
        /// Deepest ancestor preserved by the transition, or an invalid handle when the whole path
        /// including the root is re-entered.
        /// </summary>
        public StateHandle LeastCommonAncestor { get; }
    }

    /// <summary>The microstep budget for a single queue pump was exhausted.</summary>
    public readonly struct FyniteMicrostepEvent
    {
        internal FyniteMicrostepEvent(IFyniteDefinition definition, int microsteps, int limit, in FyniteSignal signal)
        {
            Definition = definition;
            Microsteps = microsteps;
            Limit = limit;
            Signal = signal;
        }

        /// <summary>Definition the handles belong to.</summary>
        public IFyniteDefinition Definition { get; }

        /// <summary>Reactions executed in this pump.</summary>
        public int Microsteps { get; }

        /// <summary>Configured limit.</summary>
        public int Limit { get; }

        /// <summary>Signal being processed when the limit was hit.</summary>
        public FyniteSignal Signal { get; }
    }

    /// <summary>The machine transitioned to <see cref="FyniteLifecycle.Faulted"/>.</summary>
    public readonly struct FyniteFaultEvent
    {
        internal FyniteFaultEvent(
            IFyniteDefinition definition,
            FyniteLifecycle lifecycle,
            StateHandle state,
            Exception exception)
        {
            Definition = definition;
            Lifecycle = lifecycle;
            State = state;
            Exception = exception;
        }

        /// <summary>Definition the handles belong to.</summary>
        public IFyniteDefinition Definition { get; }

        /// <summary>Lifecycle value after the fault.</summary>
        public FyniteLifecycle Lifecycle { get; }

        /// <summary>Deepest active state when the fault happened, when there was one.</summary>
        public StateHandle State { get; }

        /// <summary>The exception that is being propagated to the caller.</summary>
        public Exception Exception { get; }
    }
}
