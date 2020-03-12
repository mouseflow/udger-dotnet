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
using System.Collections.Concurrent;
using System.Text;
using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mouseflow.Udger.Parser
{
    public class UdgerParser
    {
        public IPAddress ipAddress { get; private set; }

        private struct IdRegString
        {
            public int id;
            public int wordId1;
            public int wordId2;
            public string pattern;
        }

        private LRUCache<string, UserAgent> cache;
        private bool useCache;
        
        private static DataReader dt;
        private static WordDetector clientWordDetector;
        private static WordDetector deviceWordDetector;
        private static WordDetector osWordDetector;
        private static List<IdRegString> clientRegstringList;
        private static List<IdRegString> osRegstringList;
        private static List<IdRegString> deviceRegstringList;

        private readonly Dictionary<string, Regex> regexCache = new Dictionary<string, Regex>();
        private readonly Dictionary<string, string> preparedStmtMap = new Dictionary<string, string>();

        public DateTime DataLoadTime;
        public int CacheSize => cache.CacheSize;
        public bool IsDataLoaded { get; private set; } = false;

        private ConcurrentDictionary<string, DataRow> conDir_SQL_CRAWLER,
                                                      conDir_SQL_CLIENT,
                                                      conDir_SQL_OS,
                                                      conDir_SQL_CLIENT_OS,
                                                      conDir_SQL_DEVICE,
                                                      conDir_SQL_CLIENT_CLASS;
 
        public UdgerParser(bool useLRUCash = true, int LRUCashCapacity = 100000)
        {
            dt = new DataReader();
            if (useLRUCash)
                cache = new LRUCache<string, UserAgent>(LRUCashCapacity);
            useCache = useLRUCash;
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

        public void LoadData()
        {
            if (!dt.Connected)
                dt.Connect();
            InitStaticStructures();
            
            conDir_SQL_CLIENT_CLASS = dt.SelectQuery(UdgerSqlQuery.SQL_CLIENT_CLASS, 0);
            conDir_SQL_CRAWLER = dt.SelectQuery(UdgerSqlQuery.SQL_CRAWLER, 0);
            conDir_SQL_CLIENT = dt.SelectQuery(UdgerSqlQuery.SQL_CLIENT, 0);
            conDir_SQL_OS = dt.SelectQuery(UdgerSqlQuery.SQL_OS, 0);
            conDir_SQL_CLIENT_OS = dt.SelectQuery(UdgerSqlQuery.SQL_CLIENT_OS, 0);
            conDir_SQL_DEVICE = dt.SelectQuery(UdgerSqlQuery.SQL_DEVICE, 0);

            DataLoadTime = DateTime.Now;
            IsDataLoaded = true;
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

            if (rs != null)
            {
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
            }

            return ret;
        }

        public UserAgent Parse(string ua)
        {
            //ipAddress = new IPAddress();
            UserAgent userAgent = null;

            if (IsDataLoaded && ua != "")
            {

                if (useCache && cache.TryGetValue(ua, out userAgent)) { }
                else
                {
                    userAgent = new UserAgent();
                    parseUA(ua.Replace("'", "''"), ref userAgent);
                }
                
                //if (this.ip != "")
                //{
                //    this.parseIP(this.ip.Replace("'", "''"));
                //    this.ip = "";
                //}
            }
            return userAgent;
        }

        private void parseUA(string _userAgent, ref UserAgent userAgent)
        {
            int client_id = 0;
            int client_class_id = -1;
            int os_id = 0;

            if (!string.IsNullOrEmpty(_userAgent))
            {
                userAgent.UaClass = "Unrecognized";
                userAgent.UaClassCode = "unrecognized";

                if (dt.Connected)
                {
                    ////Client
                    processClient(_userAgent, ref os_id, ref client_id, ref client_class_id, ref userAgent);
                    ////OS
                    processOS(_userAgent, ref os_id, client_id, ref userAgent);
                    //// device
                    processDevice(_userAgent, ref client_class_id, ref userAgent);

                    //if (userAgent.OsFamilyCode != null && userAgent.OsFamilyCode != "")
                    //    processDeviceBrand();

                    //set cache
                    if (this.useCache)
                        cache.Set(_userAgent, userAgent);
                }
            }
        }

        //private void parseIP(string _ip)
        //{
        //    string ipLoc;
        //    if (!string.IsNullOrEmpty(_ip))
        //    {
        //        ipAddress.Ip = this.ip;

        //        if (dt.Connected)
        //        {
        //            int ipVer = this.getIPAddressVersion(ip, out ipLoc);
        //            if (ipVer != 0)
        //            {
        //                if (ipLoc != "")
        //                    _ip = ipLoc;

        //                ipAddress.IpVer = UdgerParser.ConvertToStr(ipVer);

        //                DataTable ipTable = dt.SelectQuery(@"SELECT udger_crawler_list.id as botid,ip_last_seen,ip_hostname,ip_country,ip_city,ip_country_code,ip_classification,ip_classification_code,
        //                                  name,ver,ver_major,last_seen,respect_robotstxt,family,family_code,family_homepage,family_icon,vendor,vendor_code,vendor_homepage,crawler_classification,crawler_classification_code,crawler_classification
        //                                  FROM udger_ip_list
        //                                  JOIN udger_ip_class ON udger_ip_class.id=udger_ip_list.class_id
        //                                  LEFT JOIN udger_crawler_list ON udger_crawler_list.id=udger_ip_list.crawler_id
        //                                  LEFT JOIN udger_crawler_class ON udger_crawler_class.id=udger_crawler_list.class_id
        //                                  WHERE ip=" + '"' + _ip + '"' + " ORDER BY sequence");

        //                if (ipTable != null && ipTable.Rows.Count > 0)
        //                {
        //                    this.prepareIp(ipTable.Rows[0]);
        //                }
        //                if (ipVer == 4)
        //                {
        //                    long ipLong = this.AddrToInt(_ip);//ip2Long.Address;

        //                    DataTable dataCenter = dt.SelectQuery(@"select name, name_code, homepage
        //                               FROM udger_datacenter_range
        //                               JOIN udger_datacenter_list ON udger_datacenter_range.datacenter_id = udger_datacenter_list.id
        //                               where iplong_from <= " + ipLong.ToString() + " AND iplong_to >=" + ipLong.ToString());

        //                    if (dataCenter != null && dataCenter.Rows.Count > 0)
        //                    {
        //                        this.prepareIpDataCenter(dataCenter.Rows[0]);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        private void processOS(string uaString, ref int os_id, int clientId, ref UserAgent userAgent)
        {
            DataRow dataRow;
            int rowId = findIdFromList(uaString, osWordDetector.findWords(uaString), osRegstringList);
            if (rowId != -1)
            {
                conDir_SQL_OS.TryGetValue(rowId.ToString(), out dataRow);
                prepareOs(dataRow, ref os_id, ref userAgent);
            }
            else if (clientId != 0)
            {
                conDir_SQL_CLIENT_OS.TryGetValue(clientId.ToString(), out dataRow);
                if (dataRow != null)
                    prepareOs(dataRow, ref os_id, ref userAgent);
            }
        }

        private void processClient(string uaString, ref int os_id, ref int clientId, ref int classId, ref UserAgent userAgent)
        {
            DataRow dataRow;
            conDir_SQL_CRAWLER.TryGetValue(uaString, out dataRow);

            if (dataRow != null)
            {
                this.prepareUa(dataRow, true, ref clientId, ref classId, ref userAgent);
                classId = 99;
                clientId = -1;
            }
            else
            {
                int rowId = findIdFromList(uaString, clientWordDetector.findWords(uaString), clientRegstringList);
                if (rowId != -1)
                {
                    conDir_SQL_CLIENT.TryGetValue(rowId.ToString(), out dataRow);
                    prepareUa(dataRow, false, ref clientId, ref classId, ref userAgent);
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
            DataRow dataRow;
            int rowId = findIdFromList(uaString, deviceWordDetector.findWords(uaString), deviceRegstringList);
            if (rowId != -1)
            {
                conDir_SQL_DEVICE.TryGetValue(rowId.ToString(), out dataRow);
                PrepareDevice(dataRow, ref classId, ref userAgent);
            }
            else
            {
                if (classId != -1)
                {
                    conDir_SQL_CLIENT_CLASS.TryGetValue(classId.ToString(), out dataRow);
                    if (dataRow != null)
                    {
                        PrepareDevice(dataRow, ref classId, ref userAgent);
                    }
                }
            }
        }

        //private void processDeviceBrand()
        //{
        //    Regex reg;
        //    PerlRegExpConverter regConv;

        //    DataTable devRs = dt.selectQuery(String.Format(UdgerSqlQuery.SQL_DEVICE_REGEX, this.userAgent.OsFamilyCode, this.userAgent.OsCode));
        //    if (devRs != null && devRs.Rows.Count > 0)
        //    {
        //        foreach (DataRow row in devRs.Rows)
        //        {
        //            String devId = UdgerParser.ConvertToStr(row["id"]);
        //            String regex = UdgerParser.ConvertToStr(row["regstring"]);
        //            if (devId != null && regex != null)
        //            {
        //                regConv = new PerlRegExpConverter(regex, "", Encoding.UTF8);
        //                reg = regConv.Regex;
        //                if (reg.IsMatch(this.ua))
        //                {
        //                    string foo = reg.Match(this.ua).Groups[1].ToString();
        //                    DataTable devNameListRs = dt.selectQuery(String.Format(UdgerSqlQuery.SQL_DEVICE_NAME_LIST, devId, foo));
        //                    if (devNameListRs != null && devNameListRs.Rows.Count > 0)
        //                    {
        //                        DataRow r = devNameListRs.Rows[0];
        //                        userAgent.DeviceMarketname = UdgerParser.ConvertToStr(r["marketname"]);
        //                        userAgent.DeviceBrand = UdgerParser.ConvertToStr(r["brand"]);
        //                        userAgent.DeviceBrandCode = UdgerParser.ConvertToStr(r["brand_code"]);
        //                        userAgent.DeviceBrandHomepage = UdgerParser.ConvertToStr(r["brand_url"]);
        //                        userAgent.DeviceBrandIcon = UdgerParser.ConvertToStr(r["icon"]);
        //                        userAgent.DeviceBrandIconBig = UdgerParser.ConvertToStr(r["icon_big"]);
        //                        userAgent.DeviceBrandInfoUrl = @"https://udger.com/resources/ua-list/devices-brand-detail?brand=" + UdgerParser.ConvertToStr(r["brand_code"]);
        //                        break;
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}
        private void prepareUa(DataRow _row, Boolean crawler, ref int clientId, ref int classId, ref UserAgent userAgent)
        {
            Regex searchTerm;
            PerlRegExpConverter regConv;
            Group group;

            userAgent.Ua = UdgerParser.ConvertToStr(_row["ua"]);
            userAgent.UaVersion = UdgerParser.ConvertToStr(_row["ua_version"]);
            userAgent.UaVersionMajor = UdgerParser.ConvertToStr(_row["ua_version_major"]);
            if (!crawler)
            {
                string pattern = UdgerParser.ConvertToStr(_row["regstring"]);
                if (pattern != "")
                {
                    regConv = new PerlRegExpConverter(pattern, "", Encoding.UTF8);
                    searchTerm = regConv.Regex;
                    if (searchTerm.IsMatch(userAgent.Ua) && (group = searchTerm.Match(userAgent.Ua).Groups[1]) != null)
                    {

                        userAgent.Ua = UdgerParser.ConvertToStr(_row["ua"]) + " " + UdgerParser.ConvertToStr(group);
                        userAgent.UaVersion = UdgerParser.ConvertToStr(group);
                        userAgent.UaVersionMajor = UdgerParser.ConvertToStr(group).Split('.')[0];
                    }
                }
            }
            clientId = UdgerParser.ConvertToInt(_row["client_id"]);
            classId = UdgerParser.ConvertToInt(_row["class_id"]);
            userAgent.CrawlerCategory = UdgerParser.ConvertToStr(_row["crawler_category"]);
            userAgent.CrawlerCategoryCode = UdgerParser.ConvertToStr(_row["crawler_category_code"]);
            userAgent.CrawlerLastSeen = UdgerParser.ConvertToStr(_row["crawler_last_seen"]);
            userAgent.CrawlerRespectRobotstxt = UdgerParser.ConvertToStr(_row["crawler_respect_robotstxt"]);
            userAgent.UaString = userAgent.Ua;
            userAgent.UaClass = UdgerParser.ConvertToStr(_row["ua_class"]);
            userAgent.UaClassCode = UdgerParser.ConvertToStr(_row["ua_class_code"]);
            userAgent.UaUptodateCurrentVersion = UdgerParser.ConvertToStr(_row["ua_uptodate_current_version"]);
            userAgent.UaFamily = UdgerParser.ConvertToStr(_row["ua_family"]);
            userAgent.UaFamilyCode = UdgerParser.ConvertToStr(_row["ua_family_code"]);
            userAgent.UaFamilyHompage = UdgerParser.ConvertToStr(_row["ua_family_homepage"]);
            userAgent.UaFamilyVendor = UdgerParser.ConvertToStr(_row["ua_family_vendor"]);
            userAgent.UaFamilyVendorCode = UdgerParser.ConvertToStr(_row["ua_family_vendor_code"]);
            userAgent.UaFamilyVendorHomepage = UdgerParser.ConvertToStr(_row["ua_family_vendor_homepage"]);
            userAgent.UaFamilyIcon = UdgerParser.ConvertToStr(_row["ua_family_icon"]);
            userAgent.UaFamilyIconBig = UdgerParser.ConvertToStr(_row["ua_family_icon_big"]);
            userAgent.UaEngine = UdgerParser.ConvertToStr(_row["ua_engine"]);
        }

        private void prepareOs(DataRow _row, ref int _osId, ref UserAgent userAgent)
        {
            //_osId = Convert.ToInt32(_row["os_id"]);
            userAgent.Os = UdgerParser.ConvertToStr(_row["os"]);
            userAgent.OsCode = UdgerParser.ConvertToStr(_row["os_code"]);
            userAgent.OsHomepage = UdgerParser.ConvertToStr(_row["os_home_page"]);
            userAgent.OsIcon = UdgerParser.ConvertToStr(_row["os_icon"]);
            userAgent.OsIconBig = UdgerParser.ConvertToStr(_row["os_icon_big"]);
            userAgent.OsInfoUrl = UdgerParser.ConvertToStr(_row["os_info_url"]);
            userAgent.OsFamily = UdgerParser.ConvertToStr(_row["os_family"]);
            userAgent.OsFamilyCode = UdgerParser.ConvertToStr(_row["os_family_code"]);
            userAgent.OsFamilyVendor = UdgerParser.ConvertToStr(_row["os_family_vendor"]);
            userAgent.OsFamilyVendorCode = UdgerParser.ConvertToStr(_row["os_family_vendor_code"]);
            userAgent.OsFamilyVendorHomepage = UdgerParser.ConvertToStr(_row["os_family_vedor_homepage"]);

        }

        private void PrepareDevice(DataRow _row, ref int _deviceClassId, ref UserAgent userAgent)
        {
            //_deviceClassId = Convert.ToInt32(_row["device_class"]);
            userAgent.DeviceClass = UdgerParser.ConvertToStr(_row["device_class"]);
            userAgent.DeviceClassCode = UdgerParser.ConvertToStr(_row["device_class_code"]);
            userAgent.DeviceClassIcon = UdgerParser.ConvertToStr(_row["device_class_icon"]);
            userAgent.DeviceClassIconBig = UdgerParser.ConvertToStr(_row["device_class_icon_big"]);
            userAgent.DeviceClassInfoUrl =  UdgerParser.ConvertToStr(_row["device_class_info_url"]);
        }

        private void prepareIp(DataRow _row)
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

        private void prepareIpDataCenter(DataRow _row)
        {
            ipAddress.DatacenterName = UdgerParser.ConvertToStr(_row["name"]);
            ipAddress.DatacenterNameCode = UdgerParser.ConvertToStr(_row["name_code"]);
            ipAddress.DatacenterHomepage = UdgerParser.ConvertToStr(_row["homepage"]);
        }
 
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