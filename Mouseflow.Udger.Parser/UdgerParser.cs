/*
  UdgerParser - Local parser lib
  
  UdgerParser class parses useragent strings based on a database downloaded from udger.com
 
 
  author     The Udger.com Team (info@udger.com)
  copyright  Copyright (c) Udger s.r.o.
  license    GNU Lesser General Public License
  link       https://udger.com/products/local_parser
  
  Third Party lib:
  ADO.NET Data Provider for SQLite - http://www.sqlite.org/ - Public domain
  RegExpPerl.cs - https://github.com/DEVSENSE/Phalanger/blob/master/Source/ClassLibrary/RegExpPerl.cs - Apache License Version 2.0
 */
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Mouseflow.Udger.Parser.Data.Models;

namespace Mouseflow.Udger.Parser
{
    public class UdgerParser
    {
        private struct IdRegString
        {
            public int id;
            public int wordId1;
            public int wordId2;
            public string pattern;
        }

        public readonly LRUCache<string, UserAgent> Cache;
        public readonly bool UseCache;

        private static DataReader dt;
        private static WordDetector clientWordDetector;
        private static WordDetector deviceWordDetector;
        private static WordDetector osWordDetector;
        private static List<IdRegString> clientRegstringList;
        private static List<IdRegString> osRegstringList;
        private static List<IdRegString> deviceRegstringList;

        private static ConcurrentBag<DeviceName> conBag_DeviceName_List;
        private static ConcurrentBag<DeviceBrand> conBag_DeviceName_Brand;
        private static ConcurrentBag<DeviceRegex> conBag_SQL_DEVICE_REGEX;

        private static ConcurrentDictionary<string, Client> conDir_SQL_CLIENT, conDir_SQL_CRAWLER;
        private static ConcurrentDictionary<string, DeviceColumn> conDir_SQL_CLIENT_CLASS, conDir_SQL_DEVICE;
        private static ConcurrentDictionary<string, OS> conDir_SQL_OS, conDir_SQL_CLIENT_OS;

        private static ConcurrentDictionary<string, DataRow> conDir_IP_TABLE,conDir_IP_CLIENT;


        public UdgerParser(bool useLRUCash = true, int LRUCashCapacity = 10000, string cachePath = null)
        {
            dt = new DataReader();
            if (useLRUCash)
                Cache = new LRUCache<string, UserAgent>(LRUCashCapacity, cachePath);
            UseCache = useLRUCash;
        }

        public void SetDataDir(string dataDir, string fileName = null)
        {
            if (!Directory.Exists(dataDir))
                throw new Exception("Data dir not found");

            dt.DataDir = dataDir;
            dt.DataSourcePath = dataDir + $"\\{fileName ?? "udgerdb_v3.dat"}";

            if (!File.Exists(dt.DataSourcePath))
                throw new Exception($"Data file {fileName ?? "udgerdb_v3.dat"} not found");
        }

        public void LoadDataIntoMemory(bool ipDataInMemory = false)
        {
            if (!dt.Connected)
                dt.Connect();
            InitStaticStructures();

            conBag_DeviceName_List = dt.SelectQuery<DeviceName>(@"SELECT * FROM udger_devicename_list");
            conBag_DeviceName_Brand = dt.SelectQuery<DeviceBrand>(@"SELECT* FROM udger_devicename_brand");
            conBag_SQL_DEVICE_REGEX = dt.SelectQuery<DeviceRegex>(UdgerSqlQuery.SQL_DEVICE_REGEX);

            conDir_SQL_CLIENT = dt.SelectQuery<Client>(UdgerSqlQuery.SQL_CLIENT, 0);
            conDir_SQL_CRAWLER = dt.SelectQuery<Client>(UdgerSqlQuery.SQL_CRAWLER, 0);

            conDir_SQL_CLIENT_CLASS = dt.SelectQuery<DeviceColumn>(UdgerSqlQuery.SQL_CLIENT_CLASS, 0);
            conDir_SQL_DEVICE = dt.SelectQuery<DeviceColumn>(UdgerSqlQuery.SQL_DEVICE, 0);

            conDir_SQL_OS = dt.SelectQuery<OS>(UdgerSqlQuery.SQL_OS, 0);
            conDir_SQL_CLIENT_OS = dt.SelectQuery<OS>(UdgerSqlQuery.SQL_CLIENT_OS, 0);

            InitRegexString();
            if (ipDataInMemory) { 
                conDir_IP_TABLE = dt.SelectQuery(UdgerSqlQuery.SQL_IP_TABLE, 0);
            }

            GC.Collect();
        }

