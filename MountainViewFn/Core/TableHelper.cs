using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Collections.Generic;
using System.Linq;

namespace MountainViewDesktopCore.Base
{
    public static class TableHelper
    {
        private static string tableName = "chunktable";
        private static object locker = new object();
        private static Dictionary<string, CloudTable> singleton = new Dictionary<string, CloudTable>();

        private static string connectionString;
        public static void SetConnectionString(string connectionString)
        {
            TableHelper.connectionString = connectionString;
        }

        private static CloudTable GetTableReference(string tableName)
        {
            if (!singleton.TryGetValue(tableName, out CloudTable ret))
            {
                lock (locker)
                {
                    if (!singleton.TryGetValue(tableName, out ret))
                    {
                        if (string.IsNullOrEmpty(connectionString))
                        {
                            throw new System.InvalidOperationException("Must set the 'connectionString' property prior to use");
                        }

                        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                        CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
                        CloudTable table = tableClient.GetTableReference(tableName);
                        table.CreateIfNotExists();
                        ret = table;
                    }
                }
            }

            return ret;
        }

        public static void Insert(string sessionId, int order, string imageUrl, string imageMap)
        {
            var table = GetTableReference(tableName);
            ChunkImageEntity chunkdata = new ChunkImageEntity(sessionId, order, imageUrl, imageMap);
            TableOperation insertOperation = TableOperation.Insert(chunkdata);
            table.Execute(insertOperation);
        }

        public static IEnumerable<ChunkImageData> GetSessionData(string sessionId)
        {
            var table = GetTableReference(tableName);
            TableQuery<ChunkImageEntity> query = new TableQuery<ChunkImageEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, sessionId));
            var entities = table.ExecuteQuery(query).ToArray();
            var ret = entities.Select(p => p.ToPoco()).ToArray();
            return ret;
        }

        private class ChunkImageEntity : TableEntity
        {
            public ChunkImageEntity(string sessionId, int order, string imageUrl, string imageMap)
            {
                PartitionKey = sessionId;
                RowKey = order.ToString();
                ImageUrl = imageUrl;
                ImageMap = imageMap;
            }

            public ChunkImageEntity() { }

            public string ImageUrl { get; set; }

            public string ImageMap { get; set; }

            public ChunkImageData ToPoco()
            {
                return new ChunkImageData() { Order = int.Parse(RowKey), ImageMap = this.ImageMap, ImageUrl = this.ImageUrl };
            }
        }

        public class ChunkImageData
        {
            public int Order { get; set; }

            public string ImageUrl { get; set; }

            public string ImageMap { get; set; }
        }
    }
}
