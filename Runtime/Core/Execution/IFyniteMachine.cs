using System;

namespace Fynite
{
    /// <summary>
    /// Context-agnostic view of a running machine, used by hosts (such as the Unity runner) that must
    /// drive a machine without knowing its context type.
    /// </summary>
    public interface IFyniteMachine : IDisposable
    {
        /// <summary>Current lifecycle value.</summary>
        FyniteLifecycle Lifecycle { get; }

        /// <summary>True while the machine has an active path.</summary>
        bool IsRunning { get; }

        /// <summary>Structure this machine executes.</summary>
        IFyniteDefinition Definition { get; }

        /// <summary>Deepest active state, or an invalid handle while not running.</summary>
        StateHandle ActiveLeaf { get; }

        /// <summary>Number of states on the active path; zero while not running.</summary>
        int ActiveDepth { get; }

        /// <summary>Active state at a given depth; the root is depth zero.</summary>
        StateHandle GetActiveState(int depth);

        /// <summary>True when the state is on the active path.</summary>
        bool IsActive(StateHandle state);

        /// <summary>Enters the root and follows the initial chain down to a leaf.</summary>
        void Start();

        /// <summary>Exits the whole active path from leaf to root and clears the queue.</summary>
        void Stop();

        /// <summary>Drains pending signals, runs tick blocks from root to leaf, drains again.</summary>
        void Tick(float deltaTime);

        /// <summary>Drains pending signals, runs fixed-tick blocks from root to leaf, drains again.</summary>
        void FixedTick(float fixedDeltaTime);

        /// <summary>Queues a signal declared without a payload.</summary>
        void Raise(SignalHandle signal);

        /// <summary>Queues a signal together with a payload; the payload type is validated.</summary>
        void Raise(SignalHandle signal, object payload);
    }
}