        private void InitRegexString() // Ugly Temp method
        {
            foreach (var client in conDir_SQL_CLIENT)
            {
                if (client.Value.regstring != null)
                    client.Value.Reg = new PerlRegExpConverter(client.Value.regstring, "", Encoding.UTF8).Regex;
            }
            foreach (var client in conDir_SQL_CRAWLER)
            {
                if(client.Value.regstring != null)
                    client.Value.Reg = new PerlRegExpConverter(client.Value.regstring, "", Encoding.UTF8).Regex;
            }
            foreach (var device in conBag_SQL_DEVICE_REGEX)
            {
                if (device.regstring != null)
                    device.Reg = new PerlRegExpConverter(device.regstring, "", Encoding.UTF8).Regex;
            }
        }

        private void InitStaticStructures()
        {
            if (clientRegstringList != null)
                return;
            
            clientRegstringList = PrepareRegexpStruct("udger_client_regex");
            osRegstringList = PrepareRegexpStruct("udger_os_regex");
            deviceRegstringList = PrepareRegexpStruct("udger_deviceclass_regex");

            clientWordDetector = createWordDetector("udger_client_regex", "udger_client_regex_words");
            deviceWordDetector = createWordDetector("udger_deviceclass_regex", "udger_deviceclass_regex_words");
            osWordDetector = createWordDetector("udger_os_regex", "udger_os_regex_words"); 
        }

        private List<IdRegString> PrepareRegexpStruct(string regexpTableName)
        {
            List<IdRegString> ret = new List<IdRegString>();
            DataTable rs = dt.selectQuery("SELECT rowid, regstring, word_id, word2_id FROM " + regexpTableName + " ORDER BY sequence");
            rs.TableName = regexpTableName;

            foreach (DataRow row in rs.Rows)
            {
                IdRegString irs = new IdRegString();
                irs.id = UdgerParser.ConvertToInt(row["rowid"]);
                irs.wordId1 = UdgerParser.ConvertToInt(row["word_id"]);
                irs.wordId2 = UdgerParser.ConvertToInt(row["word2_id"]);
                string regex = UdgerParser.ConvertToStr(row["regstring"]);

                Regex reg = new Regex(@"^/?(.*?)/si$"); // regConv = new PerlRegExpConverter(, "", Encoding.Unicode);
                if (reg.IsMatch(regex))
                    regex = reg.Match(regex).Groups[0].ToString();
                irs.pattern = regex; //Pattern.compile(regex, Pattern.CASE_INSENSITIVE | Pattern.DOTALL);
                ret.Add(irs);
            }
            return ret;
        }

        public UserAgent Parse(string ua)
        {         
            if (ua != "")
            {
                if (!(UseCache && Cache.TryGetValue(ua, out UserAgent userAgent))) {
                    userAgent = new UserAgent();
                    parseUA(ua.Replace("'", "''"), ref userAgent);
                }

                userAgent.Hits++;
                return userAgent;
            }
            return null;
        }

        public IPAddress ParseIPAddress(string ip)
        {
            IPAddress ipAddress = new IPAddress();
            if (ip != "")
            {
                parseIP(ip.Replace("'", "''"), ref ipAddress);
            }

            return ipAddress;
        }

