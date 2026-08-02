namespace Fynite.Diagnostics
{
    /// <summary>
    /// The default sink: every callback is empty, so the JIT can inline it away and no machine
    /// allocates or logs by default.
    /// </summary>
    public sealed class NullFyniteDiagnostics : IFyniteDiagnostics
    {
        /// <summary>Shared stateless instance.</summary>
        public static readonly NullFyniteDiagnostics Instance = new NullFyniteDiagnostics();

        private NullFyniteDiagnostics()
        {
        }

        public void StateEntered(in FyniteStateEvent evt)
        {
        }

        public void StateExited(in FyniteStateEvent evt)
        {
        }

        public void SignalQueued(in FyniteSignalEvent evt)
        {
        }

        public void SignalDispatchStarted(in FyniteSignalEvent evt)
        {
        }

        public void SignalUnhandled(in FyniteSignalEvent evt)
        {
        }

        public void ReactionRejected(in FyniteReactionEvent evt)
        {
        }

        public void ReactionSelected(in FyniteReactionEvent evt)
        {
        }

        public void TransitionStarted(in FyniteTransitionEvent evt)
        {
        }

        public void TransitionCompleted(in FyniteTransitionEvent evt)
        {
        }

        public void MicrostepLimitExceeded(in FyniteMicrostepEvent evt)
        {
        }

        public void MachineFaulted(in FyniteFaultEvent evt)
        {
        }
    }
}
