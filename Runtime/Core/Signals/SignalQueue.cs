using System;

namespace Fynite
{
    /// <summary>
    /// FIFO ring buffer of pending signals, pre-allocated per machine and grown only when it overflows.
    /// </summary>
    internal sealed class SignalQueue
    {
        private FyniteSignal[] _items;
        private int _head;
        private int _count;

        internal SignalQueue(int capacity)
        {
            _items = new FyniteSignal[capacity];
        }

        internal int Count => _count;

        internal void Enqueue(in FyniteSignal signal)
        {
            if (_count == _items.Length)
            {
                Grow();
            }

            _items[(_head + _count) % _items.Length] = signal;
            _count++;
        }

        internal bool TryDequeue(out FyniteSignal signal)
        {
            if (_count == 0)
            {
                signal = default;
                return false;
            }

            signal = _items[_head];
            _items[_head] = default;
            _head = (_head + 1) % _items.Length;
            _count--;
            return true;
        }

        internal void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _head = 0;
            _count = 0;
        }

        private void Grow()
        {
            var grown = new FyniteSignal[_items.Length * 2];
            for (int i = 0; i < _count; i++)
            {
                grown[i] = _items[(_head + i) % _items.Length];
            }

            _items = grown;
            _head = 0;
        }
    }
}
