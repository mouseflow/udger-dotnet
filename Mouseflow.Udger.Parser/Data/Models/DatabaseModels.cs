using System.Text;
using System.Text.RegularExpressions;

namespace Mouseflow.Udger.Parser.Data.Models
{

    #region Views
    // UdgerSqlQuery.SQL_CLIENT
    public class Client
    {
        public int class_id { get; set; }
        public int client_id { get; set; }
        public string ua_class { get; set; }
        public string ua_class_code { get; set; }
        public string ua { get; set; }
        public string ua_engine { get; set; }
        public string ua_version { get; set; }
        public string ua_version_major { get; set; }
        public string crawler_last_seen { get; set; }
        public string crawler_respect_robotstxt { get; set; }
        public string crawler_category { get; set; }
        public string crawler_category_code { get; set; }
        public string ua_uptodate_current_version { get; set; }
        public string ua_family { get; set; }
        public string ua_family_code { get; set; }
        //public string ua_family_homepage { get; set; }
        //public string ua_family_icon { get; set; }
        //public string ua_family_icon_big { get; set; }
        public string ua_family_vendor { get; set; }
        public string ua_family_vendor_code { get; set; }
        //public string ua_family_vendor_homepage { get; set; }
        public string regstring { get; set; }

        public Regex Reg { get; set; }
    }

    // UdgerSqlQuery.DEVICE_COLUMNS
    public class DeviceColumn
    {
        public string device_class { get; set; }
        public string device_class_code { get; set; }
        //public string device_class_icon { get; set; }
        //public string device_class_icon_big { get; set; }
    }

    // UdgerSqlQuery.SQL_OS
    // UdgerSqlQuery.SQL_CLIENT_OS
    public class OS : OSColumn
    {
        public string id { get; set; } // rowId or client_id
    }

    // UdgerSqlQuery.OS_COLUMNS
    public class OSColumn
    {
        public string os_family { get; set; }
        public string os_family_code { get; set; }
        public string os { get; set; }
        public string os_code { get; set; }
        //public string os_home_page { get; set; }
        //public string deviceos_icon_class { get; set; }
        //public string os_icon { get; set; }
        //public string os_icon_big { get; set; }
        public string os_family_vendor { get; set; }
        public string os_family_vendor_code { get; set; }
        //public string os_family_vedor_homepage { get; set; }
    }

    #endregion

    #region Tables
    public class DeviceRegex
    {
        public string os_family_code { get; set; }
        public string os_code { get; set; }
        public int id { get; set; }
        public string regstring { get; set; }

        public Regex Reg { get; set; }
    }

    // udger_devicename_list
    public class DeviceName
    {
        public int regex_id { get; set; }
        public int brand_id { get; set; }
        public string code { get; set; }
        public string marketname { get; set; }
    }

    // udger_devicename_brand
    public class DeviceBrand
    {
        public int id { get; set; }
        public string brand { get; set; }
        public string brand_code { get; set; }
        //public string brand_url { get; set; }
        //public string icon { get; set; }
        //public string icon_big { get; set; }
    }
    #endregion

}