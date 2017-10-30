using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using MountainView.Base;
using MountainViewCore.Base;
using System;
using System.Text;

namespace MountainViewFn
{
    public static class GetRelevantChunkKeys
    {
        [FunctionName("GetRelevantChunkKeys")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            await Task.Delay(0);
            log.Info("C# HTTP trigger function processed a request.");

            //// parse query parameter
            //string name = req.GetQueryNameValuePairs()
            //    .FirstOrDefault(q => string.Compare(q.Key, "lat", true) == 0)
            //    .Value;

            //// Get request body
            //dynamic data = await req.Content.ReadAsAsync<object>();

            //// Set name to query string or body data
            //name = name ?? data?.name;

            //return name == null
            //    ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name on the query string or in the request body")


            //string cs = Environment.GetEnvironmentVariable("ConnectionString", EnvironmentVariableTarget.Process);
            //BlobHelper.SetConnectionString(cs);

            var config = Config.Juaneta();
            //config.Lat = Angle.FromDecimalDegrees(value.Lat);
            //config.Lon = Angle.FromDecimalDegrees(value.Lon);
            //config.MinAngle = Angle.FromDecimalDegrees(89.0);
            //config.MaxAngle = Angle.FromDecimalDegrees(91.0);

            var chunks = View.GetRelevantChunkKeys(config);

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(chunks.Select(p => p.ToString()).ToArray());
            return req.CreateResponse(HttpStatusCode.OK, json, "application/json");
        }
    }
}
