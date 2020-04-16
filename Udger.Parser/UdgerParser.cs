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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Udger.Parser.Cache;
using Udger.Parser.Data;
using Udger.Parser.Helpers;

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

        private static IdRegString[] clientRegstringList;
        private static IdRegString[] osRegstringList;
        private static IdRegString[] deviceRegstringList;
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

        #region public method
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
                cache.TryAdd(uaString, userAgent);

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

            ipAddress.IpVer = ipVer.ToString();

            var ipRow = db.SelectRow(@"SELECT udger_crawler_list.id as botid,ip_last_seen,ip_hostname,ip_country,ip_city,ip_country_code,ip_classification,ip_classification_code,
                name,ver,ver_major,last_seen,respect_robotstxt,family,family_code,family_homepage,family_icon,vendor,vendor_code,vendor_homepage,crawler_classification,crawler_classification_code,crawler_classification
                FROM udger_ip_list
                JOIN udger_ip_class ON udger_ip_class.id=udger_ip_list.class_id
                LEFT JOIN udger_crawler_list ON udger_crawler_list.id=udger_ip_list.crawler_id
                LEFT JOIN udger_crawler_class ON udger_crawler_class.id=udger_crawler_list.class_id
                WHERE ip=" + '"' + ip + '"' + " ORDER BY sequence");

            if (ipRow != null)
                PrepareIp(ipAddress, ipRow);

            if (ipVer != 4)
                return ipAddress;

            var ipLong = IpToInt(ip);

            var dataCenterRow = db.SelectRow(@"select name, name_code, homepage
                FROM udger_datacenter_range
                JOIN udger_datacenter_list ON udger_datacenter_range.datacenter_id = udger_datacenter_list.id
                where iplong_from <= " + ipLong + " AND iplong_to >=" + ipLong);

            if (dataCenterRow != null)
                PrepareIpDataCenter(ipAddress, dataCenterRow);

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
                var row = db.SelectRow(q);
                PrepareOs(userAgent, row);
            }
            else if (clientId != 0)
            {
                var row = db.SelectRow(string.Format(UdgerSqlQuery.SQL_CLIENT_OS, clientId));
                if (row != null)
                    PrepareOs(userAgent, row);
            }
        }


        private void ProcessClient(UserAgent userAgent, string uaString, ref int clientId, ref int classId)
        {
            var q = string.Format(UdgerSqlQuery.SQL_CRAWLER, uaString);
            var row = db.SelectRow(q);
            if (row != null)
            {
                PrepareUa(userAgent, uaString, row, true, ref clientId, ref classId);
                classId = 99;
                clientId = -1;
            }
            else
            {
                var rowId = FindIdFromList(uaString, clientWordDetector.FindWords(uaString), clientRegstringList);
                if (rowId != -1)
                {
                    row = db.SelectRow(string.Format(UdgerSqlQuery.SQL_CLIENT, rowId));
                    PrepareUa(userAgent, uaString, row, false, ref clientId, ref classId);
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
                var row = db.SelectRow(string.Format(UdgerSqlQuery.SQL_DEVICE, rowId));
                PrepareDevice(userAgent, row);
            }
            else
            {
                if (classId == -1)
                    return;

                var row = db.SelectRow(string.Format(UdgerSqlQuery.SQL_CLIENT_CLASS, classId.ToString()));
                if (row != null)
                    PrepareDevice(userAgent, row);
            }
        }

        private void ProcessDeviceBrand(UserAgent userAgent, string uaString)
        {
            var rows = db.Select(string.Format(UdgerSqlQuery.SQL_DEVICE_REGEX, userAgent.OsFamilyCode, userAgent.OsCode));

            foreach (var row in rows)
            {
                var devId = row.Read<string>("id");
                var regex = row.Read<string>("regstring");
                if (devId == null || regex == null)
                    continue;

                var regConv = new PerlRegExpConverter(regex, "", Encoding.UTF8);
                var reg = regConv.Regex;
                if (!reg.IsMatch(uaString))
                    continue;

                var foo = reg.Match(uaString).Groups[1].ToString();
                var deviceRow = db.SelectRow(string.Format(UdgerSqlQuery.SQL_DEVICE_NAME_LIST, devId, foo));
                if (deviceRow == null)
                    continue;

                userAgent.DeviceMarketname = deviceRow.Read<string>("marketname");
                userAgent.DeviceBrand = deviceRow.Read<string>("brand");
                userAgent.DeviceBrandCode = deviceRow.Read<string>("brand_code");
                userAgent.DeviceBrandHomepage = deviceRow.Read<string>("brand_url");
                userAgent.DeviceBrandIcon = deviceRow.Read<string>("icon");
                userAgent.DeviceBrandIconBig = deviceRow.Read<string>("icon_big");
                userAgent.DeviceBrandInfoUrl = @"https://udger.com/resources/ua-list/devices-brand-detail?brand=" + deviceRow.Read<string>("brand_code");
                break;
            }
        }
        #endregion

        #region prepare data methods

        private void PrepareUa(UserAgent userAgent, string uaString, DataRow row, bool crawler, ref int clientId, ref int classId)
        {
            userAgent.Ua = row.Read<string>("ua");
            userAgent.UaVersion = row.Read<string>("ua_version");
            userAgent.UaVersionMajor = row.Read<string>("ua_version_major");
            userAgent.CrawlerCategory = row.Read<string>("crawler_category");
            userAgent.CrawlerCategoryCode = row.Read<string>("crawler_category_code");
            userAgent.CrawlerLastSeen = row.Read<string>("crawler_last_seen");
            userAgent.CrawlerRespectRobotstxt = row.Read<string>("crawler_respect_robotstxt");
            userAgent.UaString = uaString;
            userAgent.UaClass = row.Read<string>("ua_class");
            userAgent.UaClassCode = row.Read<string>("ua_class_code");
            userAgent.UaUptodateCurrentVersion = row.Read<string>("ua_uptodate_current_version");
            userAgent.UaFamily = row.Read<string>("ua_family");
            userAgent.UaFamilyCode = row.Read<string>("ua_family_code");
            userAgent.UaFamilyHompage = row.Read<string>("ua_family_homepage");
            userAgent.UaFamilyVendor = row.Read<string>("ua_family_vendor");
            userAgent.UaFamilyVendorCode = row.Read<string>("ua_family_vendor_code");
            userAgent.UaFamilyVendorHomepage = row.Read<string>("ua_family_vendor_homepage");
            userAgent.UaFamilyIcon = row.Read<string>("ua_family_icon");
            userAgent.UaFamilyIconBig = row.Read<string>("ua_family_icon_big");
            userAgent.UaFamilyInfoUrl = row.Read<string>("ua_family_info_url");
            userAgent.UaEngine = row.Read<string>("ua_engine");

            if (!crawler)
            {
                var pattern = row.Read<string>("regstring");
                if (pattern != "")
                {
                    var regConv = new PerlRegExpConverter(pattern, "", Encoding.UTF8);
                    var searchTerm = regConv.Regex;
                    Group group;
                    if (searchTerm.IsMatch(uaString) && (group = searchTerm.Match(uaString).Groups[1]) != null)
                    {
                        userAgent.Ua = row.Read<string>("ua") + " " + group;
                        userAgent.UaVersion = group.ToString();
                        userAgent.UaVersionMajor = group.ToString().Split('.')[0];
                    }
                }
            }

            clientId = row.Read<int>("client_id");
            classId = row.Read<int>("class_id");
        }

        private void PrepareOs(UserAgent userAgent, DataRow row)
        {
            userAgent.Os = row.Read<string>("os");
            userAgent.OsCode = row.Read<string>("os_code");
            userAgent.OsHomepage = row.Read<string>("os_home_page");
            userAgent.OsIcon = row.Read<string>("os_icon");
            userAgent.OsIconBig = row.Read<string>("os_icon_big");
            userAgent.OsInfoUrl = row.Read<string>("os_info_url");
            userAgent.OsFamily = row.Read<string>("os_family");
            userAgent.OsFamilyCode = row.Read<string>("os_family_code");
            userAgent.OsFamilyVendor = row.Read<string>("os_family_vendor");
            userAgent.OsFamilyVendorCode = row.Read<string>("os_family_vendor_code");
            userAgent.OsFamilyVendorHomepage = row.Read<string>("os_family_vedor_homepage");
        }

        private void PrepareDevice(UserAgent userAgent, DataRow row)
        {
            userAgent.DeviceClass = row.Read<string>("device_class");
            userAgent.DeviceClassCode = row.Read<string>("device_class_code");
            userAgent.DeviceClassIcon = row.Read<string>("device_class_icon");
            userAgent.DeviceClassIconBig = row.Read<string>("device_class_icon_big");
            userAgent.DeviceClassInfoUrl = row.Read<string>("device_class_info_url");
        }

        private void PrepareIp(IpAddress ipAddress, DataRow row)
        {
            ipAddress.IpClassification = row.Read<string>("ip_classification");
            ipAddress.IpClassificationCode = row.Read<string>("ip_classification_code");
            ipAddress.IpLastSeen = row.Read<string>("ip_last_seen");
            ipAddress.IpHostname = row.Read<string>("ip_hostname");
            ipAddress.IpCountry = row.Read<string>("ip_country");
            ipAddress.IpCountryCode = row.Read<string>("ip_country_code");
            ipAddress.IpCity = row.Read<string>("ip_city");
            ipAddress.CrawlerName = row.Read<string>("name");
            ipAddress.CrawlerVer = row.Read<string>("ver");
            ipAddress.CrawlerVerMajor = row.Read<string>("ver_major");
            ipAddress.CrawlerFamily = row.Read<string>("family");
            ipAddress.CrawlerFamilyCode = row.Read<string>("family_code");
            ipAddress.CrawlerFamilyHomepage = row.Read<string>("family_homepage");
            ipAddress.CrawlerFamilyVendor = row.Read<string>("vendor");
            ipAddress.CrawlerFamilyVendorCode = row.Read<string>("vendor_code");
            ipAddress.CrawlerFamilyVendorHomepage = row.Read<string>("vendor_homepage");
            ipAddress.CrawlerFamilyIcon = row.Read<string>("family_icon");
            ipAddress.CrawlerLastSeen = row.Read<string>("last_seen");
            ipAddress.CrawlerCategory = row.Read<string>("crawler_classification");
            ipAddress.CrawlerCategoryCode = row.Read<string>("crawler_classification_code");
            if (ipAddress.IpClassificationCode == "crawler")
                ipAddress.CrawlerFamilyInfoUrl = "https://udger.com/resources/ua-list/bot-detail?bot=" + row.Read<string>("family") + "#id" + row.Read<string>("botid");
            ipAddress.CrawlerRespectRobotstxt = row.Read<string>("respect_robotstxt");
        }

        private void PrepareIpDataCenter(IpAddress ipAddress, DataRow row)
        {
            ipAddress.DatacenterName = row.Read<string>("name");
            ipAddress.DatacenterNameCode = row.Read<string>("name_code");
            ipAddress.DatacenterHomepage = row.Read<string>("homepage");
        }
        #endregion

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
            var rows = connection.Select("SELECT * FROM " + wordTableName);

            foreach (var row in rows)
            {
                var id = row.Read<int>("id");
                if (!usedWords.Contains(id))
                    continue;

                var word = row.Read<string>("word").ToLower();
                result.AddWord(id, word);
            }

            result.Freeze();

            return result;
        }

        private static void AddUsedWords(HashSet<int> usedWords, DataReader connection, string regexTableName, string wordIdColumn)
        {
            var rows = connection.Select("SELECT " + wordIdColumn + " FROM " + regexTableName);

            foreach (var row in rows)
                usedWords.Add(row.Read<int>(wordIdColumn));
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

        private static IdRegString[] PrepareRegexpStruct(DataReader connection, string regexpTableName)
        {
            return connection
                .Select("SELECT rowid, regstring, word_id, word2_id FROM " + regexpTableName + " ORDER BY sequence")
                .Select(CreateRegexpStruct)
                .ToArray();
        }

        private static IdRegString CreateRegexpStruct(DataRow row)
        {
            var irs = new IdRegString
            {
                Id = row.Read<int>("rowid"),
                WordId1 = row.Read<int>("word_id"),
                WordId2 = row.Read<int>("word2_id")
            };

            var regex = row.Read<string>("regstring");
            var reg = new Regex(@"^/?(.*?)/si$");
            if (reg.IsMatch(regex))
                regex = reg.Match(regex).Groups[0].ToString();

            irs.Pattern = regex;

            return irs;
        }

        #endregion
    }
}
