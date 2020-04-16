using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Udger.Parser;
using Udger.Parser.Test.Models;
using Xunit;

namespace Udger.Parser.Test
{
    public class UdgerParserThreadingTest : IClassFixture<UdgerParserThreadingFixture>
    {
        private static UdgerParser parser;

        public UdgerParserThreadingTest(UdgerParserThreadingFixture fixture)
        {
            parser = fixture.Parser;
        }

        // For this test you need a production version of the udger v3 database
        [Fact]
        public async Task ParseUserAgent_should_be_thread_safe()
        {
            Parallel.Invoke(new ParallelOptions { MaxDegreeOfParallelism = 4 }, () =>
            {
                var userAgents = File.ReadAllLines(@"./TestFiles/ua_safari.txt");
                foreach (var userAgent in userAgents)
                {
                    var actual = parser.ParseUserAgent(userAgent);
                    Assert.Contains("Safari", actual.UaFamily);
                }
            }, () =>
            {
                var userAgents = File.ReadAllLines(@"./TestFiles/ua_chrome.txt");
                foreach (var userAgent in userAgents)
                {
                    var actual = parser.ParseUserAgent(userAgent);
                    Assert.Contains("Chrome", actual.UaFamily);
                }
            }, () =>
            {
                var userAgents = File.ReadAllLines(@"./TestFiles/ua_safari.txt").Reverse().ToArray();
                foreach (var userAgent in userAgents)
                {
                    var actual = parser.ParseUserAgent(userAgent);
                    Assert.Contains("Safari", actual.UaFamily);
                }
            }, () =>
            {
                var userAgents = File.ReadAllLines(@"./TestFiles/ua_chrome.txt").Reverse().ToArray();
                foreach (var userAgent in userAgents)
                {
                    var actual = parser.ParseUserAgent(userAgent);
                    Assert.Contains("Chrome", actual.UaFamily);
                }
            });
        }

        private static async Task<IEnumerable<TestData<UserAgentReturn>>> GetUserAgentTestData()
        {
            using (var stream = File.OpenRead("./TestFiles/test_ua_family.json"))
            {
                return await JsonSerializer.DeserializeAsync<IEnumerable<TestData<UserAgentReturn>>>(stream);
            }
        }
    }

    public class UdgerParserThreadingFixture
    {
        public UdgerParserThreadingFixture()
        {
            Parser = new UdgerParser();
            Parser.SetDataDir("./TestFiles", "udgerdb_v3.dat");
        }

        public UdgerParser Parser { get; }
    }
}
