using System.Text.Json.Serialization;

namespace Udger.Parser.Test.Models
{
    public class TestData<T>
    {
        [JsonPropertyName("test")]
        public Test Test { get; set; }

        [JsonPropertyName("ret")]
        public T Return { get; set; }
    }

    public class Test
    {
        [JsonPropertyName("teststring")]
        public string TestString { get; set; }
    }
}
