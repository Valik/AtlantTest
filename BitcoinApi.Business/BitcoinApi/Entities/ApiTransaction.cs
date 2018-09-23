using Newtonsoft.Json;

namespace BitcoinApi.Business.BitcoinApi.Entities
{
    internal class ApiTransaction
    {
        [JsonProperty("txid")]
        public string TxId { get; set; }

        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        [JsonProperty("fee")]
        public decimal Fee { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("timeReceived")]
        public long TimeReceived { get; set; }

        [JsonProperty("confirmations")]
        public int Confirmations { get; set; }
    }
}
