/*
  UdgerParser - Test - Local parser lib 
 
  author     The Udger.com Team (info@udger.com)
  copyright  Copyright (c) Udger s.r.o.
 
  license    GNU Lesser General Public License
  link       http://udger.com/products/local_parser
*/


using Udger.Parser;


namespace ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            UserAgent a;
            IpAddress i;

            // Create a new UdgerParser object
            var parser = new UdgerParser();
            // orCreate and set LRU Cache capacity
            //UdgerParser parser = new UdgerParser(5000);

            // Set data dir (in this directory is stored data file: udgerdb_v3.dat)
            // Test data file is available on:  https://github.com/udger/test-data/tree/master/data_v3
            // Full data file can be downloaded manually from http://data.udger.com/, but we recommend use udger-updater
            parser.SetDataDir(@"C:\udger");
            // or set data dir and DB filename
            //parser.SetDataDir(@"C:\udger", "udgerdb_v3-noip.dat ");

            // Parse user agent and IP address
            a = parser.ParseUserAgent(@"Mozilla/5.0 (compatible; SeznamBot/3.2; +http://fulltext.sblog.cz/)");
            i = parser.ParseIpAddress("77.75.74.35");

            // Parse user agent and IP address
            a = parser.ParseUserAgent(@"Mozilla/5.0 (Linux; U; Android 4.0.4; sk-sk; Luna TAB474 Build/LunaTAB474) AppleWebKit/534.30 (KHTML, like Gecko) Version/4.0 Safari/534.30");
            i = parser.ParseIpAddress("2a02:598:111::9");
        }
    }
}
