using System;
using System.Collections.Generic;
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

        public ThreadingTest(ParserFixture parserFixture)
        {
            this.parserFixture = parserFixture;
        }

        [Fact]
        public void Test_multi_threading()
        {
            Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] TestThreadSafe Start");
            var tasks = new Task[4];

            tasks[0] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.SafariUserAgents, "Safari"));
            tasks[1] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.ChromeUserAgents, "Chrome"));
            tasks[2] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.ReverseArray(parserFixture.ChromeUserAgents), "Chrome"));
            tasks[3] = Task.Factory.StartNew(() => TestUserAgents(parserFixture.ReverseArray(parserFixture.SafariUserAgents), "Safari"));

            Task.WaitAll(tasks);
            Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] TestThreadSafe Done");
        }

        [Fact]
        public void Test_Safari_UserAgents()
        {
            TestUserAgents(parserFixture.SafariUserAgents, "Safari");
        }

        [Fact]
        public void Test_Safari_UserAgents_cache()
        {
            TestUserAgents(parserFixture.SafariUserAgents, "Safari");
            TestUserAgents(parserFixture.ReverseArray(parserFixture.SafariUserAgents), "Safari");
        }

        private void TestUserAgents(string[] uaStrings, string exceptedResult)
        {
            int totalAgents = uaStrings.Length;
            int count = 0;
            foreach (var ua in uaStrings)
            {
                var uAgent = parserFixture.parser.Parse(ua);
                Assert.Contains(exceptedResult, uAgent.UaFamily);
                count++;
            }
        }

    }
}
