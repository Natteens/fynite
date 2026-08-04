using System.Collections.Generic;
using Fynite;

namespace FyniteEditor
{
    /// <summary>
    /// What the debugger window knows: which machines it found last time it looked, and which one is
    /// selected. Separated from the window so the behaviour can be tested without drawing anything.
    /// </summary>
    internal sealed class FyniteDebuggerModel
    {
        private readonly List<IFyniteDebugView> views = new List<IFyniteDebugView>();

        /// <summary>Grows to the busiest refresh so far and is then reused; never trimmed while open.</summary>
        private readonly List<FyniteDebuggerEntry> entries = new List<FyniteDebuggerEntry>();

        private int count;
        private int selected = -1;

        /// <summary>How many machines the last refresh found.</summary>
        internal int Count => count;

        internal FyniteDebuggerEntry GetEntry(int index) => entries[index];

        internal FyniteDebuggerEntry Selected => selected >= 0 ? entries[selected] : null;

        internal int SelectedIndex => selected;

        /// <summary>
        /// Looks at the loop again. The selection follows the machine it was on, by reference, and is
        /// dropped rather than moved elsewhere when that machine is gone.
        /// </summary>
        internal void Refresh()
        {
            var previous = Selected?.View;

            FyniteLoop.CollectDebugViews(views);

            count = 0;

            for (var i = 0; i < views.Count; i++)
            {
                var entry = Reserve(count);

                if (entry.Capture(views[i]))
                {
                    count++;
                }
                else
                {
                    entry.Reset();
                }
            }

            // Whatever the collection left behind must not keep a machine alive.
            for (var i = count; i < entries.Count; i++)
            {
                entries[i].Reset();
            }

            views.Clear();

            selected = IndexOf(previous);
        }

        internal void Select(int index) => selected = index >= 0 && index < count ? index : -1;

        /// <summary>Forgets everything, including every reference to a machine or an owner.</summary>
        internal void Clear()
        {
            for (var i = 0; i < entries.Count; i++)
            {
                entries[i].Reset();
            }

            views.Clear();
            count = 0;
            selected = -1;
        }

        private FyniteDebuggerEntry Reserve(int index)
        {
            while (entries.Count <= index)
            {
                entries.Add(new FyniteDebuggerEntry());
            }

            return entries[index];
        }

        private int IndexOf(IFyniteDebugView view)
        {
            if (view == null)
            {
                return -1;
            }

            for (var i = 0; i < count; i++)
            {
                if (ReferenceEquals(entries[i].View, view))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
