using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Collections.Generic;

namespace MountainViewDesktopCore.Base
{
    public static class QueueHelper
    {
        private static object locker = new object();
        private static Dictionary<string, CloudQueue> singleton = new Dictionary<string, CloudQueue>();

        private static string connectionString;
        public static void SetConnectionString(string connectionString)
        {
            QueueHelper.connectionString = connectionString;
        }

        private static CloudQueue GetQueueReference(string queueName)
        {
            if (!singleton.TryGetValue(queueName, out CloudQueue ret))
            {
                lock (locker)
                {
                    if (!singleton.TryGetValue(queueName, out ret))
                    {
                        if (string.IsNullOrEmpty(connectionString))
                        {
                            throw new System.InvalidOperationException("Must set the 'connectionString' property prior to use");
                        }

                        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                        CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                        CloudQueue queue = queueClient.GetQueueReference(queueName);
                        queue.CreateIfNotExists();
                        ret = queue;
                    }
                }
            }

            return ret;
        }

        public static void Enqueue(string queueName, string content)
        {
            CloudQueueMessage message = new CloudQueueMessage(content);
            var queue = GetQueueReference(queueName);
            queue.AddMessage(message);
        }

        public static string Dequeue(string queueName)
        {
            var queue = GetQueueReference(queueName);
            var message = queue.GetMessage();
            return message.AsString;
        }
    }
}
