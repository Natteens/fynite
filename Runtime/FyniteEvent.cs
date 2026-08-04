using System;

namespace Fynite
{
    /// <summary>
    /// A one-off occurrence a machine can transition on, such as damage taken or an animation that
    /// finished. The system that owns the occurrence publishes it; transition modules point at the
    /// instance with <c>On(...)</c> and the machines subscribe and unsubscribe on their own.
    /// </summary>
    public sealed class FyniteEvent
    {
        private IFyniteEventSink[] sinks;
        private int[] slots;
        private int count;

        /// <summary>How many machines are listening. Exists for the tests of the package.</summary>
        internal int SubscriberCount => count;

        /// <summary>
        /// Records the occurrence on every machine listening to this event. It changes no state right
        /// away: each machine resolves the occurrence on its next safe update, so publishing from a
        /// state callback, a predicate or physics code is safe. Publishing with nobody listening does
        /// nothing.
        /// </summary>
        public void Publish()
        {
            for (var i = 0; i < count; i++)
            {
                sinks[i].Signal(slots[i]);
            }
        }

        internal void Subscribe(IFyniteEventSink sink, int slot)
        {
            if (sinks == null)
            {
                sinks = new IFyniteEventSink[2];
                slots = new int[2];
            }
            else if (count == sinks.Length)
            {
                var grownSinks = new IFyniteEventSink[count * 2];
                var grownSlots = new int[count * 2];

                Array.Copy(sinks, grownSinks, count);
                Array.Copy(slots, grownSlots, count);

                sinks = grownSinks;
                slots = grownSlots;
            }

            sinks[count] = sink;
            slots[count] = slot;
            count++;
        }

        internal void Unsubscribe(IFyniteEventSink sink)
        {
            for (var i = 0; i < count; i++)
            {
                if (!ReferenceEquals(sinks[i], sink))
                {
                    continue;
                }

                count--;
                sinks[i] = sinks[count];
                slots[i] = slots[count];
                sinks[count] = null;
                return;
            }
        }
    }
}
