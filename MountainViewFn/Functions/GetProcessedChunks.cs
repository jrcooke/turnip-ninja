using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using MountainViewDesktopCore.Base;
using Newtonsoft.Json;

namespace MountainViewFn
{
    public static class GetProcessedChunks
    {
        [FunctionName("GetProcessedChunks")]
        public static HttpResponseMessage Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "GetProcessedChunks/sessionId/{sessionId}")]HttpRequestMessage req, string sessionId, TraceWriter log)
        {
            string cs2 = Environment.GetEnvironmentVariable("ConnectionString2", EnvironmentVariableTarget.Process);
            TableHelper.SetConnectionString(cs2);

            TableHelper.ChunkImageData[] data = null;
            try
            {
                data = TableHelper.GetSessionData(sessionId).OrderBy(p => p.Order).ToArray();
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
                throw;
            }

            string json = JsonConvert.SerializeObject(data);
            return req.CreateResponse(HttpStatusCode.OK, json, "application/json");
        }
    }
}
