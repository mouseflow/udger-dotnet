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
        public UserAgent userAgent { get; private set; }
        public IPAddress ipAddress { get; private set; }

        public string ip { get; set; }
        public string ua { get; set; }

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
        private readonly DataReader dt;
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
        /// <param name="LRUCashCapacity">int LRUCash Capacity (minimum is 1)</param>
        public UdgerParser(bool useLRUCache = true, int LRUCashCapacity = 10000)
        {
            dt = new DataReader();
            ua = "";
            ip = "";
            useCache = useLRUCache;
            if (useLRUCache)
                cache = new LRUCache<string, UserAgent>(LRUCashCapacity);
        }
        #endregion

        #region setParser method
        /// <summary>
        /// Set the data directory
        /// </summary> 
        /// <param name="dataDir">string path udger DB directory</param>
        public void SetDataDir(string dataDir)
        {
            if (!Directory.Exists(dataDir))
                throw new Exception("Data dir not found");

            dt.data_dir = dataDir;
            dt.DataSourcePath = dataDir + @"\udgerdb_v3.dat";

            if (!File.Exists(dt.DataSourcePath))
                throw new Exception("Data file udgerdb_v3.dat not found");
        }
        /// <summary>
        /// Set the data directory and DB filename
        /// </summary> 
        /// <param name="dataDir">string path udger DB directory</param>
        /// <param name="fileName">string path udger DB filename</param>
        public void SetDataDir(string dataDir, string fileName)
        {
            if (!Directory.Exists(dataDir))
                throw new Exception("Data dir not found");

            dt.data_dir = dataDir;
            dt.DataSourcePath = dataDir + @"\" + fileName;

            if (!File.Exists(dt.DataSourcePath))
                throw new Exception("Data file " + fileName + " not found");
        }
        #endregion

        #region public method
        /// <summary>
        /// Parse the useragent string and/or ip address
        /// /// </summary>
        public void parse()
        {
            ipAddress = new IPAddress();
            userAgent = new UserAgent();

            dt.connect(this);
            initStaticStructures(dt);
            if (!dt.Connected)
                return;

            if (ua != "")
            {
                if (useCache && cache.TryGetValue(ua, out var uaCache))
                {
                    userAgent = uaCache;
                }
                else
                {
                    parseUA(ua.Replace("'", "''"));
                    ua = "";
                }
            }

            if (ip == "")
                return;

            parseIP(ip.Replace("'", "''"));
            ip = "";
        }
        #endregion

        #region private method

        #region parse
        private void parseUA(string _userAgent)
        {
            var clientId = 0;
            var clientClassId = -1;
            var osId = 0;

            if (string.IsNullOrEmpty(_userAgent))
                return;

            userAgent.UaString = ua;
            userAgent.UaClass = "Unrecognized";
            userAgent.UaClassCode = "unrecognized";

            if (!dt.Connected)
                return;

            processClient(_userAgent, ref osId, ref clientId, ref clientClassId);
            processOS(_userAgent, ref osId, clientId);
            processDevice(_userAgent, ref clientClassId);

            if (!string.IsNullOrEmpty(userAgent.OsFamilyCode))
                processDeviceBrand();

            //set cache
            if (useCache)
                cache.Set(_userAgent, userAgent);
        }

        private void parseIP(string _ip)
        {
            if (string.IsNullOrEmpty(_ip))
                return;

            ipAddress.Ip = ip;

            if (!dt.Connected)
                return;

            var ipVer = getIPAddressVersion(ip, out var ipLoc);
            if (ipVer == 0)
                return;

            if (ipLoc != "")
                _ip = ipLoc;

            ipAddress.IpVer = ConvertToStr(ipVer);

            var ipTable = dt.selectQuery(@"SELECT udger_crawler_list.id as botid,ip_last_seen,ip_hostname,ip_country,ip_city,ip_country_code,ip_classification,ip_classification_code,
                name,ver,ver_major,last_seen,respect_robotstxt,family,family_code,family_homepage,family_icon,vendor,vendor_code,vendor_homepage,crawler_classification,crawler_classification_code,crawler_classification
                FROM udger_ip_list
                JOIN udger_ip_class ON udger_ip_class.id=udger_ip_list.class_id
                LEFT JOIN udger_crawler_list ON udger_crawler_list.id=udger_ip_list.crawler_id
                LEFT JOIN udger_crawler_class ON udger_crawler_class.id=udger_crawler_list.class_id
                WHERE ip=" + '"' + _ip + '"' + " ORDER BY sequence");

            if (ipTable != null && ipTable.Rows.Count > 0)
                prepareIp(ipTable.Rows[0]);

            if (ipVer != 4)
                return;

            var ipLong = AddrToInt(_ip);

            var dataCenter = dt.selectQuery(@"select name, name_code, homepage
                FROM udger_datacenter_range
                JOIN udger_datacenter_list ON udger_datacenter_range.datacenter_id = udger_datacenter_list.id
                where iplong_from <= " + ipLong + " AND iplong_to >=" + ipLong);

            if (dataCenter != null && dataCenter.Rows.Count > 0)
                prepareIpDataCenter(dataCenter.Rows[0]);
        }
        #endregion

        #region process methods

        private void processOS(string uaString, ref int osId, int clientId)
        {
            var rowId = findIdFromList(uaString, osWordDetector.findWords(uaString), osRegstringList);
            if (rowId != -1)
            {
                var q = string.Format(UdgerSqlQuery.SQL_OS, rowId);
                var opSysRs = dt.selectQuery(q);
                prepareOs(opSysRs.Rows[0], ref osId);
            }
            else if(clientId != 0)
            {
                var opSysRs = dt.selectQuery(string.Format(UdgerSqlQuery.SQL_CLIENT_OS, clientId));
                if (opSysRs != null && opSysRs.Rows.Count > 0)
                    prepareOs(opSysRs.Rows[0], ref osId);
            }
        }


        private void processClient(string uaString, ref int osId, ref int clientId, ref int classId)
        {
            var q = string.Format(UdgerSqlQuery.SQL_CRAWLER, uaString);
            var userAgentRs = dt.selectQuery(q);
            if (userAgentRs != null && userAgentRs.Rows.Count > 0 )
            {
                prepareUa(userAgentRs.Rows[0],true, ref clientId, ref classId);
                classId = 99;
                clientId = -1;
            }
            else
            {
                var rowId = findIdFromList(uaString, clientWordDetector.findWords(uaString), clientRegstringList);
                if (rowId != -1)
                {
                    userAgentRs = dt.selectQuery(string.Format(UdgerSqlQuery.SQL_CLIENT, rowId));
                    prepareUa(userAgentRs.Rows[0],false, ref clientId, ref classId);
                }
                else
                {
                    userAgent.UaClass = "Unrecognized";
                    userAgent.UaClassCode = "unrecognized";
                }
            }
        }

        private void processDevice(string uaString, ref int classId)
        {
            var rowId = findIdFromList(uaString, deviceWordDetector.findWords(uaString), deviceRegstringList);
            if (rowId != -1)
            {
                var devRs = dt.selectQuery(String.Format(UdgerSqlQuery.SQL_DEVICE, rowId));
                prepareDevice(devRs.Rows[0], ref classId);
            }
            else
            {
                if (classId == -1)
                    return;

                var devRs = dt.selectQuery(string.Format(UdgerSqlQuery.SQL_CLIENT_CLASS, classId.ToString()));
                if (devRs != null && devRs.Rows.Count > 0)
                    prepareDevice(devRs.Rows[0], ref classId);
            }
        }

        private void processDeviceBrand()
        {
            Regex reg;
            PerlRegExpConverter regConv;

            var devRs = dt.selectQuery(string.Format(UdgerSqlQuery.SQL_DEVICE_REGEX, userAgent.OsFamilyCode, userAgent.OsCode));
            if (devRs == null || devRs.Rows.Count <= 0)
                return;

            foreach (DataRow row in devRs.Rows)
            {
                var devId = ConvertToStr(row["id"]);
                var regex = ConvertToStr(row["regstring"]);
                if (devId == null || regex == null)
                    continue;

                regConv = new PerlRegExpConverter(regex, "", Encoding.UTF8);
                reg = regConv.Regex;
                if (!reg.IsMatch(ua))
                    continue;

                var foo = reg.Match(ua).Groups[1].ToString();
                var devNameListRs = dt.selectQuery(string.Format(UdgerSqlQuery.SQL_DEVICE_NAME_LIST, devId, foo));
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

        private void prepareUa(DataRow _row,Boolean crawler,ref int clientId, ref int classId)
        {
            userAgent.Ua = ConvertToStr(_row["ua"]);
            userAgent.UaVersion = ConvertToStr(_row["ua_version"]);
            userAgent.UaVersionMajor = ConvertToStr(_row["ua_version_major"]);
            if (!crawler)
            {
                var pattern = ConvertToStr(_row["regstring"]);
                if (pattern != "")
                {
                    var regConv = new PerlRegExpConverter(pattern, "", Encoding.UTF8);
                    var searchTerm = regConv.Regex;
                    Group group;
                    if (searchTerm.IsMatch(ua) && (group = searchTerm.Match(ua).Groups[1]) != null)
                    {
                        userAgent.Ua = ConvertToStr(_row["ua"]) + " " + ConvertToStr(group);
                        userAgent.UaVersion = ConvertToStr(group);
                        userAgent.UaVersionMajor = ConvertToStr(group).Split('.')[0];
                    }
                }
            }
            clientId = ConvertToInt(_row["client_id"]);
            classId =  ConvertToInt(_row["class_id"]);
            userAgent.CrawlerCategory = ConvertToStr(_row["crawler_category"]);
            userAgent.CrawlerCategoryCode = ConvertToStr(_row["crawler_category_code"]);
            userAgent.CrawlerLastSeen = ConvertToStr(_row["crawler_last_seen"]);
            userAgent.CrawlerRespectRobotstxt = ConvertToStr(_row["crawler_respect_robotstxt"]);
            userAgent.UaString = ua;
            userAgent.UaClass = ConvertToStr(_row["ua_class"]);
            userAgent.UaClassCode = ConvertToStr(_row["ua_class_code"]);
            userAgent.UaUptodateCurrentVersion = ConvertToStr(_row["ua_uptodate_current_version"]);
            userAgent.UaFamily = ConvertToStr(_row["ua_family"]);
            userAgent.UaFamilyCode = ConvertToStr(_row["ua_family_code"]);
            userAgent.UaFamilyHompage = ConvertToStr(_row["ua_family_homepage"]);
            userAgent.UaFamilyVendor = ConvertToStr(_row["ua_family_vendor"]);
            userAgent.UaFamilyVendorCode = ConvertToStr(_row["ua_family_vendor_code"]);
            userAgent.UaFamilyVendorHomepage = ConvertToStr(_row["ua_family_vendor_homepage"]);
            userAgent.UaFamilyIcon = ConvertToStr(_row["ua_family_icon"]);
            userAgent.UaFamilyIconBig = ConvertToStr(_row["ua_family_icon_big"]);
            userAgent.UaFamilyInfoUrl = ConvertToStr(_row["ua_family_info_url"]);
            userAgent.UaEngine = ConvertToStr(_row["ua_engine"]);
        }

        private void prepareOs(DataRow _row, ref int _osId)
        {
            //_osId = Convert.ToInt32(_row["os_id"]);
            userAgent.Os = ConvertToStr(_row["os"]);
            userAgent.OsCode = ConvertToStr(_row["os_code"]);
            userAgent.OsHomepage = ConvertToStr(_row["os_home_page"]);
            userAgent.OsIcon = ConvertToStr(_row["os_icon"]);
            userAgent.OsIconBig = ConvertToStr(_row["os_icon_big"]);
            userAgent.OsInfoUrl = ConvertToStr(_row["os_info_url"]);
            userAgent.OsFamily = ConvertToStr(_row["os_family"]);
            userAgent.OsFamilyCode = ConvertToStr(_row["os_family_code"]);
            userAgent.OsFamilyVendor = ConvertToStr(_row["os_family_vendor"]);
            userAgent.OsFamilyVendorCode = ConvertToStr(_row["os_family_vendor_code"]);
            userAgent.OsFamilyVendorHomepage = ConvertToStr(_row["os_family_vedor_homepage"]);
        }

        private void prepareDevice(DataRow _row, ref int _deviceClassId)
        {
            //_deviceClassId = Convert.ToInt32(_row["device_class"]);
            userAgent.DeviceClass = ConvertToStr(_row["device_class"]);
            userAgent.DeviceClassCode = ConvertToStr(_row["device_class_code"]);
            userAgent.DeviceClassIcon = ConvertToStr(_row["device_class_icon"]);
            userAgent.DeviceClassIconBig = ConvertToStr(_row["device_class_icon_big"]);
            userAgent.DeviceClassInfoUrl =  ConvertToStr(_row["device_class_info_url"]);
        }

        private void prepareIp(DataRow _row)
        {
            ipAddress.IpClassification = ConvertToStr(_row["ip_classification"]);
            ipAddress.IpClassificationCode = ConvertToStr(_row["ip_classification_code"]);
            ipAddress.IpLastSeen = ConvertToStr(_row["ip_last_seen"]);
            ipAddress.IpHostname = ConvertToStr(_row["ip_hostname"]);
            ipAddress.IpCountry = ConvertToStr(_row["ip_country"]);
            ipAddress.IpCountryCode = ConvertToStr(_row["ip_country_code"]);
            ipAddress.IpCity = ConvertToStr(_row["ip_city"]);
            ipAddress.CrawlerName = ConvertToStr(_row["name"]);
            ipAddress.CrawlerVer = ConvertToStr(_row["ver"]);
            ipAddress.CrawlerVerMajor = ConvertToStr(_row["ver_major"]);
            ipAddress.CrawlerFamily = ConvertToStr(_row["family"]);
            ipAddress.CrawlerFamilyCode = ConvertToStr(_row["family_code"]);
            ipAddress.CrawlerFamilyHomepage = ConvertToStr(_row["family_homepage"]);
            ipAddress.CrawlerFamilyVendor = ConvertToStr(_row["vendor"]);
            ipAddress.CrawlerFamilyVendorCode = ConvertToStr(_row["vendor_code"]);
            ipAddress.CrawlerFamilyVendorHomepage = ConvertToStr(_row["vendor_homepage"]);
            ipAddress.CrawlerFamilyIcon = ConvertToStr(_row["family_icon"]);
            ipAddress.CrawlerLastSeen = ConvertToStr(_row["last_seen"]);
            ipAddress.CrawlerCategory = ConvertToStr(_row["crawler_classification"]);
            ipAddress.CrawlerCategoryCode = ConvertToStr(_row["crawler_classification_code"]);
            if (ipAddress.IpClassificationCode == "crawler")
                ipAddress.CrawlerFamilyInfoUrl = "https://udger.com/resources/ua-list/bot-detail?bot=" + ConvertToStr(_row["family"]) + "#id" + ConvertToStr(_row["botid"]);
            ipAddress.CrawlerRespectRobotstxt = ConvertToStr(_row["respect_robotstxt"]);
        }

        private void prepareIpDataCenter(DataRow _row)
        {
            ipAddress.DatacenterName = ConvertToStr(_row["name"]);
            ipAddress.DatacenterNameCode = ConvertToStr(_row["name_code"]);
            ipAddress.DatacenterHomepage = ConvertToStr(_row["homepage"]);
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

        private int getIPAddressVersion(string _ip, out string _retIp)
        {
            _retIp = "";

            if (!System.Net.IPAddress.TryParse(_ip, out var addr))
                return 0;

            _retIp = addr.ToString();
            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return 4;
            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                return 6;

            return 0;
        }

        private long AddrToInt(string addr)
        {
            return (uint)System.Net.IPAddress.NetworkToHostOrder(
                (int)System.Net.IPAddress.Parse(addr).Address);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private static void initStaticStructures(DataReader connection)
        {
            if (clientRegstringList != null)
                return;

            clientRegstringList = prepareRegexpStruct(connection, "udger_client_regex");
            osRegstringList = prepareRegexpStruct(connection, "udger_os_regex");
            deviceRegstringList = prepareRegexpStruct(connection, "udger_deviceclass_regex");

            clientWordDetector = createWordDetector(connection, "udger_client_regex", "udger_client_regex_words");
            deviceWordDetector = createWordDetector(connection, "udger_deviceclass_regex", "udger_deviceclass_regex_words");
            osWordDetector = createWordDetector(connection, "udger_os_regex", "udger_os_regex_words");
        }

        private static WordDetector createWordDetector(DataReader connection, string regexTableName, string wordTableName)
        {
            var usedWords = new HashSet<int>();

            addUsedWords(usedWords, connection, regexTableName, "word_id");
            addUsedWords(usedWords, connection, regexTableName, "word2_id");

            var result = new WordDetector();

            var dt = connection.selectQuery("SELECT * FROM " + wordTableName);
            if (dt == null)
                return result;

            foreach (DataRow row in dt.Rows)
            {
                var id = ConvertToInt(row["id"]);
                if (!usedWords.Contains(id))
                    continue;

                var word = ConvertToStr(row["word"]).ToLower();
                result.addWord(id, word);
            }

            return result;
        }

        private static void addUsedWords(HashSet<int> usedWords, DataReader connection, string regexTableName, string wordIdColumn) 
        {
            var rs = connection.selectQuery("SELECT " + wordIdColumn + " FROM " + regexTableName);
            if (rs == null)
                return;

            foreach (DataRow row in rs.Rows)
                usedWords.Add(ConvertToInt(row[wordIdColumn]));
        }

        private int findIdFromList(string uaString, ICollection<int> foundClientWords, IEnumerable<IdRegString> list)
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

        private static List<IdRegString> prepareRegexpStruct(DataReader connection, string regexpTableName) 
        {
            var ret = new List<IdRegString>();
            var rs = connection.selectQuery("SELECT rowid, regstring, word_id, word2_id FROM " + regexpTableName + " ORDER BY sequence");

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
