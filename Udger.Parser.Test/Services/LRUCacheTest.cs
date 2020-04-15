using System;
using System.Linq;
using Udger.Parser.Cache;
using Xunit;

namespace Udger.Parser.Test.Services
{
    public class LRUCacheTest
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_should_throw_when_capacity_is_invalid(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LRUCache<string, string>(capacity));
        }

        [Fact]
        public void TryAdd_should_return_true_when_value_is_added()
        {
            var sut = new LRUCache<string, string>(100);
            var actual = sut.TryAdd("key", "value");
            Assert.True(actual);
        }

        [Fact]
        public void TryAdd_should_return_false_when_key_exists()
        {
            var sut = new LRUCache<string, string>(100);
            sut.TryAdd("key", "value");
            var actual = sut.TryAdd("key", "other value");
            Assert.False(actual);
        }

        [Fact]
        public void TryAdd_should_return_true_when_different_keys_are_added()
        {
            var sut = new LRUCache<string, string>(100);
            Assert.True(sut.TryAdd("key", "value"));
            Assert.True(sut.TryAdd("other key", "value"));
        }

        [Fact]
        public void TryAdd_should_set_first_entry_to_head()
        {
            var sut = new LRUCache<string, string>(100);
            sut.TryAdd("key", "value");
            var actual = sut.Entries.Values.First();
            Assert.Same(actual, sut.Head);
        }

        [Fact]
        public void TryAdd_should_set_first_entry_to_tail()
        {
            var sut = new LRUCache<string, string>(100);
            sut.TryAdd("key", "value");
            var actual = sut.Entries.Values.First();
            Assert.Same(actual, sut.Tail);
        }

        [Fact]
        public void TryAdd_should_move_new_entry_to_head()
        {
            var sut = new LRUCache<string, string>(100);
            sut.TryAdd("key", "value");
            sut.TryAdd("other key", "value");
            var actual = sut.Entries.Values.Last();
            Assert.Same(actual, sut.Head);
            Assert.Same(actual.Next, sut.Tail);
            Assert.Same(sut.Tail.Previous, actual);
        }

        [Fact]
        public void TryAdd_should_remove_tail_when_capacity_is_exceeded()
        {
            var sut = new LRUCache<string, string>(2);
            sut.TryAdd("key 1", "value");
            sut.TryAdd("key 2", "value");
            sut.TryAdd("key 3", "value");
            Assert.Equal(2, sut.Entries.Count);
            Assert.Equal("key 2", sut.Entries.Keys.First());
            Assert.Equal("key 3", sut.Entries.Keys.Last());
        }

        [Fact]
        public void TryGetValue_should_return_value()
        {
            var sut = new LRUCache<string, string>(100);
            var expected = "value";
            sut.TryAdd("key", expected);
            sut.TryGetValue("key", out var actual);
            Assert.Equal(actual, expected);
        }

        [Fact]
        public void TryGetValue_should_return_true_when_key_is_found()
        {
            var sut = new LRUCache<string, string>(100);
            sut.TryAdd("key", "value");
            var actual = sut.TryGetValue("key", out _);
            Assert.True(actual);
        }

        [Fact]
        public void TryGetValue_should_return_false_when_key_is_not_found()
        {
            var sut = new LRUCache<string, string>(100);
            var actual = sut.TryGetValue("key", out _);
            Assert.False(actual);
        }

        [Fact]
        public void TryGetValue_should_move_entry_to_head()
        {
            var sut = new LRUCache<string, string>(100);
            sut.TryAdd("key 1", "value");
            sut.TryAdd("key 2", "value");
            sut.TryGetValue("key 1", out _);
            var actual = sut.Entries.Values.First();
            Assert.Same(actual, sut.Head);
            Assert.Same(actual.Next, sut.Tail);
            Assert.Same(sut.Tail.Previous, actual);
        }

        [Fact]
        public void TryGetValue_should_connect_next_and_previous_nodes_when_moving_entry()
        {
            var sut = new LRUCache<string, string>(100);
            sut.TryAdd("key 1", "value");
            sut.TryAdd("key 2", "value");
            sut.TryAdd("key 3", "value");
            sut.TryGetValue("key 2", out _);
            var nodes = sut.Entries.Values.ToList();
            Assert.Same(nodes[0].Previous, nodes[2]);
            Assert.Same(nodes[2].Next, nodes[0]);
        }
    }
}
