using System;

namespace Fleck
{
    /// <summary>
    /// Growable byte buffer with a consume offset - replaces the List&lt;byte&gt;
    /// accumulation in the receive path (fork, 2026-07-27). The retired path
    /// paid a per-byte IEnumerator AddRange for every read and an O(remaining)
    /// RemoveRange shift after every parsed frame; here appends are
    /// Buffer.BlockCopy and consuming is a head-offset bump (the buffer
    /// compacts lazily, only when an append would otherwise grow it).
    /// </summary>
    public sealed class MessageBuffer
    {
        private byte[] _array;
        private int _head;
        private int _tail;

        public MessageBuffer() : this(8192) { }

        public MessageBuffer(int initialCapacity)
        {
            _array = new byte[initialCapacity];
        }

        /// <summary>Bytes available to read.</summary>
        public int Count
        {
            get { return _tail - _head; }
        }

        /// <summary>Reads relative to the current head.</summary>
        public byte this[int index]
        {
            get { return _array[_head + index]; }
        }

        public void Append(byte[] source, int count)
        {
            if (_tail + count > _array.Length)
            {
                int live = _tail - _head;
                int needed = live + count;

                if (needed <= _array.Length)
                {
                    // enough room once the consumed prefix is dropped - compact in place
                    Buffer.BlockCopy(_array, _head, _array, 0, live);
                }
                else
                {
                    int capacity = _array.Length * 2;
                    while (capacity < needed)
                        capacity *= 2;

                    byte[] bigger = new byte[capacity];
                    Buffer.BlockCopy(_array, _head, bigger, 0, live);
                    _array = bigger;
                }

                _head = 0;
                _tail = live;
            }

            Buffer.BlockCopy(source, 0, _array, _tail, count);
            _tail += count;
        }

        /// <summary>Advances past count consumed bytes; resets when drained.</summary>
        public void Consume(int count)
        {
            _head += count;
            if (_head == _tail)
            {
                _head = 0;
                _tail = 0;
            }
        }

        /// <summary>Block-copies out of the live region (sourceIndex is head-relative).</summary>
        public void CopyTo(int sourceIndex, byte[] destination, int destinationIndex, int count)
        {
            Buffer.BlockCopy(_array, _head + sourceIndex, destination, destinationIndex, count);
        }
    }
}
