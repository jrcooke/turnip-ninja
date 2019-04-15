using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using MountainView.Base;
using MountainViewDesktopCore.Base;
using MountainViewFn.Core;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace MountainViewFn
{
    public static class QueueRelevantChunkKeys
    {
        [FunctionName("QueueRelevantChunkKeys")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            DecConfig data = await req.Content.ReadAsAsync<DecConfig>();
            Config config = data.GetConfig();

            var log2 = new MyTraceLister(log);

            var chunks = View.GetRelevantChunkKeys(config);
            float eyeHeight = 5;
            string cs1 = Environment.GetEnvironmentVariable("ConnectionString", EnvironmentVariableTarget.Process);
            BlobHelper.SetConnectionString(cs1);
            float heightOffset = (await View.GetHeightAtPoint(config, chunks.Last(), log2)) + eyeHeight;

            string cs = Environment.GetEnvironmentVariable("ConnectionString2", EnvironmentVariableTarget.Process);
            QueueHelper.SetConnectionString(cs);

            ChunkProcess ret = new ChunkProcess()
            {
                SessionId = Guid.NewGuid().ToString(),
                Count = chunks.Length,
            };

            ChunkMetadata[] chunksToProcess = chunks
                .Select((p, i) => new ChunkMetadata()
                {
                    Order = i,
                    SessionId = ret.SessionId,
                    ChunkKey = chunks[i],
                    HeightOffset = heightOffset,
                    Config = data,
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
