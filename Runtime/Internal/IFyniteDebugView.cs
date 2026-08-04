#if UNITY_EDITOR
using System;

namespace Fynite
{
    /// <summary>
    /// What the Editor debugger is allowed to see of a machine: its owner, the shape of its context,
    /// and the active path it already publishes. Read only, internal, and compiled out of player
    /// builds entirely. Nothing here reaches a context instance, a state instance or a transition.
    /// </summary>
    internal interface IFyniteDebugView
    {
        UnityEngine.Object DebugOwner { get; }

        Type DebugContextType { get; }

        bool DebugIsRunning { get; }

        int DebugActiveStateCount { get; }

        Type DebugGetActiveStateType(int index);
    }
}
#endif
