using System;

namespace Fynite
{
    /// <summary>
    /// Where a machine's published events wait for a safe update. Owns the subscriptions and the ring
    /// buffer together, because they are one invariant: a source is subscribed exactly while the inbox
    /// is open, and it is waiting at most once at a time however often it is published.
    /// </summary>
    internal sealed class FyniteEventInbox : IFyniteEventSink
    {
        private readonly FyniteEvent[] sources;
        private readonly int[] queue;
        private readonly bool[] waiting;

        private int head;
        private int count;
        private bool open;

        internal FyniteEventInbox(FyniteEvent[] sources)
        {
            this.sources = sources;
            queue = new int[sources.Length];
            waiting = new bool[sources.Length];
        }

        /// <summary>How many distinct sources are waiting right now.</summary>
        internal int PendingCount => count;

        /// <summary>
        /// Starts listening to every source of the machine. Safe to abandon halfway: <see cref="Close"/>
        /// removes whatever was subscribed and ignores the rest. Opening twice would subscribe the
        /// same inbox to the same source twice, so it is refused rather than counted.
        /// </summary>
        internal void Open()
        {
            if (open)
            {
                throw new InvalidOperationException(
                    "Fynite: the event inbox of this machine is already open.");
            }

            open = true;

            for (var i = 0; i < sources.Length; i++)
            {
                sources[i].Subscribe(this, i);
            }
        }

        /// <summary>
        /// Stops listening and forgets what was waiting. Publishing afterwards reaches nothing, which
        /// is what lets a machine be ended without its context having to know.
        /// </summary>
        internal void Close()
        {
            open = false;

            for (var i = 0; i < sources.Length; i++)
            {
                sources[i].Unsubscribe(this);
            }

            for (var i = 0; i < waiting.Length; i++)
            {
                waiting[i] = false;
            }

            head = 0;
            count = 0;
        }

        /// <summary>
        /// Called by every publication of a source this machine listens to, including from inside a
        /// callback and while the owner is disabled. It only records that the occurrence happened: a
        /// source already waiting stays one occurrence, so repeated publications never grow the queue.
        /// </summary>
        void IFyniteEventSink.Signal(int slot)
        {
            if (!open || waiting[slot])
            {
                return;
            }

            waiting[slot] = true;

            var tail = head + count;
            if (tail >= queue.Length)
            {
                tail -= queue.Length;
            }

            queue[tail] = slot;
            count++;
        }

        internal int Dequeue()
        {
            var slot = queue[head];

            head++;
            if (head == queue.Length)
            {
                head = 0;
            }

            count--;
            waiting[slot] = false;
            return slot;
        }
    }
}
