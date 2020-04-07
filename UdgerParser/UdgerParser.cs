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
using System.Text;
using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Udger.Parser
{
    public class UdgerParser
    {
        #region private Variables
        private struct IdRegString
        {
            public int Id;
            public int WordId1;
            public int WordId2;
            public string Pattern;
        }

        private readonly LRUCache<string, UserAgent> cache;
        private readonly bool useCache;
        private readonly DataReader db;
        private bool connected;

        private static WordDetector clientWordDetector;
        private static WordDetector deviceWordDetector;
        private static WordDetector osWordDetector;

        private static List<IdRegString> clientRegstringList;
        private static List<IdRegString> osRegstringList;
        private static List<IdRegString> deviceRegstringList;
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="useLRUCache">bool eneble/disable LRUCash</param>
        /// <param name="cacheCapacity">int LRUCash Capacity (minimum is 1)</param>
        public UdgerParser(bool useLRUCache = true, int cacheCapacity = 10000)
        {
            db = new DataReader();
            useCache = useLRUCache;
            if (useLRUCache)
                cache = new LRUCache<string, UserAgent>(cacheCapacity);
        }
        #endregion

        #region setParser method
        /// <summary>
        /// Set the data directory and DB filename
        /// </summary> 
        /// <param name="dataDir">string path udger DB directory</param>
        /// <param name="fileName">string path udger DB filename</param>
        public void SetDataDir(string dataDir, string fileName = "udgerdb_v3.dat")
        {
            if (!Directory.Exists(dataDir))
                throw new Exception("Data dir not found");

            db.DataSourcePath = $@"{dataDir}\{fileName}";

            if (!File.Exists(db.DataSourcePath))
                throw new Exception($"Data file {fileName} not found");

            InitStaticStructures(db);

            connected = true;
        }
        #endregion

        #region public method
        /// <summary>
        /// Parse the useragent string and/or ip address
        /// /// </summary>
        public UserAgent ParseUserAgent(string ua)
        {
            if (useCache && cache.TryGetValue(ua, out var userAgent))
                return userAgent;

            return ParseUa(ua.Replace("'", "''"));
        }

        /// <summary>
        /// Parse the useragent string and/or ip address
        /// /// </summary>
        public IpAddress ParseIpAddress(string ip)
        {
            return ParseIp(ip.Replace("'", "''"));
        }
        #endregion

        #region private method

        #region parse
        private UserAgent ParseUa(string uaString)
        {
            var userAgent = new UserAgent();
            var clientId = 0;
            var clientClassId = -1;

            if (string.IsNullOrEmpty(uaString))
                return userAgent;

            userAgent.UaString = uaString;
            userAgent.UaClass = "Unrecognized";
            userAgent.UaClassCode = "unrecognized";

            if (!connected)
                return userAgent;

            ProcessClient(userAgent, uaString, ref clientId, ref clientClassId);
            ProcessOs(userAgent, uaString, clientId);
            ProcessDevice(userAgent, uaString, clientClassId);

            if (!string.IsNullOrEmpty(userAgent.OsFamilyCode))
                ProcessDeviceBrand(userAgent, uaString);

            if (useCache)
                cache.Set(uaString, userAgent);

            return userAgent;
        }

        private IpAddress ParseIp(string ip)
        {
            var ipAddress = new IpAddress();

            if (string.IsNullOrEmpty(ip))
                return ipAddress;

            ipAddress.Ip = ip;

            if (!connected)
                return ipAddress;

            var ipVer = GetIpAddressVersion(ip, out var ipLoc);
            if (ipVer == 0)
                return ipAddress;

            if (ipLoc != "")
                ip = ipLoc;

            ipAddress.IpVer = ConvertToStr(ipVer);

            var ipTable = db.SelectQuery(@"SELECT udger_crawler_list.id as botid,ip_last_seen,ip_hostname,ip_country,ip_city,ip_country_code,ip_classification,ip_classification_code,
                name,ver,ver_major,last_seen,respect_robotstxt,family,family_code,family_homepage,family_icon,vendor,vendor_code,vendor_homepage,crawler_classification,crawler_classification_code,crawler_classification
                FROM udger_ip_list
                JOIN udger_ip_class ON udger_ip_class.id=udger_ip_list.class_id
                LEFT JOIN udger_crawler_list ON udger_crawler_list.id=udger_ip_list.crawler_id
                LEFT JOIN udger_crawler_class ON udger_crawler_class.id=udger_crawler_list.class_id
                WHERE ip=" + '"' + ip + '"' + " ORDER BY sequence");

            if (ipTable != null && ipTable.Rows.Count > 0)
                PrepareIp(ipAddress, ipTable.Rows[0]);

            if (ipVer != 4)
                return ipAddress;

            var ipLong = IpToInt(ip);

            var dataCenter = db.SelectQuery(@"select name, name_code, homepage
                FROM udger_datacenter_range
                JOIN udger_datacenter_list ON udger_datacenter_range.datacenter_id = udger_datacenter_list.id
                where iplong_from <= " + ipLong + " AND iplong_to >=" + ipLong);

            if (dataCenter != null && dataCenter.Rows.Count > 0)
                PrepareIpDataCenter(ipAddress, dataCenter.Rows[0]);

            return ipAddress;
        }
        #endregion

        #region process methods

        private void ProcessOs(UserAgent userAgent, string uaString, int clientId)
        {
            var rowId = FindIdFromList(uaString, osWordDetector.FindWords(uaString), osRegstringList);
            if (rowId != -1)
            {
                var q = string.Format(UdgerSqlQuery.SQL_OS, rowId);
                var opSysRs = db.SelectQuery(q);
                PrepareOs(userAgent, opSysRs.Rows[0]);
            }
            else if(clientId != 0)
            {
                var opSysRs = db.SelectQuery(string.Format(UdgerSqlQuery.SQL_CLIENT_OS, clientId));
                if (opSysRs != null && opSysRs.Rows.Count > 0)
                    PrepareOs(userAgent, opSysRs.Rows[0]);
            }
        }


        private void ProcessClient(UserAgent userAgent, string uaString, ref int clientId, ref int classId)
        {
            var q = string.Format(UdgerSqlQuery.SQL_CRAWLER, uaString);
            var userAgentRs = db.SelectQuery(q);
            if (userAgentRs != null && userAgentRs.Rows.Count > 0 )
            {
                PrepareUa(userAgent, uaString, userAgentRs.Rows[0], true, ref clientId, ref classId);
                classId = 99;
                clientId = -1;
            }
            else
            {
                var rowId = FindIdFromList(uaString, clientWordDetector.FindWords(uaString), clientRegstringList);
                if (rowId != -1)
                {
                    userAgentRs = db.SelectQuery(string.Format(UdgerSqlQuery.SQL_CLIENT, rowId));
                    PrepareUa(userAgent, uaString, userAgentRs.Rows[0], false, ref clientId, ref classId);
                }
                else
                {
                    userAgent.UaClass = "Unrecognized";
                    userAgent.UaClassCode = "unrecognized";
                }
            }
        }

        private void ProcessDevice(UserAgent userAgent, string uaString, int classId)
        {
            var rowId = FindIdFromList(uaString, deviceWordDetector.FindWords(uaString), deviceRegstringList);
            if (rowId != -1)
            {
                var devRs = db.SelectQuery(string.Format(UdgerSqlQuery.SQL_DEVICE, rowId));
                PrepareDevice(userAgent, devRs.Rows[0]);
            }
            else
            {
                if (classId == -1)
                    return;

                var devRs = db.SelectQuery(string.Format(UdgerSqlQuery.SQL_CLIENT_CLASS, classId.ToString()));
                if (devRs != null && devRs.Rows.Count > 0)
                    PrepareDevice(userAgent, devRs.Rows[0]);
            }
        }

        private void ProcessDeviceBrand(UserAgent userAgent, string uaString)
        {
            var devRs = db.SelectQuery(string.Format(UdgerSqlQuery.SQL_DEVICE_REGEX, userAgent.OsFamilyCode, userAgent.OsCode));
            if (devRs == null || devRs.Rows.Count <= 0)
                return;

            foreach (DataRow row in devRs.Rows)
            {
                var devId = ConvertToStr(row["id"]);
                var regex = ConvertToStr(row["regstring"]);
                if (devId == null || regex == null)
                    continue;

                var regConv = new PerlRegExpConverter(regex, "", Encoding.UTF8);
                var reg = regConv.Regex;
                if (!reg.IsMatch(uaString))
                    continue;

                var foo = reg.Match(uaString).Groups[1].ToString();
                var devNameListRs = db.SelectQuery(string.Format(UdgerSqlQuery.SQL_DEVICE_NAME_LIST, devId, foo));
                if (devNameListRs == null || devNameListRs.Rows.Count <= 0)
                    continue;

                var r = devNameListRs.Rows[0];
                userAgent.DeviceMarketname = ConvertToStr(r["marketname"]);
                userAgent.DeviceBrand = ConvertToStr(r["brand"]);
                userAgent.DeviceBrandCode = ConvertToStr(r["brand_code"]);
                userAgent.DeviceBrandHomepage = ConvertToStr(r["brand_url"]);
                userAgent.DeviceBrandIcon = ConvertToStr(r["icon"]);
                userAgent.DeviceBrandIconBig = ConvertToStr(r["icon_big"]);
                userAgent.DeviceBrandInfoUrl = @"https://udger.com/resources/ua-list/devices-brand-detail?brand=" + ConvertToStr(r["brand_code"]);
                break;
            }
        }
        #endregion

        #region prepare data methods

        private void PrepareUa(UserAgent userAgent, string uaString, DataRow row, bool crawler, ref int clientId, ref int classId)
        {
            userAgent.Ua = ConvertToStr(row["ua"]);
            userAgent.UaVersion = ConvertToStr(row["ua_version"]);
            userAgent.UaVersionMajor = ConvertToStr(row["ua_version_major"]);
            userAgent.CrawlerCategory = ConvertToStr(row["crawler_category"]);
            userAgent.CrawlerCategoryCode = ConvertToStr(row["crawler_category_code"]);
            userAgent.CrawlerLastSeen = ConvertToStr(row["crawler_last_seen"]);
            userAgent.CrawlerRespectRobotstxt = ConvertToStr(row["crawler_respect_robotstxt"]);
            userAgent.UaString = uaString;
            userAgent.UaClass = ConvertToStr(row["ua_class"]);
            userAgent.UaClassCode = ConvertToStr(row["ua_class_code"]);
            userAgent.UaUptodateCurrentVersion = ConvertToStr(row["ua_uptodate_current_version"]);
            userAgent.UaFamily = ConvertToStr(row["ua_family"]);
            userAgent.UaFamilyCode = ConvertToStr(row["ua_family_code"]);
            userAgent.UaFamilyHompage = ConvertToStr(row["ua_family_homepage"]);
            userAgent.UaFamilyVendor = ConvertToStr(row["ua_family_vendor"]);
            userAgent.UaFamilyVendorCode = ConvertToStr(row["ua_family_vendor_code"]);
            userAgent.UaFamilyVendorHomepage = ConvertToStr(row["ua_family_vendor_homepage"]);
            userAgent.UaFamilyIcon = ConvertToStr(row["ua_family_icon"]);
            userAgent.UaFamilyIconBig = ConvertToStr(row["ua_family_icon_big"]);
            userAgent.UaFamilyInfoUrl = ConvertToStr(row["ua_family_info_url"]);
            userAgent.UaEngine = ConvertToStr(row["ua_engine"]);

            if (!crawler)
            {
                var pattern = ConvertToStr(row["regstring"]);
                if (pattern != "")
                {
                    var regConv = new PerlRegExpConverter(pattern, "", Encoding.UTF8);
                    var searchTerm = regConv.Regex;
                    Group group;
                    if (searchTerm.IsMatch(uaString) && (group = searchTerm.Match(uaString).Groups[1]) != null)
                    {
                        userAgent.Ua = ConvertToStr(row["ua"]) + " " + ConvertToStr(group);
                        userAgent.UaVersion = ConvertToStr(group);
                        userAgent.UaVersionMajor = ConvertToStr(group).Split('.')[0];
                    }
                }
            }

            clientId = ConvertToInt(row["client_id"]);
            classId =  ConvertToInt(row["class_id"]);
        }

        private void PrepareOs(UserAgent userAgent, DataRow row)
        {
            userAgent.Os = ConvertToStr(row["os"]);
            userAgent.OsCode = ConvertToStr(row["os_code"]);
            userAgent.OsHomepage = ConvertToStr(row["os_home_page"]);
            userAgent.OsIcon = ConvertToStr(row["os_icon"]);
            userAgent.OsIconBig = ConvertToStr(row["os_icon_big"]);
            userAgent.OsInfoUrl = ConvertToStr(row["os_info_url"]);
            userAgent.OsFamily = ConvertToStr(row["os_family"]);
            userAgent.OsFamilyCode = ConvertToStr(row["os_family_code"]);
            userAgent.OsFamilyVendor = ConvertToStr(row["os_family_vendor"]);
            userAgent.OsFamilyVendorCode = ConvertToStr(row["os_family_vendor_code"]);
            userAgent.OsFamilyVendorHomepage = ConvertToStr(row["os_family_vedor_homepage"]);
        }

        private void PrepareDevice(UserAgent userAgent, DataRow row)
        {
            userAgent.DeviceClass = ConvertToStr(row["device_class"]);
            userAgent.DeviceClassCode = ConvertToStr(row["device_class_code"]);
            userAgent.DeviceClassIcon = ConvertToStr(row["device_class_icon"]);
            userAgent.DeviceClassIconBig = ConvertToStr(row["device_class_icon_big"]);
            userAgent.DeviceClassInfoUrl =  ConvertToStr(row["device_class_info_url"]);
        }

        private void PrepareIp(IpAddress ipAddress, DataRow row)
        {
            ipAddress.IpClassification = ConvertToStr(row["ip_classification"]);
            ipAddress.IpClassificationCode = ConvertToStr(row["ip_classification_code"]);
            ipAddress.IpLastSeen = ConvertToStr(row["ip_last_seen"]);
            ipAddress.IpHostname = ConvertToStr(row["ip_hostname"]);
            ipAddress.IpCountry = ConvertToStr(row["ip_country"]);
            ipAddress.IpCountryCode = ConvertToStr(row["ip_country_code"]);
            ipAddress.IpCity = ConvertToStr(row["ip_city"]);
            ipAddress.CrawlerName = ConvertToStr(row["name"]);
            ipAddress.CrawlerVer = ConvertToStr(row["ver"]);
            ipAddress.CrawlerVerMajor = ConvertToStr(row["ver_major"]);
            ipAddress.CrawlerFamily = ConvertToStr(row["family"]);
            ipAddress.CrawlerFamilyCode = ConvertToStr(row["family_code"]);
            ipAddress.CrawlerFamilyHomepage = ConvertToStr(row["family_homepage"]);
            ipAddress.CrawlerFamilyVendor = ConvertToStr(row["vendor"]);
            ipAddress.CrawlerFamilyVendorCode = ConvertToStr(row["vendor_code"]);
            ipAddress.CrawlerFamilyVendorHomepage = ConvertToStr(row["vendor_homepage"]);
            ipAddress.CrawlerFamilyIcon = ConvertToStr(row["family_icon"]);
            ipAddress.CrawlerLastSeen = ConvertToStr(row["last_seen"]);
            ipAddress.CrawlerCategory = ConvertToStr(row["crawler_classification"]);
            ipAddress.CrawlerCategoryCode = ConvertToStr(row["crawler_classification_code"]);
            if (ipAddress.IpClassificationCode == "crawler")
                ipAddress.CrawlerFamilyInfoUrl = "https://udger.com/resources/ua-list/bot-detail?bot=" + ConvertToStr(row["family"]) + "#id" + ConvertToStr(row["botid"]);
            ipAddress.CrawlerRespectRobotstxt = ConvertToStr(row["respect_robotstxt"]);
        }

        private void PrepareIpDataCenter(IpAddress ipAddress, DataRow row)
        {
            ipAddress.DatacenterName = ConvertToStr(row["name"]);
            ipAddress.DatacenterNameCode = ConvertToStr(row["name_code"]);
            ipAddress.DatacenterHomepage = ConvertToStr(row["homepage"]);
        }
        #endregion
 
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

        private int GetIpAddressVersion(string ip, out string retIp)
        {
            retIp = "";

            if (!System.Net.IPAddress.TryParse(ip, out var addr))
                return 0;

            retIp = addr.ToString();
            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return 4;
            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                return 6;

            return 0;
        }

        private long IpToInt(string ip)
        {
            return (uint)System.Net.IPAddress.NetworkToHostOrder(
                (int)System.Net.IPAddress.Parse(ip).Address);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private static void InitStaticStructures(DataReader connection)
        {
            if (clientRegstringList != null)
                return;

            clientRegstringList = PrepareRegexpStruct(connection, "udger_client_regex");
            osRegstringList = PrepareRegexpStruct(connection, "udger_os_regex");
            deviceRegstringList = PrepareRegexpStruct(connection, "udger_deviceclass_regex");

            clientWordDetector = CreateWordDetector(connection, "udger_client_regex", "udger_client_regex_words");
            deviceWordDetector = CreateWordDetector(connection, "udger_deviceclass_regex", "udger_deviceclass_regex_words");
            osWordDetector = CreateWordDetector(connection, "udger_os_regex", "udger_os_regex_words");
        }

        private static WordDetector CreateWordDetector(DataReader connection, string regexTableName, string wordTableName)
        {
            var usedWords = new HashSet<int>();

            AddUsedWords(usedWords, connection, regexTableName, "word_id");
            AddUsedWords(usedWords, connection, regexTableName, "word2_id");

            var result = new WordDetector();

            var dt = connection.SelectQuery("SELECT * FROM " + wordTableName);
            if (dt == null)
                return result;

            foreach (DataRow row in dt.Rows)
            {
                var id = ConvertToInt(row["id"]);
                if (!usedWords.Contains(id))
                    continue;

                var word = ConvertToStr(row["word"]).ToLower();
                result.AddWord(id, word);
            }

            return result;
        }

        private static void AddUsedWords(HashSet<int> usedWords, DataReader connection, string regexTableName, string wordIdColumn) 
        {
            var rs = connection.SelectQuery("SELECT " + wordIdColumn + " FROM " + regexTableName);
            if (rs == null)
                return;

            foreach (DataRow row in rs.Rows)
                usedWords.Add(ConvertToInt(row[wordIdColumn]));
        }

        private int FindIdFromList(string uaString, ICollection<int> foundClientWords, IEnumerable<IdRegString> list)
        {
            foreach (var irs in list)
            {
                if ((irs.WordId1 != 0 && !foundClientWords.Contains(irs.WordId1)) ||
                    (irs.WordId2 != 0 && !foundClientWords.Contains(irs.WordId2)))
                    continue;

                var regConv = new PerlRegExpConverter(irs.Pattern, "", Encoding.UTF8);
                var searchTerm = regConv.Regex;
                if (searchTerm.IsMatch(uaString))
                    return irs.Id;
            }

            return -1;
        }

        private static List<IdRegString> PrepareRegexpStruct(DataReader connection, string regexpTableName) 
        {
            var ret = new List<IdRegString>();
            var rs = connection.SelectQuery("SELECT rowid, regstring, word_id, word2_id FROM " + regexpTableName + " ORDER BY sequence");

            if (rs == null)
                return ret;

            foreach (DataRow row in rs.Rows)
            {
                var irs = new IdRegString();
                irs.Id = ConvertToInt(row["rowid"]);
                irs.WordId1 = ConvertToInt(row["word_id"]);
                irs.WordId2 = ConvertToInt(row["word2_id"]);

                var regex = ConvertToStr(row["regstring"]);
                var reg = new Regex(@"^/?(.*?)/si$");
                if (reg.IsMatch(regex))
                    regex = reg.Match(regex).Groups[0].ToString();

                irs.Pattern = regex;
                ret.Add(irs);
            }

            return ret;
        }

        #endregion
    }
}
