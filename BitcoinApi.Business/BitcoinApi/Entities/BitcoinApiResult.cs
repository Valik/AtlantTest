using Newtonsoft.Json;

namespace BitcoinApi.Business.BitcoinApi.Entities
{
    internal class BitcoinApiResult
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }
}