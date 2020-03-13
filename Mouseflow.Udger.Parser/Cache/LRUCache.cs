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
    public class LRUCache<TKey,TValue>
    {
        private ConcurrentDictionary<TKey, TValue> entries;
        public string CachePath { get; private set; }

        public int Size => entries.Count;

        public LRUCache(int capacity, string cachePath = null)
        {
            this.CachePath = cachePath;

            if (string.IsNullOrWhiteSpace(cachePath))
                entries = new ConcurrentDictionary<TKey, TValue>();
            else
                entries = JsonSerializer.Deserialize<ConcurrentDictionary<TKey, TValue>>(File.ReadAllBytes(cachePath));
        }

        public bool TryAdd(TKey key, TValue value)
        {
            if (!entries.TryGetValue(key, out _))    
                return entries.TryAdd(key, value);
            return false;
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

        public int Flush()
        {
            var tmp = entries.Count;
            entries.Clear();
            return tmp;
        }

        public int LoadCache(string cachePath)
        {
            this.CachePath = cachePath;
            entries = JsonSerializer.Deserialize<ConcurrentDictionary<TKey, TValue>>(File.ReadAllBytes(cachePath));
            return entries.Count;
        }

        public int ReloadCache()
        {
            entries = JsonSerializer.Deserialize<ConcurrentDictionary<TKey, TValue>>(File.ReadAllBytes(CachePath));
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
