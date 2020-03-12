using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mouseflow.Udger.Parser.Test
{
    class Program
    {
        private static UdgerParser mParser;

        static void Main(string[] args)
        {
            SetUpTestData();
            mParser = new UdgerParser();
            mParser.SetDataDir(@"C:\Mouseflow\Data\UserAgents");
            mParser.LoadData();

            TestThreadSafe();
            TestThreadSafe();
            TestThreadSafe();
            TestThreadSafe();

            Console.ReadKey();
        }


        static string[] chromeUas, chromeUasReversed, safariUas;

        public static void SetUpTestData()
        {
            safariUas = File.ReadAllLines(@"./TestFiles/SafariUserAgents.txt");
            chromeUas = File.ReadAllLines(@"./TestFiles/ChromeUserAgents.txt");
            chromeUasReversed = new string[chromeUas.Length];

            for (int i = (chromeUas.Length - 1), j = 0; i >= 0; i--, j++)
                chromeUasReversed[j] = chromeUas[i];
        }

        static Task[] tasks;
        public static void TestThreadSafe()
        {
            Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] TestThreadSafe Start");
            tasks = new Task[3];

            tasks[0] = Task.Factory.StartNew(() => TestUserAgents(safariUas, "Safari"));
            tasks[1] = Task.Factory.StartNew(() => TestUserAgents(chromeUas, "Chrome"));
            tasks[2] = Task.Factory.StartNew(() => TestUserAgents(chromeUasReversed, "Chrome"));

            Task.WaitAll(tasks);
            Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] TestThreadSafe Done");
        }

        public static void TestUserAgents(string[] uaStrings, string exceptedResult)
        {
            int totalAgents = uaStrings.Length;
            int count = 0;

            Dictionary<string, int> osList = new Dictionary<string, int>();
            Dictionary<string, int> deviceList = new Dictionary<string, int>();

            foreach (var ua in uaStrings)
            {
                var uAgent = mParser.Parse(ua);

                if (osList.TryGetValue(uAgent.OsFamily, out int osCount))
                    osList[uAgent.OsFamily] = osCount + 1;
                else
                    osList.Add(uAgent.OsFamily, 1);

                if (deviceList.TryGetValue(uAgent.DeviceClassCode, out int deviceCount))
                    deviceList[uAgent.DeviceClassCode] = deviceCount + 1;
                else
                    deviceList.Add(uAgent.DeviceClassCode, 1);
        
                count++;
                //Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] Expected Result: {exceptedResult} - {uAgent.UaFamily.Contains(exceptedResult)}");
            }
            
            StringBuilder sbOs = new StringBuilder();
            foreach (KeyValuePair<string,int> os in osList)
                sbOs.Append($"\n\t\t{os.Key}: {os.Value} ");

            StringBuilder sbDevice = new StringBuilder();
            foreach (KeyValuePair<string, int> device in deviceList)
                sbDevice.Append($"\n\t\t{device.Key}: {device.Value}");

            Console.WriteLine(
                $"{"[" + Thread.CurrentThread.ManagedThreadId + "]",-3} Total: {count + "/" + totalAgents}\n" +
                $"\tOS: {sbOs.ToString()}\n" +
                $"\tDevices: {sbDevice.ToString()}\n"
            );
        }

    }
}
