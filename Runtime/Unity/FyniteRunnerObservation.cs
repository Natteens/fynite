using System;
using Fynite.Diagnostics;

namespace Fynite
{
    /// <summary>
    /// Records just enough of a machine's activity for the graph editor to show what is happening,
    /// without the editor ever touching the running machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is an <see cref="IFyniteDiagnostics"/> observer, so it sees events as they happen and never
    /// polls. It keeps only the latest occurrence of each kind — no history buffer, no allocation per
    /// event, nothing resolved by name or GUID. Handles are stored raw; turning them into node
    /// highlights is the editor's job and happens only when someone is looking.
    /// </para>
    /// <para>
    /// The runner installs one of these only in the editor and in development builds; a release player
    /// compiles the whole thing out.
    /// </para>
    /// </remarks>
    public sealed class FyniteRunnerObservation : IFyniteDiagnostics
    {
        private readonly IFyniteDiagnostics _inner;

        /// <summary>Wraps another sink, forwarding everything to it after recording.</summary>
        /// <param name="inner">Sink to forward to, or null to only record.</param>
        public FyniteRunnerObservation(IFyniteDiagnostics inner = null)
        {
            _inner = inner ?? NullFyniteDiagnostics.Instance;
        }

        /// <summary>Incremented on every recorded event, so a viewer can tell whether anything changed.</summary>
        public int Version { get; private set; }

        /// <summary>The last signal that began dispatching.</summary>
        public SignalHandle LastSignal { get; private set; }

        /// <summary>True when the last dispatched signal found no reaction.</summary>
        public bool LastSignalUnhandled { get; private set; }

        /// <summary>Source state of the last selected reaction.</summary>
        public StateHandle LastReactionSource { get; private set; }

        /// <summary>Target of the last selected reaction; invalid for an effect-only reaction.</summary>
        public StateHandle LastReactionTarget { get; private set; }

        /// <summary>Signal of the last selected reaction.</summary>
        public SignalHandle LastReactionSignal { get; private set; }

        /// <summary>True when a reaction has been selected at least once.</summary>
        public bool HasReaction { get; private set; }

        /// <summary>Source of the last completed transition.</summary>
        public StateHandle LastTransitionSource { get; private set; }

        /// <summary>Target of the last completed transition.</summary>
        public StateHandle LastTransitionTarget { get; private set; }

        /// <summary>Deepest state preserved by the last completed transition.</summary>
        public StateHandle LastTransitionLca { get; private set; }

        /// <summary>True when a transition has completed at least once.</summary>
        public bool HasTransition { get; private set; }

        /// <summary>The exception that faulted the machine, or null.</summary>
        public Exception Fault { get; private set; }

        /// <summary>State the machine was in when it faulted.</summary>
        public StateHandle FaultState { get; private set; }

        /// <summary>Forgets everything recorded so far.</summary>
        public void Reset()
        {
            LastSignal = SignalHandle.Invalid;
            LastSignalUnhandled = false;
            LastReactionSource = StateHandle.Invalid;
            LastReactionTarget = StateHandle.Invalid;
            LastReactionSignal = SignalHandle.Invalid;
            HasReaction = false;
            LastTransitionSource = StateHandle.Invalid;
            LastTransitionTarget = StateHandle.Invalid;
            LastTransitionLca = StateHandle.Invalid;
            HasTransition = false;
            Fault = null;
            FaultState = StateHandle.Invalid;
            Version++;
        }

        /// <inheritdoc />
        public void StateEntered(in FyniteStateEvent evt)
        {
            Version++;
            _inner.StateEntered(evt);
        }

        /// <inheritdoc />
        public void StateExited(in FyniteStateEvent evt)
        {
            Version++;
            _inner.StateExited(evt);
        }

        /// <inheritdoc />
        public void SignalQueued(in FyniteSignalEvent evt) => _inner.SignalQueued(evt);

        /// <inheritdoc />
        public void SignalDispatchStarted(in FyniteSignalEvent evt)
        {
            LastSignal = evt.Signal.Handle;
            LastSignalUnhandled = false;
            Version++;
            _inner.SignalDispatchStarted(evt);
        }

        /// <inheritdoc />
        public void SignalUnhandled(in FyniteSignalEvent evt)
        {
            LastSignal = evt.Signal.Handle;
            LastSignalUnhandled = true;
            Version++;
            _inner.SignalUnhandled(evt);
        }

        /// <inheritdoc />
        public void ReactionRejected(in FyniteReactionEvent evt) => _inner.ReactionRejected(evt);

        /// <inheritdoc />
        public void ReactionSelected(in FyniteReactionEvent evt)
        {
            LastReactionSource = evt.Source;
            LastReactionTarget = evt.Target;
            LastReactionSignal = evt.Signal.Handle;
            HasReaction = true;
            Version++;
            _inner.ReactionSelected(evt);
        }

        /// <inheritdoc />
        public void TransitionStarted(in FyniteTransitionEvent evt) => _inner.TransitionStarted(evt);

        /// <inheritdoc />
        public void TransitionCompleted(in FyniteTransitionEvent evt)
        {
            LastTransitionSource = evt.Source;
            LastTransitionTarget = evt.Target;
            LastTransitionLca = evt.LeastCommonAncestor;
            HasTransition = true;
            Version++;
            _inner.TransitionCompleted(evt);
        }

        /// <inheritdoc />
        public void MicrostepLimitExceeded(in FyniteMicrostepEvent evt) => _inner.MicrostepLimitExceeded(evt);

        /// <inheritdoc />
        public void MachineFaulted(in FyniteFaultEvent evt)
        {
            Fault = evt.Exception;
            FaultState = evt.State;
            Version++;
            _inner.MachineFaulted(evt);
        }
    }
}
