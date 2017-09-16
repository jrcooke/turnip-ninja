using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Threading.Tasks;

namespace MountainView.Base
{
    public class BlobHelper
    {
        private static Lazy<CloudBlobContainer> singleton = new Lazy<CloudBlobContainer>(() =>
        {
            var connectionString = ConfigurationManager.AppSettings["ConnectionString"];
            var containerName = ConfigurationManager.AppSettings["Container"];

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            Task.WaitAll(container.CreateIfNotExistsAsync());
            return container;
        });

        public static CloudBlobContainer Container
        {
            get
            {
                return singleton.Value;
            }
        }
    }
}
