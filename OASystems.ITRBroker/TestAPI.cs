using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RestSharp;

namespace OASystems.ITRBroker
{
    public static class TestAPI
    {
        public static async Task DoTestAsync(string message)
        {
            var client = new RestClient("https://webhook.site/21ef4f0f-878d-4a3c-bb01-5f1b9e49b81b");
            client.Timeout = -1;
            var request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "text/plain");
            var body = @message;
            request.AddParameter("text/plain", body, ParameterType.RequestBody);
            IRestResponse response = await client.ExecuteAsync(request);
            Console.WriteLine(response.Content);
        }
    }
}
