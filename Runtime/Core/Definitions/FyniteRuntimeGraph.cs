using System;

namespace Fynite
{
    /// <summary>
    /// The compiler's output: a graph reduced to dense indices, with every authoring concept already
    /// resolved away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Authoring identity (GUIDs), visual layout and display metadata do not reach this type. What
    /// survives is the shape the builder needs: a topologically ordered state array, a signal array, a
    /// reaction array already in registration order, and indices into the block prototype table.
    /// </para>
    /// <para>
    /// Signal GUIDs are the one exception. They are kept so that a persistent
    /// <c>FyniteSignalReference</c> can be resolved to a dense <see cref="SignalHandle"/> once, when a
    /// component binds — never during tick or dispatch.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class FyniteRuntimeGraph
    {
        /// <summary>Persistent identity of the authoring graph this was compiled from.</summary>
        public string graphGuid;

        /// <summary>
        /// States ordered so that a parent always precedes its children, which is what lets the builder
        /// declare the tree in a single forward pass.
        /// </summary>
        public FyniteRuntimeState[] states = Array.Empty<FyniteRuntimeState>();

        /// <summary>Declared signals, in compilation order.</summary>
        public FyniteRuntimeSignal[] signals = Array.Empty<FyniteRuntimeSignal>();

        /// <summary>Reactions in the exact order they must be registered; the array order is the tie-break.</summary>
        public FyniteRuntimeReaction[] reactions = Array.Empty<FyniteRuntimeReaction>();

        /// <summary>Index of the root state in <see cref="states"/>.</summary>
        public int rootIndex = -1;
    }

    /// <summary>One compiled state. All indices refer to the containing <see cref="FyniteRuntimeGraph"/>.</summary>
    [Serializable]
    public struct FyniteRuntimeState
    {
        /// <summary>Diagnostic label.</summary>
        public string name;

        /// <summary>Index of the parent state, or -1 for the root.</summary>
        public int parent;

        /// <summary>Index of the initial child, or -1 for a leaf.</summary>
        public int initialChild;

        /// <summary>Indices into the block prototype table, in execution order.</summary>
        public int[] enter;

        /// <inheritdoc cref="enter" />
        public int[] tick;

        /// <inheritdoc cref="enter" />
        public int[] fixedTick;

        /// <inheritdoc cref="enter" />
        public int[] exit;
    }

    /// <summary>One compiled signal.</summary>
    [Serializable]
    public struct FyniteRuntimeSignal
    {
        /// <summary>Diagnostic label.</summary>
        public string name;

        /// <summary>Persistent authoring identity, used only to resolve external references once.</summary>
        public string guid;
    }

    /// <summary>One compiled reaction. All indices refer to the containing <see cref="FyniteRuntimeGraph"/>.</summary>
    [Serializable]
    public struct FyniteRuntimeReaction
    {
        /// <summary>Index of the state the reaction is declared on.</summary>
        public int source;

        /// <summary>Index of the signal that triggers it.</summary>
        public int signal;

        /// <summary>Index of the target state, or -1 for a reaction that only runs effects.</summary>
        public int target;

        /// <summary>Resolution priority; higher wins.</summary>
        public int priority;

        /// <summary>Indices into the block prototype table, in evaluation order.</summary>
        public int[] guards;

        /// <inheritdoc cref="guards" />
        public int[] effects;
    }
}