        #region parse
        private void parseUA(string userAgentString, ref UserAgent userAgent)
        {
            userAgent.Ua = userAgentString;
            int client_id = 0;
            int client_class_id = -1;
            int os_id = 0;

            if (!string.IsNullOrEmpty(userAgentString))
            {
                userAgent.UaClass = "Unrecognized";
                userAgent.UaClassCode = "unrecognized";

                if (dt.Connected)
                {
                    ////Client
                    processClient(userAgentString, ref os_id, ref client_id, ref client_class_id, ref userAgent);
                    ////OS
                    processOS(userAgentString, ref os_id, client_id, ref userAgent);
                    //// deviceColumn
                    processDevice(userAgentString, ref client_class_id, ref userAgent);

                    if (string.IsNullOrEmpty(userAgent.OsFamilyCode))
                        processDeviceBrand(ref userAgent);

                    //set Cache
                    if (this.UseCache)
                        Cache.TryAdd(userAgentString, userAgent);
                }
            }
        }

        private void parseIP(string _ip, ref IPAddress ipAddress)
        {
            if (!string.IsNullOrEmpty(_ip))
            {
                ipAddress.Ip = _ip;

                if (dt.Connected)
                {
                    int ipVer = getIPAddressVersion(_ip, out string ipLoc);
                    if (ipVer != 0)
                    {
                        if (ipLoc != "")
                            _ip = ipLoc;

                        ipAddress.IpVer = UdgerParser.ConvertToStr(ipVer);

                        if(conDir_IP_TABLE != null && conDir_IP_TABLE.TryGetValue(_ip, out DataRow dataRow)) 
                        { 
                            prepareIp(dataRow, ref ipAddress);
                        }
                        else
                        {
                            DataTable ipTable = dt.selectQuery(UdgerSqlQuery.SQL_IP_TABLE + "  WHERE ip=" + '"' + _ip + '"' + " ORDER BY sequence");
                            if (ipTable != null && ipTable.Rows.Count > 0)
                                prepareIp(ipTable.Rows[0], ref ipAddress);
                        }  
                        
                        if (ipVer == 4)
                        {
                            long ipLong = this.AddrToInt(_ip);//ip2Long.Address;

                            DataTable dataCenter = dt.selectQuery(UdgerSqlQuery.SQL_DATACENTER_TABLE + " WHERE iplong_from <= " + ipLong.ToString() + " AND iplong_to >=" + ipLong.ToString());

                            if (dataCenter != null && dataCenter.Rows.Count > 0)
                            {
                                prepareIpDataCenter(dataCenter.Rows[0], ref ipAddress);
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region process methods
        private void processOS(string uaString, ref int os_id, int clientId, ref UserAgent userAgent)
        {
            int rowId = findIdFromList(uaString, osWordDetector.findWords(uaString), osRegstringList);
            if (rowId != -1)
            {
                conDir_SQL_OS.TryGetValue(rowId.ToString(), out OS os);
                prepareOs(os, ref userAgent);
            }
            else if (clientId != 0)
            {
                if (conDir_SQL_CLIENT_OS.TryGetValue(clientId.ToString(), out OS os))
                    prepareOs(os, ref userAgent);
            }
        }

        private void processClient(string uaString, ref int os_id, ref int clientId, ref int classId, ref UserAgent userAgent)
        {     
            if (conDir_SQL_CRAWLER.TryGetValue(uaString, out Client crawlerClient))
            {
                prepareUa(crawlerClient, true, ref clientId, ref classId, ref userAgent);
                classId = 99;
                clientId = -1;
            }
            else
            {
                int rowId = findIdFromList(uaString, clientWordDetector.findWords(uaString), clientRegstringList);
                if (rowId != -1)
                {
                    conDir_SQL_CLIENT.TryGetValue(rowId.ToString(), out Client client);
                    prepareUa(client, false, ref clientId, ref classId, ref userAgent);
                    //patchVersions(ret);
                }
                else
                {
                    userAgent.UaClass = "Unrecognized";
                    userAgent.UaClassCode = "unrecognized";
                }
            }
        }

        private void processDevice(string uaString, ref int classId, ref UserAgent userAgent)
        {

            int rowId = findIdFromList(uaString, deviceWordDetector.findWords(uaString), deviceRegstringList);
            if (rowId != -1)
            {
                conDir_SQL_DEVICE.TryGetValue(rowId.ToString(), out DeviceColumn device);
                PrepareDevice(device, ref userAgent);
            }
            else if (classId != -1)
            {
                conDir_SQL_CLIENT_CLASS.TryGetValue(classId.ToString(), out DeviceColumn device);
                if (device != null)
                {
                    PrepareDevice(device, ref userAgent);
                }  
            }
        }

        private void processDeviceBrand(ref UserAgent userAgent)
        {
            var osFamilyCode = userAgent.OsFamilyCode;
            var osCode = userAgent.OsCode;
            List<DeviceRegex> deviceRegexs = conBag_SQL_DEVICE_REGEX.Select(x => x).Where(x => x.os_family_code == osFamilyCode && (x.os_code == osCode || x.os_code == "-all-")).ToList();

            if (deviceRegexs.Count > 0)
            {
                foreach (DeviceRegex deviceRegex in deviceRegexs)
                {
                    if (deviceRegex.regstring != null && deviceRegex.Reg.IsMatch(userAgent.Ua))
                    {
                        string code = deviceRegex.Reg.Match(userAgent.Ua).Groups[1].ToString();
                        List<DeviceName> deviceNames = conBag_DeviceName_List.Select(x => x).Where(x => x.regex_id == deviceRegex.id && x.code == code).ToList();
                        List<DeviceBrand> deviceBrands = (deviceNames.Count > 0) ? conBag_DeviceName_Brand.Select(x => x).Where(x => x.id == deviceNames.First().brand_id).ToList() : null;

                        if (deviceNames.Count > 0)
                        {
                            userAgent.DeviceMarketname = deviceNames.First().marketname;
                            userAgent.DeviceBrand = deviceBrands.First().brand;
                            userAgent.DeviceBrandCode = deviceBrands.First().brand_code;
                            //userAgent.DeviceBrandHomepage = deviceBrands.First().brand_url;
                            //userAgent.DeviceBrandIcon = deviceBrands.First().icon;
                            //userAgent.DeviceBrandIconBig = deviceBrands.First().icon_big;
                            break;
                        }
                    }               
                }
            }
        }
        #endregion

        #region prepare data methods
        private void prepareUa(Client client, bool crawler, ref int clientId, ref int classId, ref UserAgent userAgent)
        {
            //userAgent.Ua = UdgerParser.ConvertToStr(_row["ua"]);
            userAgent.UaVersion = client.ua_version;
            userAgent.UaVersionMajor = client.ua_version_major;
            if (!crawler)
            {
                if (client.Reg != null)
                {
                    Group group;
                    if (client.Reg.IsMatch(userAgent.Ua) && (group = client.Reg.Match(userAgent.Ua).Groups[1]).Length > 0)
                    {

                        userAgent.Ua = UdgerParser.ConvertToStr(client.ua) + " " + UdgerParser.ConvertToStr(group);
                        userAgent.UaVersion = UdgerParser.ConvertToStr(group);
                        userAgent.UaVersionMajor = UdgerParser.ConvertToStr(group).Split('.')[0];
                    }
                }
            }

            clientId = client.client_id;
            classId = client.class_id;
            userAgent.CrawlerCategory = client.crawler_category;
            userAgent.CrawlerCategoryCode = client.crawler_category_code;
            userAgent.CrawlerLastSeen = client.crawler_last_seen;
            userAgent.CrawlerRespectRobotstxt = client.crawler_respect_robotstxt;
            userAgent.UaString = userAgent.Ua;
            userAgent.UaClass = client.ua_class;
            userAgent.UaClassCode = client.ua_class_code;
            userAgent.UaUptodateCurrentVersion = client.ua_uptodate_current_version;
            userAgent.UaFamily = client.ua_family;
            userAgent.UaFamilyCode = client.ua_family_code;
            //userAgent.UaFamilyHompage = client.ua_family_homepage;
            userAgent.UaFamilyVendor = client.ua_family_vendor;
            userAgent.UaFamilyVendorCode = client.ua_family_vendor_code;
            //userAgent.UaFamilyVendorHomepage = client.ua_family_vendor_homepage;
            //userAgent.UaFamilyIcon = client.ua_family_icon;
            //userAgent.UaFamilyIconBig = client.ua_family_icon_big;
            userAgent.UaEngine = client.ua_engine;
        }

        private void prepareOs(OS os, ref UserAgent userAgent)
        {
            userAgent.Os = os.os;
            userAgent.OsCode = os.os_code;
            //userAgent.OsHomepage = os.os_home_page;
            //userAgent.OsIcon = os.os_icon;
            //userAgent.OsIconBig = os.os_icon_big;
            userAgent.OsFamily = os.os_family;
            userAgent.OsFamilyCode = os.os_family_code;
            userAgent.OsFamilyVendor = os.os_family_vendor;
            userAgent.OsFamilyVendorCode = os.os_family_vendor_code;
            //userAgent.OsFamilyVendorHomepage = os.os_family_vedor_homepage;
        }

        private void PrepareDevice(DeviceColumn deviceColumn, ref UserAgent userAgent)
        {
            userAgent.DeviceClass = deviceColumn.device_class;
            userAgent.DeviceClassCode = deviceColumn.device_class_code;
            //userAgent.DeviceClassIcon = deviceColumn.device_class_icon;
            //userAgent.DeviceClassIconBig = deviceColumn.device_class_icon_big;
        }

        private void prepareIp(DataRow _row, ref IPAddress ipAddress)
        {
            ipAddress.IpClassification = UdgerParser.ConvertToStr(_row["ip_classification"]);
            ipAddress.IpClassificationCode = UdgerParser.ConvertToStr(_row["ip_classification_code"]);
            ipAddress.IpLastSeen = UdgerParser.ConvertToStr(_row["ip_last_seen"]);
            ipAddress.IpHostname = UdgerParser.ConvertToStr(_row["ip_hostname"]);
            ipAddress.IpCountry = UdgerParser.ConvertToStr(_row["ip_country"]);
            ipAddress.IpCountryCode = UdgerParser.ConvertToStr(_row["ip_country_code"]);
            ipAddress.IpCity = UdgerParser.ConvertToStr(_row["ip_city"]);
            ipAddress.CrawlerName = UdgerParser.ConvertToStr(_row["name"]);
            ipAddress.CrawlerVer = UdgerParser.ConvertToStr(_row["ver"]);
            ipAddress.CrawlerVerMajor = UdgerParser.ConvertToStr(_row["ver_major"]);
            ipAddress.CrawlerFamily = UdgerParser.ConvertToStr(_row["family"]);
            ipAddress.CrawlerFamilyCode = UdgerParser.ConvertToStr(_row["family_code"]);
            ipAddress.CrawlerFamilyHomepage = UdgerParser.ConvertToStr(_row["family_homepage"]);
            ipAddress.CrawlerFamilyVendor = UdgerParser.ConvertToStr(_row["vendor"]);
            ipAddress.CrawlerFamilyVendorCode = UdgerParser.ConvertToStr(_row["vendor_code"]);
            ipAddress.CrawlerFamilyVendorHomepage = UdgerParser.ConvertToStr(_row["vendor_homepage"]);
            ipAddress.CrawlerFamilyIcon = UdgerParser.ConvertToStr(_row["family_icon"]);
            ipAddress.CrawlerLastSeen = UdgerParser.ConvertToStr(_row["last_seen"]);
            ipAddress.CrawlerCategory = UdgerParser.ConvertToStr(_row["crawler_classification"]);
            ipAddress.CrawlerCategoryCode = UdgerParser.ConvertToStr(_row["crawler_classification_code"]);
            if (ipAddress.IpClassificationCode == "crawler")
                ipAddress.CrawlerFamilyInfoUrl = "https://udger.com/resources/ua-list/bot-detail?bot=" + UdgerParser.ConvertToStr(_row["family"]) + "#id" + UdgerParser.ConvertToStr(_row["botid"]);
            ipAddress.CrawlerRespectRobotstxt = UdgerParser.ConvertToStr(_row["respect_robotstxt"]);
        }

        private void prepareIpDataCenter(DataRow _row, ref IPAddress ipAddress)
        {
            ipAddress.DatacenterName = UdgerParser.ConvertToStr(_row["name"]);
            ipAddress.DatacenterNameCode = UdgerParser.ConvertToStr(_row["name_code"]);
            ipAddress.DatacenterHomepage = UdgerParser.ConvertToStr(_row["homepage"]);
        }
        #endregion

        #region database convertors
        private static string ConvertToStr(object value)
        {
            if (value == null || value.GetType() == typeof(DBNull))
                return "";
            return value.ToString();
        }

        private static int ConvertToInt(object value)
        {
            if (value == null || value.GetType() == typeof(DBNull))
                return 0;
            return Convert.ToInt32(value);
        }
        private static DateTime ConvertToDateTime(string value)
        {
            DateTime dt;
            DateTime.TryParse(value, out dt);

            return dt;
        }
        #endregion

        private int getIPAddressVersion(string _ip, out string _retIp)
        {
            System.Net.IPAddress addr;
            _retIp = "";

            if (System.Net.IPAddress.TryParse(_ip, out addr))
            {
                _retIp = addr.ToString();
                if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return 4;
                if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    return 6;
            }

            return 0;
        }

        private long AddrToInt(string addr)
        {
            return (long)(uint)System.Net.IPAddress.NetworkToHostOrder(
                 (int)System.Net.IPAddress.Parse(addr).Address);
        }

        private static WordDetector createWordDetector(string regexTableName, string wordTableName)
        {
            HashSet<int> usedWords = new HashSet<int>();

            addUsedWords(usedWords, regexTableName, "word_id");
            addUsedWords(usedWords, regexTableName, "word2_id");

            WordDetector result = new WordDetector();

            DataTable dtResult = dt.selectQuery("SELECT * FROM " + wordTableName);
            if (dtResult != null)
            {
                foreach (DataRow row in dtResult.Rows)
                {
                    int id = UdgerParser.ConvertToInt(row["id"]);
                    if (usedWords.Contains(id))
                    {
                        String word = UdgerParser.ConvertToStr(row["word"]).ToLower();
                        result.addWord(id, word);
                    }
                }
            }
            return result;
        }

        private static void addUsedWords(HashSet<int> usedWords, string regexTableName, string wordIdColumn) 
        {
                DataTable rs = dt.selectQuery("SELECT " + wordIdColumn + " FROM " + regexTableName);
                if (rs != null)
                {
                    foreach (DataRow row in rs.Rows)
                    {
                        usedWords.Add(UdgerParser.ConvertToInt(row[wordIdColumn]));
                    }
                }
        }

        private int findIdFromList(String uaString, HashSet<int> foundClientWords, List<IdRegString> list)
        {
            Regex searchTerm;
            PerlRegExpConverter regConv;

            foreach (IdRegString irs in list)
            {
                if ((irs.wordId1 == 0 || foundClientWords.Contains(irs.wordId1)) &&
                    (irs.wordId2 == 0 || foundClientWords.Contains(irs.wordId2)))
                {
                    regConv = new PerlRegExpConverter(irs.pattern, "", Encoding.UTF8);
                    searchTerm = regConv.Regex;
                    if (searchTerm.IsMatch(uaString))
                    {
                        //lastPatternMatcher = irs.pattern;
                        return irs.id;
                    }
                }
            }
            return -1;
        }

    }
}