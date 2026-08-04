using System;
using System.Collections.Generic;
using Fynite;
using Object = UnityEngine.Object;

namespace FyniteEditor
{
    /// <summary>
    /// One machine as the window last saw it. Reading a machine twice during a refresh could catch it
    /// mid-transition, so everything the UI draws is copied here once and then left alone.
    /// </summary>
    internal sealed class FyniteDebuggerEntry
    {
        internal const string DestroyedOwner = "<destroyed owner>";
        internal const string Stopped = "<stopped>";

        private readonly List<Type> path = new List<Type>();

        /// <summary>Identity of the entry. Only ever compared, never dereferenced by the UI.</summary>
        internal IFyniteDebugView View { get; private set; }

        internal Object Owner { get; private set; }

        internal string OwnerLabel { get; private set; } = DestroyedOwner;

        internal string ContextTypeName { get; private set; } = string.Empty;

        internal int PathCount => path.Count;

        internal Type GetPathState(int index) => path[index];

        internal Type Leaf => path.Count > 0 ? path[path.Count - 1] : null;

        internal string LeafLabel => Leaf != null ? Leaf.Name : Stopped;

        /// <summary>The one line the machine list shows.</summary>
        internal string ListLabel => OwnerLabel + " — " + LeafLabel;

        /// <summary>
        /// Takes a fresh reading of <paramref name="view"/>. Returns false when the machine stopped
        /// while it was being read, in which case the entry is not worth showing and the next refresh
        /// will simply not find it.
        /// </summary>
        internal bool Capture(IFyniteDebugView view)
        {
            Reset();
            View = view;

            if (!view.DebugIsRunning)
            {
                return false;
            }

            Owner = view.DebugOwner;
            OwnerLabel = Owner != null ? Owner.name : DestroyedOwner;

            var contextType = view.DebugContextType;
            ContextTypeName = contextType != null ? contextType.Name : string.Empty;

            var count = view.DebugActiveStateCount;

            for (var i = 0; i < count; i++)
            {
                Type state;

                try
                {
                    state = view.DebugGetActiveStateType(i);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // The machine stopped between the count and this read. Nothing is wrong; the entry
                    // is dropped and the next refresh reports whatever is true then.
                    return false;
                }

                path.Add(state);
            }

            return true;
        }

        /// <summary>Lets go of the machine and its owner, so the window never keeps either alive.</summary>
        internal void Reset()
        {
            View = null;
            Owner = null;
            OwnerLabel = DestroyedOwner;
            ContextTypeName = string.Empty;
            path.Clear();
        }
    }
}
