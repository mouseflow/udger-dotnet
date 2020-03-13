using System;
using System.IO;

namespace Mouseflow.Udger.Parser.Test.Tests.Fixtures
{

    public class ParserFixture : IDisposable
    {
        protected UdgerParser parser;

        private static string[] _chromeUas, _chromeUasReversed;
        private static string[] _safariUas, _safariUasReversed;
        private static string[] _largeListUserAgents;

        public string[] SafariUserAgents => _safariUas;
        public string[] ChromeUserAgents => _chromeUas;
        public string[] SafariUserAgentsReversed => _safariUasReversed;
        public string[] ChromeUserAgentsReversed => _chromeUasReversed;
        public string[] LargeListUserAgents { get {
                if (_largeListUserAgents == null)
                    _largeListUserAgents = File.ReadAllLines(@"./TestFiles/UserAgents_Large.txt");
                return _largeListUserAgents;
            }}

        public UdgerParser InitParser(int caschCapacity = 10000, string cachepath = null)
        {
            if (parser == null)
            {
                parser = new UdgerParser(true, caschCapacity, cachepath);
                parser.SetDataDir(@"C:\Mouseflow\Data\UserAgents\");
                parser.LoadDataIntoMemory();
            }
            return parser;
        }

        public ParserFixture()
        {
            LoadTestData();
        }

        private static void LoadTestData()
        {
            _safariUas = File.ReadAllLines(@"./TestFiles/SafariUserAgents.txt");
            _chromeUas = File.ReadAllLines(@"./TestFiles/ChromeUserAgents.txt");
            _chromeUasReversed = ReverseArray(_chromeUas);
            _safariUasReversed = ReverseArray(_safariUas);
        }

        private static string[] ReverseArray(string[] array)
        {
            var reverseArray = new string[array.Length];
            for (int i = (array.Length - 1), j = 0; i >= 0; i--, j++)
                reverseArray[j] = array[i];
            return reverseArray;
        }

        public void Dispose()
        {

        }

    }
}
