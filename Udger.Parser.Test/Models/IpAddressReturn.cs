using System.Text.Json.Serialization;

namespace Udger.Parser.Test.Models
{
    public class IpAddressReturn
    {
        [JsonPropertyName("ip")]
        public string Ip { get; set; }

        [JsonPropertyName("ip_ver")]
        public string IpVer { get; set; }

        [JsonPropertyName("ip_classification")]
        public string IpClassification { get; set; }

        [JsonPropertyName("ip_classification_code")]
        public string IpClassificationCode { get; set; }

        [JsonPropertyName("ip_hostname")]
        public string IpHostname { get; set; }

        [JsonPropertyName("ip_country")]
        public string IpCountry { get; set; }

        [JsonPropertyName("ip_country_code")]
        public string IpCountryCode { get; set; }

        [JsonPropertyName("ip_city")]
        public string IpCity { get; set; }

        [JsonPropertyName("crawler_name")]
        public string CrawlerName { get; set; }

        [JsonPropertyName("crawler_ver")]
        public string CrawlerVer { get; set; }

        [JsonPropertyName("crawler_ver_major")]
        public string CrawlerVerMajor { get; set; }

        [JsonPropertyName("crawler_family")]
        public string CrawlerFamily { get; set; }

        [JsonPropertyName("crawler_family_code")]
        public string CrawlerFamilyCode { get; set; }

        [JsonPropertyName("crawler_family_homepage")]
        public string CrawlerFamilyHomepage { get; set; }

        [JsonPropertyName("crawler_family_vendor")]
        public string CrawlerFamilyVendor { get; set; }

        [JsonPropertyName("crawler_family_vendor_code")]
        public string CrawlerFamilyVendorCode { get; set; }

        [JsonPropertyName("crawler_family_vendor_homepage")]
        public string CrawlerFamilyVendorHomepage { get; set; }

        [JsonPropertyName("crawler_family_icon")]
        public string CrawlerFamilyIcon { get; set; }

        [JsonPropertyName("crawler_family_info_url")]
        public string CrawlerFamilyInfoUrl { get; set; }

        [JsonPropertyName("crawler_category")]
        public string CrawlerCategory { get; set; }

        [JsonPropertyName("crawler_category_code")]
        public string CrawlerCategoryCode { get; set; }

        [JsonPropertyName("crawler_respect_robotstxt")]
        public string CrawlerRespectRobotstxt { get; set; }

        [JsonPropertyName("datacenter_name")]
        public string DatacenterName { get; set; }

        [JsonPropertyName("datacenter_name_code")]
        public string DatacenterNameCode { get; set; }

        [JsonPropertyName("datacenter_homepage")]
        public string DatacenterHomepage { get; set; }
    }
}
