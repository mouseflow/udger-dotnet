/*
  UdgerParser - Local parser lib
  
  UdgerParser class parses useragent strings based on a database downloaded from udger.com
 
 
  author     The Udger.com Team (info@udger.com)
  copyright  Copyright (c) Udger s.r.o.
  license    GNU Lesser General Public License
  link       https://udger.com/products/local_parser
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Udger.Parser.Cache
{
    public class LRUCache<TKey, TValue>
    {
        private readonly Dictionary<TKey, Node> entries;

        public LRUCache(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero");

            Capacity = capacity;
            entries = new Dictionary<TKey, Node>();
        }

        public int Capacity { get; }
        public Node Head { get; private set; }
        public Node Tail { get; private set; }
        public IReadOnlyDictionary<TKey, Node> Entries => new ReadOnlyDictionary<TKey, Node>(entries);

        public bool TryAdd(TKey key, TValue value)
        {
            var entry = new Node(key, value);

            lock (this)
            {
                if (!entries.TryAdd(key, entry))
                    return false;

                MoveToHead(entry);

                if (entries.Count > Capacity)
                    RemoveTail();
            }

            return true;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default;

            if (!entries.TryGetValue(key, out var entry))
                return false;

            value = entry.Value;

            lock (this)
            {
                if (entry == Head)
                    return true;

                MoveToHead(entry);
            }

            return true;
        }

        private void MoveToHead(Node entry)
        {
            var next = entry.Next;
            var previous = entry.Previous;

            if (next != null)
                next.Previous = entry.Previous;

            if (previous != null)
                previous.Next = entry.Next;

            entry.Previous = null;
            entry.Next = Head;

            if (Head != null)
                Head.Previous = entry;

            Head = entry;

            if (Tail == null)
                Tail = entry;
            else if (Tail == entry)
                Tail = previous;
        }

        private void RemoveTail()
        {
            entries.Remove(Tail.Key);
            Tail = Tail.Previous;
            Tail.Next = null;
        }

        public class Node
        {
            public TKey Key { get; }
            public TValue Value { get; }
            public Node Previous { get; internal set; }
            public Node Next { get; internal set; }

            public Node(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }
    }
}
