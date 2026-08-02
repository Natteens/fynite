using System;
using System.Collections.Generic;
using Fynite.Diagnostics;

namespace Fynite.Tests
{
    /// <summary>Diagnostics sink that records every callback for assertions.</summary>
    internal sealed class RecordingDiagnostics : IFyniteDiagnostics
    {
        internal List<string> Events { get; } = new List<string>();

        internal List<FyniteReactionEvent> Rejections { get; } = new List<FyniteReactionEvent>();

        internal List<FyniteReactionEvent> Selections { get; } = new List<FyniteReactionEvent>();

        internal List<FyniteTransitionEvent> TransitionsStarted { get; } = new List<FyniteTransitionEvent>();

        internal List<FyniteSignalEvent> Unhandled { get; } = new List<FyniteSignalEvent>();

        internal List<FyniteMicrostepEvent> MicrostepOverflows { get; } = new List<FyniteMicrostepEvent>();

        internal List<Exception> Faults { get; } = new List<Exception>();

        internal bool Contains(string entry) => Events.Contains(entry);

        internal int CountOf(string entry)
        {
            int count = 0;
            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i] == entry)
                {
                    count++;
                }
            }

            return count;
        }

        public void StateEntered(in FyniteStateEvent evt) =>
            Events.Add("entered:" + evt.Definition.GetStateName(evt.State));

        public void StateExited(in FyniteStateEvent evt) =>
            Events.Add("exited:" + evt.Definition.GetStateName(evt.State));

        public void SignalQueued(in FyniteSignalEvent evt) =>
            Events.Add("queued:" + evt.Definition.GetSignalName(evt.Signal.Handle));

        public void SignalDispatchStarted(in FyniteSignalEvent evt) =>
            Events.Add("dispatch:" + evt.Definition.GetSignalName(evt.Signal.Handle));

        public void SignalUnhandled(in FyniteSignalEvent evt)
        {
            Events.Add("unhandled:" + evt.Definition.GetSignalName(evt.Signal.Handle));
            Unhandled.Add(evt);
        }

        public void ReactionRejected(in FyniteReactionEvent evt)
        {
            Events.Add("rejected:" + evt.Definition.GetStateName(evt.Source) + ":guard" + evt.RejectedGuardIndex);
            Rejections.Add(evt);
        }

        public void ReactionSelected(in FyniteReactionEvent evt)
        {
            Events.Add("selected:" + evt.Definition.GetStateName(evt.Source));
            Selections.Add(evt);
        }

        public void TransitionStarted(in FyniteTransitionEvent evt)
        {
            Events.Add("transition:" + evt.Definition.GetStateName(evt.Source) + "->" +
                       evt.Definition.GetStateName(evt.Target));
            TransitionsStarted.Add(evt);
        }

        public void TransitionCompleted(in FyniteTransitionEvent evt) =>
            Events.Add("completed:" + evt.Definition.GetStateName(evt.Target));

        public void MicrostepLimitExceeded(in FyniteMicrostepEvent evt)
        {
            Events.Add("microstep-limit:" + evt.Microsteps + "/" + evt.Limit);
            MicrostepOverflows.Add(evt);
        }

        public void MachineFaulted(in FyniteFaultEvent evt)
        {
            Events.Add("faulted:" + evt.Exception.GetType().Name);
            Faults.Add(evt.Exception);
        }
    }
}
