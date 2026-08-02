using UnityEngine;

namespace Fynite.Diagnostics
{
    /// <summary>
    /// Opt-in adapter that writes machine diagnostics to the Unity console.
    /// </summary>
    /// <remarks>
    /// Verbose by design and never installed automatically: enable it on a single
    /// <see cref="FyniteRunner"/> while investigating a specific machine.
    /// </remarks>
    public sealed class FyniteConsoleDiagnostics : IFyniteDiagnostics
    {
        private readonly Object _owner;
        private readonly string _prefix;

        /// <summary>Creates an adapter whose messages point back at <paramref name="owner"/> when clicked.</summary>
        /// <param name="owner">Object to associate log entries with; may be null.</param>
        /// <param name="label">Short label prepended to every message.</param>
        public FyniteConsoleDiagnostics(Object owner = null, string label = "Fynite")
        {
            _owner = owner;
            _prefix = "[" + label + "] ";
        }

        public void StateEntered(in FyniteStateEvent evt) =>
            Log("enter " + Name(evt.Definition, evt.State) + " (depth " + evt.Depth + ")");

        public void StateExited(in FyniteStateEvent evt) =>
            Log("exit " + Name(evt.Definition, evt.State) + " (depth " + evt.Depth + ")");

        public void SignalQueued(in FyniteSignalEvent evt) =>
            Log("queued " + evt.Definition.GetSignalName(evt.Signal.Handle) + " (pending " + evt.QueueLength + ")");

        public void SignalDispatchStarted(in FyniteSignalEvent evt) =>
            Log("dispatch " + evt.Definition.GetSignalName(evt.Signal.Handle));

        public void SignalUnhandled(in FyniteSignalEvent evt) =>
            Log("unhandled " + evt.Definition.GetSignalName(evt.Signal.Handle) + ", dropped");

        public void ReactionRejected(in FyniteReactionEvent evt) =>
            Log("reaction on " + Name(evt.Definition, evt.Source) + " for " +
                evt.Definition.GetSignalName(evt.Signal.Handle) + " rejected by guard " + evt.RejectedGuardIndex);

        public void ReactionSelected(in FyniteReactionEvent evt) =>
            Log("reaction on " + Name(evt.Definition, evt.Source) + " for " +
                evt.Definition.GetSignalName(evt.Signal.Handle) + " selected (priority " + evt.Priority +
                ", depth " + evt.Depth + ", order " + evt.RegistrationOrder + ")");

        public void TransitionStarted(in FyniteTransitionEvent evt) =>
            Log("transition " + Name(evt.Definition, evt.Source) + " → " + Name(evt.Definition, evt.Target) +
                " (lca " + Name(evt.Definition, evt.LeastCommonAncestor) + ")");

        public void TransitionCompleted(in FyniteTransitionEvent evt) =>
            Log("transition to " + Name(evt.Definition, evt.Target) + " completed");

        public void MicrostepLimitExceeded(in FyniteMicrostepEvent evt) =>
            Debug.LogError(
                _prefix + "microstep limit exceeded: " + evt.Microsteps + " of " + evt.Limit +
                " while handling " + evt.Definition.GetSignalName(evt.Signal.Handle),
                _owner);

        public void MachineFaulted(in FyniteFaultEvent evt) =>
            Debug.LogError(
                _prefix + "machine faulted at " + Name(evt.Definition, evt.State) + ": " + evt.Exception,
                _owner);

        private void Log(string message) => Debug.Log(_prefix + message, _owner);

        private static string Name(IFyniteDefinition definition, StateHandle state) =>
            state.IsValid ? definition.GetStateName(state) : "<none>";
    }
}
