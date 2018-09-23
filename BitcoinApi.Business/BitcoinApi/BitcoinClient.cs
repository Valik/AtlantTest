using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using BitcoinApi.Business.BitcoinApi.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BitcoinApi.Business.BitcoinApi
{
    internal class BitcoinClient
    {
        public async Task<BitcoinApiResult> MakeRequest(string method, (string bitcoindAddress, string user, string password) requestParams, params object[] args)
        {
            return await MakeRequest<object>(method, requestParams, args);
        }

        public async Task<BitcoinApiResult<T>> MakeRequest<T>(string method, (string bitcoindAddress, string user, string password) requestParams, params object[] args)
        {
            var request = (HttpWebRequest)WebRequest.Create(requestParams.bitcoindAddress);
            request.ContentType = "application/json-rpc";
            request.Method = "POST";

            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(requestParams.user + ":" + requestParams.password));
            request.Headers.Add("Authorization", "Basic " + credentials);

            var requestData = new JObject
            {
                new JProperty("jsonrpc", "1.0"),
                new JProperty("id", "BitcoinClient"),
                new JProperty("method", method)
            };

            var @params = new JArray();
            foreach (var arg in args)
            {
                @params.Add(arg);
            }
            requestData.Add(new JProperty("params", @params));

            try
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestData));
                request.ContentLength = byteArray.Length;
                using (var dataStream = request.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                using (var response = await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                using (var streamReader = new StreamReader(stream))
                {
                    return JsonConvert.DeserializeObject<BitcoinApiResult<T>>(streamReader.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                return new BitcoinApiResult<T>
                {
                    Error = $"Bitcoind api call exception: {ex.Message}",
                };
            }
        }
    }
}
