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
            log.Info($"C# Queue trigger function processed: {myQueueItem}");

            string cs = Environment.GetEnvironmentVariable("ConnectionString2", EnvironmentVariableTarget.Process);
            QueueHelper.SetConnectionString(cs);

            var chunks = JsonConvert.DeserializeObject<QueueRelevantChunkKeys.ChunkMetadata[]>(myQueueItem);
            foreach (var data in chunks)
            {
                var json = JsonConvert.SerializeObject(data);
                QueueHelper.Enqueue(Constants.ChunkQueueName, json);
            }
        }
    }
}

