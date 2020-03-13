using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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

        private string  threadId => $"[{Thread.CurrentThread.ManagedThreadId}]{"",-3}";

        public ThreadingTest(ParserFixture parserFixture, ITestOutputHelper output)
        {
            this.output = output;
            this.parserFixture = parserFixture;
        }

        [Fact]
        public void Test_can_parse_ip()
        {
            var ip4Addr = parserFixture.parser.ParseIPAddress("77.75.74.35");
            var ip6Addr = parserFixture.parser.ParseIPAddress("2a02:598:111::9");
            
            Assert.Equal("CZ", ip6Addr.IpCountryCode);
            Assert.Equal("CZ", ip4Addr.IpCountryCode);
        }

        [Fact]
        public void Test_multi_threading()
        {
            output.WriteLine($"Cache size: {parserFixture.parser.CacheSize}");
            var tasks = new Task[4];

            tasks[0] = StartNewThread(delegate () { TestUserAgents(parserFixture.SafariUserAgents, "Safari"); });
            tasks[1] = StartNewThread(delegate () { TestUserAgents(parserFixture.ChromeUserAgents, "Chrome"); });
            tasks[2] = StartNewThread(delegate () { TestUserAgents(parserFixture.ChromeUserAgentsReversed, "Chrome"); });
            tasks[3] = StartNewThread(delegate () { TestUserAgents(parserFixture.SafariUserAgentsReversed, "Safari"); });

            Task.WaitAll(tasks);
            //parserFixture.parser.SaveCacheToDisk(@"C:\Mouseflow\Data\UserAgents\Cache\cache.json");
        }


        [Theory]
        [InlineData(64)]
        [InlineData(32)]
        [InlineData(16)]
        [InlineData(12)]
        [InlineData(8)]
        [InlineData(4)]
        [InlineData(1)]
        public void Test_16mb_UserAgents_list(int taskAmount)
        {
            output.WriteLine($"Cache size: {parserFixture.parser.CacheSize}");

            var list = parserFixture.LargeListUserAgents;
            Task[] tasks = new Task[taskAmount];
            var indexStart = list.Length / tasks.Length;
            for (int i = 0; i < tasks.Length; i++)
            {
                var modifier = i;
                tasks[i] = StartNewThread(delegate() { ProcessUserAgents(list, indexStart * modifier, parserFixture.parser); });
            }

            Task.WaitAll(tasks);
            output.WriteLine($"Cache size: {parserFixture.parser.CacheSize}");

            parserFixture.parser.SaveCacheToDisk(@"C:\Mouseflow\Data\UserAgents\Cache\cache.json");
        }

        private Task StartNewThread(Action method)
        {
            var start = DateTime.Now;
            var task = Task.Factory.StartNew(() =>
            {
                output.WriteLine($"{threadId} Task Stared");
                method();
            })
            .ContinueWith((t) =>
            {
                output.WriteLine($"{threadId} Task Finished in [{(DateTime.Now - start).TotalSeconds}]");
            });
            return task;
        }

        [Fact]
        public void Test_large_file_single_thread()
        {
            Test_16mb_UserAgents_list(1);
        }

        [Fact]
        public void Test_multi_threading2()
        {
            Test_multi_threading();
        }

        [Fact]
        public void Test_Safari_UserAgents()
        {
            output.WriteLine($"Cache size: {parserFixture.parser.CacheSize}");
            TestUserAgents(parserFixture.SafariUserAgents, "Safari");
        }

        [Fact]
        public void Test_UserAgent_cache()
        {
            output.WriteLine($"Cache size: {parserFixture.parser.CacheSize}");
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

        private void TestUserAgents(string[] uaStrings, string exceptedResult)
        {
#if DEBUG
            HashSet<string> stats = new HashSet<string>();
            output.WriteLine($"{threadId} [TestUserAgents] Started");
            output.WriteLine($"{threadId} [TestUserAgents] Expected result: {exceptedResult}");
#endif
            for (int i = 0; i < uaStrings.Length; i++)
            {
                var uAgent = parserFixture.parser.Parse(uaStrings[i]);
                Assert.Contains(exceptedResult, uAgent.UaFamily);
                Assert.NotNull(uAgent.DeviceClass);
#if DEBUG
                stats.Add(uAgent.OsFamily);
#endif
            }
#if DEBUG
            output.WriteLine($"{threadId} [TestUserAgents] Stats");
            foreach (var str in stats)
            {
                output.WriteLine($"\t{str}");
            }
            output.WriteLine($"{threadId} [TestUserAgents] Finished");
#endif
        }

        private void ProcessUserAgents(string[] uaStrings, int indexStart, UdgerParser parser)
        {
            int totalAgents = uaStrings.Length;
            for (int i = indexStart; i < totalAgents; i++) 
            {
                var uAgent = parser.Parse(uaStrings[i]);
            }
        }

    }
}
