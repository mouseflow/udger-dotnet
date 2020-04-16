using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Udger.Parser.Test.Models;
using Xunit;

namespace Udger.Parser.Test
{
    public class UdgerParserTest : IClassFixture<UdgerParserFixture>
    {
        private readonly UdgerParser parser;

        public UdgerParserTest(UdgerParserFixture fixture)
        {
            parser = fixture.Parser;
        }

        [Fact]
        public async Task ParseUserAgent_should_return_correct_user_agent()
        {
            foreach (var testData in await GetUserAgentTestData())
            {
                var actual = parser.ParseUserAgent(testData.Test.TestString);
                Compare(testData.Return, actual, testData.Test.TestString);
            }
        }

        [Fact]
        public async Task ParseIpAddress_should_return_correct_IP_address()
        {
            foreach (var testData in await GetIpAddressTestData())
            {
                var actual = parser.ParseIpAddress(testData.Test.TestString);
                Compare(testData.Return, actual, testData.Test.TestString);
            }
        }

        private void Compare(object expected, object actual, string testString)
        {
            var expectedType = expected.GetType();
            var actualType = actual.GetType();
            foreach (var expectedProperty in expectedType.GetProperties())
            {
                var actualProperty = actualType.GetProperty(expectedProperty.Name);
                var expectedValue = expectedProperty.GetValue(expected)?.ToString() ?? "";
                var actualValue = actualProperty.GetValue(actual)?.ToString() ?? "";
                Assert.Equal(expectedValue, actualValue);
            }
        }

        public static async Task<IEnumerable<TestData<UserAgentReturn>>> GetUserAgentTestData()
        {
            using (var stream = File.OpenRead("./TestFiles/test_ua.json"))
            {
                return await JsonSerializer.DeserializeAsync<IEnumerable<TestData<UserAgentReturn>>>(stream);
            }
        }

        private static async Task<IEnumerable<TestData<IpAddressReturn>>> GetIpAddressTestData()
        {
            using (var stream = File.OpenRead("./TestFiles/test_ip.json"))
            {
                return await JsonSerializer.DeserializeAsync<IEnumerable<TestData<IpAddressReturn>>>(stream);
            }
        }
    }

    public class UdgerParserFixture : IDisposable
    {
        public UdgerParserFixture()
        {
            Parser = new UdgerParser();
            Parser.SetDataDir("./TestFiles", "udgerdb_v3.test.dat");
        }

        public UdgerParser Parser { get; }

        public void Dispose()
        { }
    }
}
