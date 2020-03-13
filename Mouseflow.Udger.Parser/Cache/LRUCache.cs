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
using System.IO;
using System.Text.Json;

namespace Mouseflow.Udger.Parser
{
    class LRUCache<TKey,TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> entries;
        private readonly int capacity;

        public int CacheSize => entries.Count;

        public LRUCache(int capacity, string cachePath = null)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(
                    "capacity",
                    "Capacity should be greater than zero");
            this.capacity = capacity;
            if (cachePath == null) { 
                entries = new ConcurrentDictionary<TKey, TValue>();
            }
            else
            {
                entries = JsonSerializer.Deserialize<ConcurrentDictionary<TKey, TValue>>(File.ReadAllBytes(cachePath));
            }
        }



        public void Set(TKey key, TValue value)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                entries.TryAdd(key, value);
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default(TValue);
            TValue entry;
            if (!entries.TryGetValue(key, out entry))
                return false;

            value = entry;
            return true;
        }

        public void SaveCache(string path)
        {
            string jsonString = JsonSerializer.Serialize(entries);
            File.WriteAllText(path, jsonString);
        }

    }
}
