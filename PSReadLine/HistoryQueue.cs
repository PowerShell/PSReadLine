using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PSConsoleUtilities
{
    internal class HistoryQueue<T>
    {
        private T[] _array;
        private int _head;
        private int _tail;

        public HistoryQueue(int capacity)
        {
            Debug.Assert(capacity > 0);
            _array = new T[capacity];
            _head = _tail = Count = 0;
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++)
            {
                this[i] = default(T);
            }
            _head = _tail = Count = 0;
        }

        public bool Contains(T item)
        {
            return IndexOf(item) != -1;
        }

        public int Count { get; private set; }

        public int IndexOf(T item)
        {
            // REVIEW: should we use case insensitive here?
            var eqComparer = EqualityComparer<T>.Default;
            for (int i = 0; i < Count; i++)
            {
                if (eqComparer.Equals(this[i], item))
                {
                    return i;
                }
            }

            return -1;
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
            Debug.Assert(Count > 0);

            T obj = _array[_head];
            _array[_head] = default(T);
            _head = (_head + 1) % _array.Length;
            Count -= 1;
            return obj;
        }

        [ExcludeFromCodeCoverage]
        public T this[int index]
        {
            get
            { 
                Debug.Assert(index >= 0 && index < Count);
                return _array[(_head + index) % _array.Length];
            }
            set
            {
                Debug.Assert(index >= 0 && index < Count);
                _array[(_head + index) % _array.Length] = value;
            }
        }
    }
}
