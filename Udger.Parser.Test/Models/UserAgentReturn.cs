using System.Text.Json.Serialization;

namespace Udger.Parser.Test.Models
{
    public class UserAgentReturn
    {
        [JsonPropertyName("ua_string")]
        public string UaString { get; set; }

        [JsonPropertyName("ua_class")]
        public string UaClass { get; set; }

        [JsonPropertyName("ua_class_code")]
        public string UaClassCode { get; set; }

        [JsonPropertyName("ua")]
        public string Ua { get; set; }

        [JsonPropertyName("ua_version")]
        public string UaVersion { get; set; }

        [JsonPropertyName("ua_version_major")]
        public string UaVersionMajor { get; set; }

        [JsonPropertyName("ua_uptodate_current_version")]
        public string UaUptodateCurrentVersion { get; set; }

        [JsonPropertyName("ua_family")]
        public string UaFamily { get; set; }

        [JsonPropertyName("ua_family_code")]
        public string UaFamilyCode { get; set; }

        [JsonPropertyName("ua_family_homepage")]
        public string UaFamilyHompage { get; set; }

        [JsonPropertyName("ua_family_vendor")]
        public string UaFamilyVendor { get; set; }

        [JsonPropertyName("ua_family_vendor_code")]
        public string UaFamilyVendorCode { get; set; }

        [JsonPropertyName("ua_family_vendor_homepage")]
        public string UaFamilyVendorHomepage { get; set; }

        [JsonPropertyName("ua_family_icon")]
        public string UaFamilyIcon { get; set; }

        [JsonPropertyName("ua_family_icon_big")]
        public string UaFamilyIconBig { get; set; }

        [JsonPropertyName("ua_family_info_url")]
        public string UaFamilyInfoUrl { get; set; }

        [JsonPropertyName("ua_engine")]
        public string UaEngine { get; set; }

        [JsonPropertyName("os")]
        public string Os { get; set; }

        [JsonPropertyName("os_code")]
        public string OsCode { get; set; }

        [JsonPropertyName("os_homepage")]
        public string OsHomepage { get; set; }

        [JsonPropertyName("os_icon")]
        public string OsIcon { get; set; }

        [JsonPropertyName("os_icon_big")]
        public string OsIconBig { get; set; }

        [JsonPropertyName("os_info_url")]
        public string OsInfoUrl { get; set; }

        [JsonPropertyName("os_family")]
        public string OsFamily { get; set; }

        [JsonPropertyName("os_family_code")]
        public string OsFamilyCode { get; set; }

        [JsonPropertyName("os_family_vendor")]
        public string OsFamilyVendor { get; set; }

        [JsonPropertyName("os_family_vendor_code")]
        public string OsFamilyVendorCode { get; set; }

        [JsonPropertyName("os_family_vendor_homepage")]
        public string OsFamilyVendorHomepage { get; set; }

        [JsonPropertyName("device_class")]
        public string DeviceClass { get; set; }

        [JsonPropertyName("device_class_code")]
        public string DeviceClassCode { get; set; }

        [JsonPropertyName("device_class_icon")]
        public string DeviceClassIcon { get; set; }

        [JsonPropertyName("device_class_icon_big")]
        public string DeviceClassIconBig { get; set; }

        [JsonPropertyName("device_class_info_url")]
        public string DeviceClassInfoUrl { get; set; }

        [JsonPropertyName("device_marketname")]
        public string DeviceMarketname { get; set; }

        [JsonPropertyName("device_brand")]
        public string DeviceBrand { get; set; }

        [JsonPropertyName("device_brand_code")]
        public string DeviceBrandCode { get; set; }

        [JsonPropertyName("device_brand_homepage")]
        public string DeviceBrandHomepage { get; set; }

        [JsonPropertyName("device_brand_icon")]
        public string DeviceBrandIcon { get; set; }

        [JsonPropertyName("device_brand_icon_big")]
        public string DeviceBrandIconBig { get; set; }

        [JsonPropertyName("device_brand_info_url")]
        public string DeviceBrandInfoUrl { get; set; }

        [JsonPropertyName("crawler_category")]
        public string CrawlerCategory { get; set; }

        [JsonPropertyName("crawler_category_code")]
        public string CrawlerCategoryCode { get; set; }

        [JsonPropertyName("crawler_respect_robotstxt")]
        public string CrawlerRespectRobotstxt { get; set; }
    }
}
