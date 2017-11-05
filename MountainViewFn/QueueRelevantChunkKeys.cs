using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using MountainView.Base;
using MountainViewCore.Base;
using MountainViewDesktopCore.Base;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace MountainViewFn
{
    public static class QueueRelevantChunkKeys
    {
        [FunctionName("QueueRelevantChunkKeys")]
        public static HttpResponseMessage Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
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

            string cs = Environment.GetEnvironmentVariable("ConnectionString2", EnvironmentVariableTarget.Process);
            QueueHelper.SetConnectionString(cs);

            ChunkProcess ret = new ChunkProcess() { SessionId = Guid.NewGuid().ToString(), Count = chunks.Length };
            ChunkMetadata[] chunksToProcess = chunks
                .Select((p, i) => new ChunkMetadata()
                {
                    Order = i,
                    SessionId = ret.SessionId,
                    ChunkKey = chunks[i]
                })
                .ToArray();
            var json = JsonConvert.SerializeObject(chunksToProcess);
            QueueHelper.Enqueue(Constants.FanOutQueue, json);

            return req.CreateResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(ret), "application/json");
        }

        private class ChunkProcess
        {
            public int Count;
            public string SessionId;
        }
    }
}
