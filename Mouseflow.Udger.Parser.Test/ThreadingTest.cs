using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mouseflow.Udger.Parser.Test.Tests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Mouseflow.Udger.Parser.Test.Tests
{
    public class ThreadingTest : IClassFixture<ParserFixture>
    {
        private readonly ITestOutputHelper output;
        private readonly ParserFixture parserFixture;
        private readonly UdgerParser parser;

        public ThreadingTest(ParserFixture parserFixture, ITestOutputHelper output)
        {
            this.output = output;
            this.parserFixture = parserFixture;
            parser = parserFixture.InitParser(100000); // @"C:\Mouseflow\Data\UserAgents\Cache\cache.json"
        }

        #region theories
        [Theory]
        [InlineData(16), InlineData(32), InlineData(64)]
        [InlineData(12)]
        public void Test_16mb_UserAgents_list(int taskAmount)
        {
            output.WriteLine($"Cache size: {parser.CacheSize}");

            var list = parserFixture.LargeListUserAgents;
            Task[] tasks = new Task[taskAmount];
            var indexSpan = list.Length / tasks.Length;
            for (int i = 0; i < tasks.Length; i++)
            {
                var modifier = i * indexSpan;
                tasks[i] = StartNewThread(delegate () { ProcessUserAgents(list.Skip(modifier).Take(indexSpan).ToArray(), parser); });
            }
            Task.WaitAll(tasks);

            output.WriteLine($"Cache size: {parser.CacheSize}");

            //parser.SaveCacheToDisk(@"C:\Mouseflow\Data\UserAgents\Cache\cache.json");
        }
        #endregion

        #region facts
        [Fact]
        public void Test_can_parse_ip()
        {
            var ip4Addr = parser.ParseIPAddress("77.75.74.35");
            var ip6Addr = parser.ParseIPAddress("2a02:598:111::9");
            Assert.Equal("CZ", ip6Addr.IpCountryCode);
            Assert.Equal("CZ", ip4Addr.IpCountryCode);
        }

        [Fact]
        public void Test_multi_threading()
        {
            output.WriteLine($"Cache size: {parser.CacheSize}");
            var tasks = new Task[4];

            tasks[0] = StartNewThread(delegate () { TestUserAgents(parserFixture.SafariUserAgents, "Safari"); });
            tasks[1] = StartNewThread(delegate () { TestUserAgents(parserFixture.ChromeUserAgents, "Chrome"); });
            tasks[2] = StartNewThread(delegate () { TestUserAgents(parserFixture.ChromeUserAgentsReversed, "Chrome"); });
            tasks[3] = StartNewThread(delegate () { TestUserAgents(parserFixture.SafariUserAgentsReversed, "Safari"); });

            Task.WaitAll(tasks);
            //parserFixture.parser.SaveCacheToDisk(@"C:\Mouseflow\Data\UserAgents\Cache\cache.json");
            
        }

        [Fact]
        public void Test_multi_threading2()
        {
            Test_multi_threading();
        }

        [Fact]
        public void Test_Safari_UserAgents()
        {
            output.WriteLine($"Cache size: {parser.CacheSize}");
            TestUserAgents(parserFixture.SafariUserAgents, "Safari");
        }

        [Fact]
        public void Test_UserAgent_cache()
        {
            output.WriteLine($"Cache size: {parser.CacheSize}");
            TestUserAgents(parserFixture.SafariUserAgents, "Safari");
            TestUserAgents(parserFixture.ChromeUserAgents, "Chrome");
            TestUserAgents(parserFixture.SafariUserAgents, "Safari");
            TestUserAgents(parserFixture.ChromeUserAgents, "Chrome");
            TestUserAgents(parserFixture.SafariUserAgents, "Safari");
            TestUserAgents(parserFixture.ChromeUserAgents, "Chrome");
            TestUserAgents(parserFixture.SafariUserAgents, "Safari");
            TestUserAgents(parserFixture.ChromeUserAgents, "Chrome");
            TestUserAgents(parserFixture.SafariUserAgents, "Safari");
            TestUserAgents(parserFixture.ChromeUserAgents, "Chrome");
        }
        #endregion

        #region methods
        private void TestUserAgents(string[] uaStrings, string exceptedResult)
        {
            for (int i = 0; i < uaStrings.Length; i++)
            {
                var uAgent = parser.Parse(uaStrings[i]);
                Assert.Contains(exceptedResult, uAgent.UaFamily);
                Assert.NotNull(uAgent.DeviceClass);
            }
        }

        private void ProcessUserAgents(string[] uaStrings, UdgerParser parser)
        {
            for (int i = 0; i < uaStrings.Length; i++) 
            {
                var uAgent = parser.Parse(uaStrings[i]);
                Assert.NotNull(uAgent);
            }
        }

        private Task StartNewThread(Action method)
        {
            var start = DateTime.Now;
            var task = Task.Factory.StartNew(() =>
                {
                    method();
                })
                .ContinueWith((t) =>
                {
                });
            return task;
        }
        #endregion
    }
}
