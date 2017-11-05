using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MountainView.Base
{
    public static class BlobHelper
    {
        private static object locker = new object();
        private static ConcurrentDictionary<string, CloudBlobContainer> singleton = new ConcurrentDictionary<string, CloudBlobContainer>();

        private static string connectionString;
        public static void SetConnectionString(string connectionString)
        {
            BlobHelper.connectionString = connectionString;
        }

        private static async Task<CloudBlobContainer> GetContainerAsync(string containerName)
        {
            if (!singleton.TryGetValue(containerName, out CloudBlobContainer ret))
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new System.InvalidOperationException("Must set the 'connectionString' property prior to use");
                }

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference(containerName);
                await container.CreateIfNotExistsAsync();
                ret = container;
                singleton.AddOrUpdate(containerName, ret, (a, b) => b);
            }

            return ret;
        }

        public static async Task<FileStream> TryGetStreamAsync(string containerName, string fileName)
        {
            {
                var localFileName = Path.Combine(Path.GetTempPath(), fileName.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(localFileName))
                {
                    try
                    {
                        CloudBlockBlob blockBlob = (await GetContainerAsync(containerName)).GetBlockBlobReference(fileName);
                        var tmpName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");
                        await blockBlob.DownloadToFileAsync(tmpName, FileMode.CreateNew);

                        if (!File.Exists(localFileName))
                        {
                            File.Move(tmpName, localFileName);
                        }
                        else
                        {
                            File.Delete(tmpName);
                        }
                    }
                    catch
                    {
                        Console.WriteLine("Missing blob: " + fileName);
                        return null;
                    }
                }

                var fs = File.OpenRead(localFileName);
                fs.Position = 0;
                return fs;
            }
        }

        public static async Task<bool> BlobExists(string containerName, string fileName)
        {
            var localFileName = Path.Combine(Path.GetTempPath(), fileName.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(localFileName))
            {
                return true;
            }

            CloudBlockBlob blockBlob = (await GetContainerAsync(containerName)).GetBlockBlobReference(fileName);
            return await blockBlob.ExistsAsync();
        }

        public static async Task<IEnumerable<string>> ReadAllLines(string containerName, string fileName)
        {
            List<string> ret = new List<string>();
            using (var stream = await TryGetStreamAsync(containerName, fileName))
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
            CloudBlockBlob blockBlob = (await GetContainerAsync(containerName)).GetBlockBlobReference(fileName);
            await blockBlob.UploadFromStreamAsync(stream);
        }

        public static async Task<string> WriteStream(string containerName, string fileName, string sourceName)
        {
            CloudBlockBlob blockBlob = (await GetContainerAsync(containerName)).GetBlockBlobReference(fileName);
            await blockBlob.UploadFromFileAsync(sourceName);
            return blockBlob.Uri.ToString();
        }

        public static async Task<IEnumerable<string>> GetDirectories(string containerName, string directoryPrefix)
        {
            var blobList = await (await GetContainerAsync(containerName))
                .ListBlobsSegmentedAsync(directoryPrefix, false, BlobListingDetails.None, int.MaxValue, null, null, null);
            var x = blobList.Results.OfType<CloudBlobDirectory>().Select(p => p.Prefix.TrimEnd('/')).ToArray();
            return x;
        }

        public static async Task<IEnumerable<string>> GetFiles(string containerName, string directory)
        {
            List<string> ret = new List<string>();
            var dir = (await GetContainerAsync(containerName)).GetDirectoryReference(directory);
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
