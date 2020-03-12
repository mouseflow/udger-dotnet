using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mouseflow.Udger.Parser.Test.Tests.Fixtures
{

    public class ParserFixture : IDisposable
    {
        public readonly UdgerParser parser;

        private static string[] _chromeUas, _safariUas;

        public string[] SafariUserAgents => _safariUas;
        public string[] ChromeUserAgents => _chromeUas;

        public ParserFixture()
        {
            LoadTestData();
            parser = new UdgerParser();
            parser.SetDataDir(@"C:\Mouseflow\Data\UserAgents");
            parser.LoadData();
        }

        public static void LoadTestData()
        {
            _safariUas = File.ReadAllLines(@"./TestFiles/SafariUserAgents.txt");
            _chromeUas = File.ReadAllLines(@"./TestFiles/ChromeUserAgents.txt");
        }

        public string[] ReverseArray(string[] array)
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
