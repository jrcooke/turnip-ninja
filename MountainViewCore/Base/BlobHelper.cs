using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MountainView.Base
{
    public static class BlobHelper
    {
        public static bool CacheLocally { get; set; }

        private static object locker = new object();
        private static Dictionary<string, CloudBlobContainer> singleton = new Dictionary<string, CloudBlobContainer>();

        private static CloudBlobContainer Container(string containerName)
        {
            if (!singleton.TryGetValue(containerName, out CloudBlobContainer ret))
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
                        var tmpName = System.Guid.NewGuid().ToString() + ".tmp";
                        await blockBlob.DownloadToFileAsync(tmpName, FileMode.CreateNew);
                        if (!File.Exists(fileName))
                        {
                            File.Move(tmpName, fileName);
                        }
                        else
                        {
                            File.Delete(tmpName);
                        }
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

        public static Task<bool> BlobExists(string containerName, string fileName)
        {
            CloudBlockBlob blockBlob = Container(containerName).GetBlockBlobReference(fileName);
            return blockBlob.ExistsAsync();
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

        public static async Task<IEnumerable<string>> GetDirectories(string containerName, string directoryPrefix)
        {
            var blobList = await Container(containerName)
                .ListBlobsSegmentedAsync(directoryPrefix, false, BlobListingDetails.None, int.MaxValue, null, null, null);
            var x = blobList.Results.OfType<CloudBlobDirectory>().Select(p => p.Prefix.TrimEnd('/')).ToArray();
            return x;
        }

        public static async Task<IEnumerable<string>> GetFiles(string containerName, string directory)
        {
            List<string> ret = new List<string>();
            var dir = Container(containerName).GetDirectoryReference(directory);
            BlobContinuationToken bcc = null;
            while (true)
            {
                var blobList = await dir.ListBlobsSegmentedAsync(
                    useFlatBlobListing: true,
                    blobListingDetails: BlobListingDetails.None,
                    maxResults: int.MaxValue,
                    currentToken: bcc,
                    options: null,
                    operationContext: null);
                bcc = blobList.ContinuationToken;
                ret.AddRange(blobList.Results.OfType<CloudBlockBlob>().Select(p => p.Name));
                if (bcc == null)
                {
                    break;
                }
            }

            return ret;
        }
    }
}
