namespace Fynite.Diagnostics
{
    /// <summary>
    /// Structured observation of everything a machine does. Purely an observer.
    /// </summary>
    /// <remarks>
    /// Implementations must not mutate the machine and must not throw: the executor does not guard
    /// against either, so a misbehaving diagnostics sink will corrupt or fault the machine it observes.
    /// The core never logs by itself; <see cref="NullFyniteDiagnostics"/> is used when none is supplied.
    /// </remarks>
    public interface IFyniteDiagnostics
    {
        /// <summary>A state was pushed onto the active path, before its enter blocks run.</summary>
        void StateEntered(in FyniteStateEvent evt);

        /// <summary>A state was popped from the active path, after its exit blocks ran.</summary>
        void StateExited(in FyniteStateEvent evt);

        /// <summary>A signal was appended to the queue.</summary>
        void SignalQueued(in FyniteSignalEvent evt);

        /// <summary>A signal was dequeued and reaction resolution began.</summary>
        void SignalDispatchStarted(in FyniteSignalEvent evt);

        /// <summary>No valid reaction existed on the active path; the signal was dropped.</summary>
        void SignalUnhandled(in FyniteSignalEvent evt);

        /// <summary>A candidate reaction was discarded because one of its guards returned false.</summary>
        void ReactionRejected(in FyniteReactionEvent evt);

        /// <summary>A reaction won resolution and is about to run.</summary>
        void ReactionSelected(in FyniteReactionEvent evt);

        /// <summary>A transition began, before any exit block runs.</summary>
        void TransitionStarted(in FyniteTransitionEvent evt);

        /// <summary>A transition finished, after the target's initial chain was entered.</summary>
        void TransitionCompleted(in FyniteTransitionEvent evt);

        /// <summary>The queue pump exceeded its microstep budget and was aborted.</summary>
        void MicrostepLimitExceeded(in FyniteMicrostepEvent evt);

        /// <summary>The machine faulted; the exception is about to be rethrown to the caller.</summary>
        void MachineFaulted(in FyniteFaultEvent evt);
    }
}
