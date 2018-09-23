using Newtonsoft.Json;

namespace BitcoinApi.Business.BitcoinApi.Entities
{
    internal class BitcoinApiResult<T> : BitcoinApiResult
    {
        [JsonProperty("result")]
        public T Result { get; set; }
    }
}