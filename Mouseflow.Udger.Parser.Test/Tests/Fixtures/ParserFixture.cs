using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Mouseflow.Udger.Parser.Test.Tests.Fixtures
{

    public class ParserFixture : IDisposable
    {
        public readonly UdgerParser parser;

        private static string[] _chromeUas, _safariUas;
        private static string[] _chromeUasReversed, _safariUasReversed;
        private static string[] _largeListUserAgents;

        public string[] SafariUserAgents => _safariUas;
        public string[] ChromeUserAgents => _chromeUas;
        public string[] SafariUserAgentsReversed => _safariUasReversed;
        public string[] ChromeUserAgentsReversed => _chromeUasReversed;

        public string[] LargeListUserAgents
        {
            get
            {
                if (_largeListUserAgents == null)
                _largeListUserAgents = File.ReadAllLines(@"./TestFiles/UserAgents_Large.txt");

                return _largeListUserAgents;
            }
        }

        public ParserFixture()
        {
            LoadTestData();
            parser = new UdgerParser(true, 100000); // @"C:\Mouseflow\Data\UserAgents\Cache\cache.json"
            parser.SetDataDir(@"C:\Mouseflow\Data\UserAgents");
            parser.LoadData();
        }

        public static void LoadTestData()
        {
            _safariUas = File.ReadAllLines(@"./TestFiles/SafariUserAgents.txt");
            _chromeUas = File.ReadAllLines(@"./TestFiles/ChromeUserAgents.txt");
            _chromeUasReversed = ReverseArray(_chromeUas);
            _safariUasReversed = ReverseArray(_safariUas);
        }

        public static string[] ReverseArray(string[] array)
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
