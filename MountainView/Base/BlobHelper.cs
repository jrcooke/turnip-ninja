using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace MountainView.Base
{
    public static class BlobHelper
    {
        public static bool CacheLocally { get; set; }

        private static object locker = new object();
        private static Dictionary<string, CloudBlobContainer> singleton = new Dictionary<string, CloudBlobContainer>();

        private static CloudBlobContainer Container(string containerName)
        {
            CloudBlobContainer ret;
            if (!singleton.TryGetValue(containerName, out ret))
            {
                lock (locker)
                {
                    if (!singleton.TryGetValue(containerName, out ret))
                    {
                        var connectionString = ConfigurationManager.AppSettings["ConnectionString"];
                        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                        CloudBlobContainer container = blobClient.GetContainerReference(containerName);
                        Task.WaitAll(container.CreateIfNotExistsAsync());
                        ret = container;
                    }
                }
            }

            return ret;
        }

        public static async Task<MemoryStream> TryGetStream(string containerName, string fileName)
        {
            if (CacheLocally)
            {
                if (!File.Exists(fileName))
                {
                    try
                    {
                        CloudBlockBlob blockBlob = Container(containerName).GetBlockBlobReference(fileName);
                        await blockBlob.DownloadToFileAsync(fileName, FileMode.CreateNew);
                    }
                    catch
                    {
                        return null;
                    }
                }

                var stream = new MemoryStream();
                var fs = File.OpenRead(fileName);
                fs.Position = 0;
                await fs.CopyToAsync(stream);
                stream.Position = 0;
                return stream;
            }
            else
            {
                try
                {
                    CloudBlockBlob blockBlob = Container(containerName).GetBlockBlobReference(fileName);
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
        }

        public static async Task<IEnumerable<string>> ReadAllLines(string containerName, string fileName)
        {
            List<string> ret = new List<string>();
            using (var stream = await TryGetStream(containerName, fileName))
            {
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        ret.Add(line);
                    }
                }
            }

            return ret;
        }

        public static async Task WriteStream(string containerName, string fileName, MemoryStream stream)
        {
            CloudBlockBlob blockBlob = Container(containerName).GetBlockBlobReference(fileName);
            await blockBlob.UploadFromStreamAsync(stream);
        }

        internal static async Task<IEnumerable<string>> GetDirectories(string containerName, string directoryPrefix)
        {
            var blobList = await Container(containerName)
                .ListBlobsSegmentedAsync(directoryPrefix, false, BlobListingDetails.None, int.MaxValue, null, null, null);
            var x = blobList.Results.OfType<CloudBlobDirectory>().Select(p => p.Prefix.TrimEnd('/')).ToArray();
            return x;
        }

        internal static async Task<IEnumerable<string>> GetFiles(string containerName, string directory)
        {
            var dir = Container(containerName).GetDirectoryReference(directory);
            var blobList = await dir.ListBlobsSegmentedAsync(true, BlobListingDetails.None, int.MaxValue, null, null, null);
            var x = blobList.Results.OfType<CloudBlockBlob>().Select(p => p.Name).ToArray();
            return x;
        }
    }
}
