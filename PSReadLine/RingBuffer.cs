/********************************************************************++
Copyright (c) Microsoft Corporation.  All rights reserved.
--********************************************************************/

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PowerShell
{
    [ExcludeFromCodeCoverage]
    internal sealed class QueueDebugView<T>
    {
        private readonly RingBuffer<T> _queue;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items => this._queue.ToArray();

        public QueueDebugView(RingBuffer<T> queue)
        {
            this._queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }
    }

    [DebuggerDisplay("Count = {" + nameof(Count) + "}")]
    [DebuggerTypeProxy(typeof(QueueDebugView<>))]
    internal class RingBuffer<T>
    {
        private readonly T[] _array;
        private int _head;
        private int _tail;

        public int Capacity => _array.Length;
        public int Count { get; private set; }

        public RingBuffer(int capacity)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);
            _array = new T[capacity];
            _head = _tail = Count = 0;
        }

        public void Clear()
        {
            Array.Clear(_array);
            _head = _tail = Count = 0;
        }

        public void Enqueue(T item)
        {
            if (Count == _array.Length)
            {
                Dequeue();
            }
            _array[_tail] = item;
            _tail = (_tail + 1) % _array.Length;
            Count += 1;
        }

        public T Dequeue()
        {
            if (Count == 0) throw new InvalidOperationException("RingBuffer is empty");

            T obj = _array[_head];
            _array[_head] = default;
            _head = (_head + 1) % _array.Length;
            Count -= 1;
            return obj;
        }

        public T[] ToArray()
        {
            var result = new T[Count];
            if (Count > 0)
            {
                if (_head < _tail)
                {
                    Array.Copy(_array, _head, result, 0, Count);
                }
                else
                {
                    Array.Copy(_array, _head, result, 0, _array.Length - _head);
                    Array.Copy(_array, 0, result, _array.Length - _head, _tail);
                }
            }
            return result;
        }

        [ExcludeFromCodeCoverage]
        public T this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
                return _array[(_head + index) % _array.Length];
            }
        }
    }
}
