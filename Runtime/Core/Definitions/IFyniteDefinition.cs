using System;

namespace Fynite
{
    /// <summary>
    /// Context-agnostic view of a compiled definition: structure and diagnostic names, no execution.
    /// </summary>
    /// <remarks>
    /// Diagnostics sinks, tooling and the Unity layer use this so they do not need to know
    /// <c>TContext</c>. Names are labels for humans; nothing is resolved by name at runtime.
    /// </remarks>
    public interface IFyniteDefinition
    {
        /// <summary>Number of states in the tree.</summary>
        int StateCount { get; }

        /// <summary>Number of declared signals.</summary>
        int SignalCount { get; }

        /// <summary>The single root state.</summary>
        StateHandle Root { get; }

        /// <summary>Number of levels in the tree; a root-only machine has depth 1.</summary>
        int MaxDepth { get; }

        /// <summary>The context type this definition executes against.</summary>
        Type ContextType { get; }

        /// <summary>True when the handle was produced by this definition's builder.</summary>
        bool Owns(StateHandle state);

        /// <summary>True when the handle was produced by this definition's builder.</summary>
        bool Owns(SignalHandle signal);

        /// <summary>Diagnostic label of a state.</summary>
        string GetStateName(StateHandle state);

        /// <summary>Diagnostic label of a signal.</summary>
        string GetSignalName(SignalHandle signal);

        /// <summary>Parent of a state, or an invalid handle for the root.</summary>
        StateHandle GetParent(StateHandle state);

        /// <summary>Initial child of a composite state, or an invalid handle for a leaf.</summary>
        StateHandle GetInitialChild(StateHandle state);

        /// <summary>Depth of a state; the root is zero.</summary>
        int GetDepth(StateHandle state);

        /// <summary>Number of direct children of a state.</summary>
        int GetChildCount(StateHandle state);

        /// <summary>Direct child of a state by index, in registration order.</summary>
        StateHandle GetChild(StateHandle state, int childIndex);

        /// <summary>Declared payload type of a signal, or <c>null</c> when it accepts no payload.</summary>
        Type GetPayloadType(SignalHandle signal);
    }
}
