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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Mouseflow.Udger.Parser
{
    public class LRUCache<TKey,TValue>
    {
        private ConcurrentDictionary<TKey, Node> entries;
        public string CachePath { get; private set; }

        public int Size => entries.Count;

        private int capacity;
        private class Node
        {
            public TValue Value { get; set; }
            public int Hits { get; set; }
        }

        public LRUCache(int capacity = 0, string cachePath = null)
        {
            this.CachePath = cachePath;
            this.capacity = capacity;
            if (string.IsNullOrWhiteSpace(cachePath))
                entries = new ConcurrentDictionary<TKey, Node>();
            else
                entries = JsonSerializer.Deserialize<ConcurrentDictionary<TKey, Node>>(File.ReadAllBytes(cachePath));
        }

        public ICollection<TValue> GetTopN(int n)
        {
            return entries.OrderByDescending(x => x.Value.Hits).Take(n).Select(x => x.Value.Value).ToArray();
        }

        public bool TryAdd(TKey key, TValue value)
        {
            if (!entries.TryGetValue(key, out _))
            {
                if (capacity > 0 && entries.Count > capacity)
                    Flush(0.2m);
                return entries.TryAdd(key, new Node(){ Value = value, Hits = 1});
            }
            return false;
        }



        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default(TValue);
            Node entry;
            if (!entries.TryGetValue(key, out entry))
                return false;
            value = entry.Value;
            entry.Hits++;
            return true;
        }

        private readonly object _lock = new object();
        public int Flush(decimal flushPct = 1)
        {
            if (!Monitor.TryEnter(_lock))
                return -1;

            try
            {
                var tmp = entries.Count;
                foreach (TKey key in entries.OrderBy(x => x.Value.Hits).Take(decimal.ToInt32(tmp * flushPct)).Select(x => x.Key).ToArray())
                {
                    entries.TryRemove(key, out _);
                }
                return tmp - entries.Count;
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }

        public int LoadCache(string cachePath)
        {
            this.CachePath = cachePath;
            entries = JsonSerializer.Deserialize<ConcurrentDictionary<TKey, Node>>(File.ReadAllBytes(cachePath));
            return entries.Count;
        }

        public int ReloadCache()
        {
            entries = JsonSerializer.Deserialize<ConcurrentDictionary<TKey, Node>>(File.ReadAllBytes(CachePath));
            return entries.Count;
        }

        public void SaveCache(string path)
        {
            string jsonString = JsonSerializer.Serialize(entries);
            File.WriteAllText(path, jsonString);
            CachePath = path;
        }

    }
}
