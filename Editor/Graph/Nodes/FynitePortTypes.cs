namespace Fynite.GraphEditor
{
    /// <summary>
    /// Marker types that give each kind of connection its own port data type.
    /// </summary>
    /// <remarks>
    /// Graph Toolkit refuses to connect ports whose data types differ, so giving hierarchy, reaction and
    /// signal wiring separate marker types makes whole classes of invalid graph structurally impossible
    /// to draw — the user simply cannot drop a signal wire onto a parent port. Nothing is ever
    /// instantiated; only the type identity matters.
    /// </remarks>
    public static class FynitePortTypes
    {
        /// <summary>Connects a parent state's children port to a child state's parent port.</summary>
        public sealed class Hierarchy
        {
            private Hierarchy()
            {
            }
        }

        /// <summary>Connects a state's reactions port to a reaction's source port.</summary>
        public sealed class ReactionSource
        {
            private ReactionSource()
            {
            }
        }

        /// <summary>Connects a state's incoming port to a reaction's target port.</summary>
        public sealed class TransitionTarget
        {
            private TransitionTarget()
            {
            }
        }

        /// <summary>Connects a signal's output port to a reaction's signal port.</summary>
        public sealed class Signal
        {
            private Signal()
            {
            }
        }
    }
}
