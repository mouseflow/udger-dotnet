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

        public ThreadingTest(ParserFixture parserFixture, ITestOutputHelper output)
        {
            this.output = output;
            this.parserFixture = parserFixture;
        }

        [Fact]
        public void Test_multi_threading()
        {
            output.WriteLine($"Cache size: {parserFixture.parser.CacheSize}");
            var tasks = new Task[12];

            tasks[0] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.SafariUserAgents, "Safari"));
            tasks[1] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.ChromeUserAgents, "Chrome"));
            tasks[2] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.ChromeUserAgentsReversed, "Chrome"));
            tasks[3] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.SafariUserAgentsReversed, "Safari"));
            tasks[4] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.SafariUserAgents, "Safari"));
            tasks[5] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.ChromeUserAgents, "Chrome"));
            tasks[6] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.ChromeUserAgentsReversed, "Chrome"));
            tasks[7] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.SafariUserAgentsReversed, "Safari"));
            tasks[8] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.SafariUserAgents, "Safari"));
            tasks[9] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.ChromeUserAgents, "Chrome"));
            tasks[10] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.ChromeUserAgentsReversed, "Chrome"));
            tasks[11] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.SafariUserAgentsReversed, "Safari"));

            Task.WaitAll(tasks);
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
            var list = parserFixture.LargeListUserAgents;
            Task[] tasks = new Task[taskAmount];

            output.WriteLine($"Large List UserAgentStrings: {list.Length}");
            output.WriteLine($"Cache size: {parserFixture.parser.CacheSize}");

            var indexStart = list.Length / tasks.Length;
            for (int i = 0; i < tasks.Length; i++)
            {
                var multiplier = i;
                tasks[i] = Task.Factory.StartNew(() => ProcessUserAgents(list, indexStart * multiplier));
                output.WriteLine($"Task {i} started");
            }

            Task.WaitAll(tasks);
            output.WriteLine($"Cache size: {parserFixture.parser.CacheSize}");
            //ProcessUserAgents(parserFixture.LargeListUserAgents, 0);
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
            var start = DateTime.Now;
            int totalAgents = uaStrings.Length;
            int count = 0;
            foreach (var ua in uaStrings)
            {
                var uAgent = parserFixture.parser.Parse(ua);
                Assert.Contains(exceptedResult, uAgent.UaFamily);
                count++;
            }
        }

        private void ProcessUserAgents(string[] uaStrings, int indexStart)
        {
            int totalAgents = uaStrings.Length;
            for (int i = indexStart; i < totalAgents; i++) 
            {
                var uAgent = parserFixture.parser.Parse(uaStrings[i]);
            }
        }

    }
}
