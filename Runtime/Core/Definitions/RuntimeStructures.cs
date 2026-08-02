using System;
using System.Collections.Generic;

namespace Fynite
{
    /// <summary>Contiguous slice of the definition's flat reaction array.</summary>
    internal readonly struct ReactionRange
    {
        internal ReactionRange(int start, int count)
        {
            Start = start;
            Count = count;
        }

        internal int Start { get; }

        internal int Count { get; }
    }

    /// <summary>Contiguous slice of the definition's flat block-slot arrays.</summary>
    internal readonly struct SlotRange
    {
        internal SlotRange(int start, int count)
        {
            Start = start;
            Count = count;
        }

        internal int Start { get; }

        internal int Count { get; }
    }

    /// <summary>Baked, immutable data of a single state.</summary>
    internal sealed class StateRuntime
    {
        internal StateHandle Handle;
        internal string Name;
        internal int Index;
        internal int Parent;
        internal int InitialChild;
        internal int Depth;

        /// <summary>Indices from the root down to and including this state; <c>AncestorPath[d]</c> has depth <c>d</c>.</summary>
        internal int[] AncestorPath;

        internal int[] Children;

        internal SlotRange Enter;
        internal SlotRange Tick;
        internal SlotRange FixedTick;
        internal SlotRange Exit;

        /// <summary>Reaction slices keyed by signal index. Empty when the state declares no reaction.</summary>
        internal Dictionary<int, ReactionRange> ReactionsBySignal;

        internal bool IsComposite => InitialChild >= 0;
    }

    /// <summary>Baked, immutable data of a single reaction.</summary>
    internal sealed class ReactionRuntime
    {
        internal StateHandle SourceHandle;
        internal StateHandle TargetHandle;
        internal SignalHandle SignalHandle;

        internal int Source;
        internal int Signal;

        /// <summary>Target state index, or -1 for an effect-only reaction.</summary>
        internal int Target;

        internal int Priority;
        internal int RegistrationOrder;
        internal int SourceDepth;

        internal SlotRange Guards;
        internal SlotRange Effects;

        internal bool IsTransition => Target >= 0;
    }

    /// <summary>Baked, immutable data of a single signal.</summary>
    internal sealed class SignalRuntime
    {
        internal SignalHandle Handle;
        internal string Name;

        /// <summary>Declared payload type, or <c>null</c> when the signal accepts no payload.</summary>
        internal Type PayloadType;
    }
}
