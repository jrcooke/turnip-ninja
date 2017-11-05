using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using MountainViewDesktopCore.Base;
using Newtonsoft.Json;
using System;

namespace MountainViewFn
{
    public static class ProcessFanOutQueue
    {
        [FunctionName("ProcessFanOutQueue")]
        public static void Run([QueueTrigger(Constants.FanOutQueue, Connection = "ConnectionString2")]string myQueueItem, TraceWriter log)
        {
            string cs = Environment.GetEnvironmentVariable("ConnectionString2", EnvironmentVariableTarget.Process);
            QueueHelper.SetConnectionString(cs);

            var chunkMetadatas = JsonConvert.DeserializeObject<ChunkMetadata[]>(myQueueItem);
            foreach (var chunkMetadata in chunkMetadatas)
            {
                var json = JsonConvert.SerializeObject(chunkMetadata);
                QueueHelper.Enqueue(Constants.ChunkQueueName, json);
            }
        }
    }
}
