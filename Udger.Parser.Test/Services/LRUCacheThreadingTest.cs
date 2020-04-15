using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Udger.Parser.Cache;
using Xunit;

namespace Udger.Parser.Test.Services
{
    public class LRUCacheThreadingTest : IClassFixture<LRUCacheThreadingFixture>
    {
        private readonly LRUCacheThreadingFixture fixture;
        private readonly LRUCache<string, string> cache;

        public LRUCacheThreadingTest(LRUCacheThreadingFixture fixture)
        {
            this.fixture = fixture;
            cache = LRUCacheThreadingFixture.Cache;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_should_throw_when_capacity_is_invalid(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LRUCache<string, string>(capacity));
        }

        [Fact]
        public void Head_should_not_be_null()
        {
            Assert.NotNull(cache.Head);
        }

        [Fact]
        public void Previous_node_of_head_should_be_null()
        {
            Assert.Null(cache.Head.Previous);
        }

        [Fact]
        public void Next_node_of_head_should_not_be_null()
        {
            Assert.NotNull(cache.Head.Next);
        }

        [Fact]
        public void Tail_should_not_be_null()
        {
            Assert.NotNull(cache.Tail);
        }

        [Fact]
        public void Previous_node_of_tail_should_not_be_null()
        {
            Assert.NotNull(cache.Tail.Previous);
        }

        [Fact]
        public void Next_node_of_tail_should_be_null()
        {
            Assert.Null(cache.Tail.Next);
        }

        [Fact]
        public void Entries_should_not_contain_duplicate_keys()
        {
            var entry = cache.Head;
            var foundKeys = new List<string>();
            while (entry != null)
            {
                Assert.DoesNotContain(entry.Key, foundKeys);
                foundKeys.Add(entry.Key);
                entry = entry.Next;
            }
        }

        [Fact]
        public void Tail_should_be_the_last_entry()
        {
            var entry = cache.Head;
            var foundKeys = new List<string>();
            while (entry.Next != null)
            {
                if (foundKeys.Contains(entry.Key))
                    break;

                foundKeys.Add(entry.Key);
                entry = entry.Next;
            }
            Assert.Same(cache.Tail, entry);
        }

        [Fact]
        public void Number_of_entries_should_match_entries_count()
        {
            var entry = cache.Head;
            var foundKeys = new List<string>();
            while (entry != null)
            {
                if (foundKeys.Contains(entry.Key))
                    break;

                foundKeys.Add(entry.Key);
                entry = entry.Next;
            }

            Assert.Equal(cache.Entries.Count, foundKeys.Count);
        }

        [Fact]
        public void Entries_should_have_previous_node_except_for_head()
        {
            foreach (var entry in cache.Entries.Values.Where(entry => entry != cache.Head))
            {
                Assert.NotNull(entry.Previous);
            }
        }

        [Fact]
        public void Entries_should_have_next_node_except_for_tail()
        {
            foreach (var entry in cache.Entries.Values.Where(entry => entry != cache.Tail))
            {
                Assert.NotNull(entry.Next);
            }
        }

        [Fact]
        public void Number_of_entries_should_equal_capacity()
        {
            Assert.Equal(cache.Capacity, cache.Entries.Count);
        }
    }

    public class LRUCacheThreadingFixture : IDisposable
    {
        private const int NumberOfThreads = 4;
        private const int ExecutionsPerThread = 1000000;

        public LRUCacheThreadingFixture()
        {
            Cache = new LRUCache<string, string>(100);

            var threads = Enumerable
                .Range(1, NumberOfThreads)
                .Select(_ => new Thread(() =>
                {
                    var random = new Random();
                    for (var i = 0; i < ExecutionsPerThread; i++)
                    {
                        var key = random.Next(Cache.Capacity).ToString();
                        if (Cache.TryGetValue(key, out var value))
                            continue;

                        value = Guid.NewGuid().ToString();
                        Cache.TryAdd(key, value);
                    }
                }))
                .ToList();

            threads.ForEach(thread => thread.Start());
            threads.ForEach(thread => thread.Join());
        }

        public static LRUCache<string, string> Cache { get; private set; }

        public void Dispose()
        {
            Cache = null;
        }
    }
}
