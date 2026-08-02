using System;
using System.Collections.Generic;

namespace Fynite.Tests
{
    /// <summary>Narrow contract the test blocks depend on, so every test also exercises contravariance.</summary>
    internal interface ITraceContext
    {
        void Record(string entry);
    }

    /// <summary>Concrete context: an ordered log of everything the blocks did.</summary>
    internal sealed class TraceContext : ITraceContext
    {
        internal List<string> Log { get; } = new List<string>();

        public void Record(string entry) => Log.Add(entry);

        internal string Trace => string.Join(",", Log);

        internal void Clear() => Log.Clear();
    }

    /// <summary>Appends a fixed label. Declared against the narrow contract on purpose.</summary>
    internal sealed class TraceAction : IFyniteAction<ITraceContext>
    {
        private readonly string _label;

        internal TraceAction(string label) => _label = label;

        public void Execute(ITraceContext context, in FyniteExecution execution) => context.Record(_label);
    }

    /// <summary>Appends a label together with the phase it ran in.</summary>
    internal sealed class PhaseAction : IFyniteAction<ITraceContext>
    {
        private readonly string _label;

        internal PhaseAction(string label) => _label = label;

        public void Execute(ITraceContext context, in FyniteExecution execution) =>
            context.Record(execution.Phase + ":" + _label);
    }

    /// <summary>Records the delta time it was given, so Tick and FixedTick can be told apart.</summary>
    internal sealed class DeltaAction : IFyniteAction<ITraceContext>
    {
        private readonly string _label;

        internal DeltaAction(string label) => _label = label;

        public void Execute(ITraceContext context, in FyniteExecution execution) =>
            context.Record(_label + "@" + execution.DeltaTime.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Logs, then raises another signal through the sink.</summary>
    internal sealed class RaiseAction : IFyniteAction<ITraceContext>
    {
        private readonly string _label;
        private readonly SignalHandle _signal;

        internal RaiseAction(string label, SignalHandle signal)
        {
            _label = label;
            _signal = signal;
        }

        public void Execute(ITraceContext context, in FyniteExecution execution)
        {
            context.Record(_label);
            execution.Signals.Raise(_signal);
        }
    }

    /// <summary>Holds mutable per-instance state, used to prove instances are not shared.</summary>
    internal sealed class CountingAction : IFyniteAction<ITraceContext>
    {
        private readonly string _label;
        private int _count;

        internal CountingAction(string label) => _label = label;

        public void Execute(ITraceContext context, in FyniteExecution execution)
        {
            _count++;
            context.Record(_label + ":" + _count);
        }
    }

    /// <summary>Reports the signal payload it observed.</summary>
    internal sealed class PayloadAction : IFyniteAction<ITraceContext>
    {
        private readonly string _label;

        internal PayloadAction(string label) => _label = label;

        public void Execute(ITraceContext context, in FyniteExecution execution)
        {
            context.Record(execution.Signal.TryGetPayload<int>(out int value)
                ? _label + ":" + value
                : _label + ":none");
        }
    }

    /// <summary>Records the execution data a reaction hands to its blocks.</summary>
    internal sealed class ExecutionProbeAction : IFyniteAction<ITraceContext>
    {
        public FyniteExecution Last { get; private set; }

        public void Execute(ITraceContext context, in FyniteExecution execution)
        {
            Last = execution;
            context.Record("probe");
        }
    }

    /// <summary>Throws when executed, to exercise fault propagation.</summary>
    internal sealed class ThrowingAction : IFyniteAction<ITraceContext>
    {
        private readonly string _message;

        internal ThrowingAction(string message) => _message = message;

        public void Execute(ITraceContext context, in FyniteExecution execution) =>
            throw new InvalidOperationException(_message);
    }

    /// <summary>Counts its own disposals in a shared tally.</summary>
    internal sealed class DisposableAction : IFyniteAction<ITraceContext>, IDisposable
    {
        private readonly DisposalTally _tally;
        private readonly string _label;

        internal DisposableAction(DisposalTally tally, string label)
        {
            _tally = tally;
            _label = label;
        }

        public void Execute(ITraceContext context, in FyniteExecution execution) => context.Record(_label);

        public void Dispose() => _tally.Record(_label);
    }

    /// <summary>Shared disposal ledger for the block-lifetime tests.</summary>
    internal sealed class DisposalTally
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        internal int Total { get; private set; }

        internal void Record(string label)
        {
            _counts.TryGetValue(label, out int count);
            _counts[label] = count + 1;
            Total++;
        }

        internal int CountOf(string label)
        {
            _counts.TryGetValue(label, out int count);
            return count;
        }
    }

    /// <summary>Returns a constant verdict and logs that it was consulted.</summary>
    internal sealed class ConstantGuard : IFyniteGuard<ITraceContext>
    {
        private readonly bool _result;
        private readonly string _label;

        internal ConstantGuard(bool result, string label)
        {
            _result = result;
            _label = label;
        }

        public bool Evaluate(ITraceContext context, in FyniteExecution execution)
        {
            context.Record("guard:" + _label + "=" + _result);
            return _result;
        }
    }

    /// <summary>Verdict controlled from the test body.</summary>
    internal sealed class SwitchableGuard : IFyniteGuard<ITraceContext>
    {
        private readonly GuardSwitch _guardSwitch;
        private readonly string _label;

        internal SwitchableGuard(GuardSwitch guardSwitch, string label)
        {
            _guardSwitch = guardSwitch;
            _label = label;
        }

        public bool Evaluate(ITraceContext context, in FyniteExecution execution)
        {
            context.Record("guard:" + _label + "=" + _guardSwitch.Allow);
            return _guardSwitch.Allow;
        }
    }

    /// <summary>Mutable flag shared with a <see cref="SwitchableGuard"/>.</summary>
    internal sealed class GuardSwitch
    {
        internal bool Allow { get; set; }
    }

    /// <summary>Captures what a guard was given.</summary>
    internal sealed class ProbeGuard : IFyniteGuard<ITraceContext>
    {
        internal object SeenContext { get; private set; }

        internal SignalHandle SeenSignal { get; private set; }

        internal StateHandle SeenSource { get; private set; }

        internal StateHandle SeenTarget { get; private set; }

        internal FynitePhase SeenPhase { get; private set; }

        public bool Evaluate(ITraceContext context, in FyniteExecution execution)
        {
            SeenContext = context;
            SeenSignal = execution.Signal.Handle;
            SeenSource = execution.Source;
            SeenTarget = execution.Target;
            SeenPhase = execution.Phase;
            return true;
        }
    }

    /// <summary>Throws when evaluated.</summary>
    internal sealed class ThrowingGuard : IFyniteGuard<ITraceContext>
    {
        private readonly string _message;

        internal ThrowingGuard(string message) => _message = message;

        public bool Evaluate(ITraceContext context, in FyniteExecution execution) =>
            throw new InvalidOperationException(_message);
    }

    /// <summary>Guard that never runs; used to prove short-circuiting.</summary>
    internal sealed class DisposableGuard : IFyniteGuard<ITraceContext>, IDisposable
    {
        private readonly DisposalTally _tally;
        private readonly string _label;

        internal DisposableGuard(DisposalTally tally, string label)
        {
            _tally = tally;
            _label = label;
        }

        public bool Evaluate(ITraceContext context, in FyniteExecution execution) => true;

        public void Dispose() => _tally.Record(_label);
    }
}
