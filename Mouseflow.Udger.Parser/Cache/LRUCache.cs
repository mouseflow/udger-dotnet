/*
  UdgerParser - Local parser lib
  
  UdgerParser class parses useragent strings based on a database downloaded from udger.com
 
  author     The Udger.com Team (info@udger.com)
  copyright  Copyright (c) Udger s.r.o.
  license    GNU Lesser General Public License
  link       https://udger.com/products/local_parser
 */

using System;
using System.Collections.Concurrent;

namespace Mouseflow.Udger.Parser
{
    class LRUCache<TKey,TValue>
    {
        private readonly ConcurrentDictionary<TKey, Node> entries;
        private readonly int capacity;
        private Node head;
        private Node tail;

        public int CacheSize => entries.Count;

        private class Node
        {
            public Node Next { get; set; }
            public Node Previous { get; set; }
            public TKey Key { get; set; }
            public TValue Value { get; set; }
        }

        public LRUCache(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(
                    "capacity",
                    "Capacity should be greater than zero");
            this.capacity = capacity;
            entries = new ConcurrentDictionary<TKey, Node>();
            head = null;
        }

        public void Set(TKey key, TValue value)
        {
            Node entry;
            if (!entries.TryGetValue(key, out entry))
            {
                entry = new Node { Key = key, Value = value };
                //if (entries.Count == capacity)
                //{
                //    entries.TryRemove(tail.Key, out _);
                //    tail = tail.Previous;
                //    if (tail != null)
                //        tail.Next = null;
                //}
                entries.TryAdd(key, entry);
            }

            entry.Value = value;
            //MoveToHead(entry);
            if (tail == null)
                tail = head;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default(TValue);
            Node entry;
            if (!entries.TryGetValue(key, out entry))
                return false;

            //MoveToHead(entry);
            value = entry.Value;

            return true;
        }

        //private void MoveToHead(Node entry)
        //{
        //    if (entry == head || entry == null)
        //        return;

        //    var next = entry.Next;
        //    var previous = entry.Previous;

        //    if (next != null)
        //        next.Previous = entry.Previous;

        //    if (previous != null)
        //        previous.Next = entry.Next;

        //    entry.Previous = null;
        //    entry.Next = head;

        //    if (head != null)
        //        head.Previous = entry;

        //    head = entry;

        //    if (tail == entry)
        //        tail = previous;
        //}

    }
}
