using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
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

        private static CloudBlobContainer Container
        {
            get
            {
                return singleton.Value;
            }
        }

        public static async Task<MemoryStream> TryGetStream(string fileName)
        {
            try
            {
                CloudBlockBlob blockBlob = Container.GetBlockBlobReference(fileName);
                var stream = new MemoryStream();
                await blockBlob.DownloadToStreamAsync(stream);
                stream.Position = 0;
                return stream;
            }
            catch
            {
                return null;
            }
        }

        public static async Task WriteStream(string fileName, MemoryStream stream)
        {
            CloudBlockBlob blockBlob = Container.GetBlockBlobReference(fileName);
            await blockBlob.UploadFromStreamAsync(stream);
        }
    }
}
