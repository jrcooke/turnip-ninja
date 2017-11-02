using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MountainView.Base
{
    public static class BlobHelper
    {
        private static object locker = new object();
        private static Dictionary<string, CloudBlobContainer> singleton = new Dictionary<string, CloudBlobContainer>();

        private static string connectionString;
        public static void SetConnectionString(string connectionString)
        {
            BlobHelper.connectionString = connectionString;
        }

        private static CloudBlobContainer Container(string containerName)
        {
            if (!singleton.TryGetValue(containerName, out CloudBlobContainer ret))
            {
                lock (locker)
                {
                    if (!singleton.TryGetValue(containerName, out ret))
                    {
                        if (string.IsNullOrEmpty(connectionString))
                        {
                            throw new System.InvalidOperationException("Must set the 'connectionString' property prior to use");
                        }

                        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                        CloudBlobContainer container = blobClient.GetContainerReference(containerName);
#if !JDESKTOP
                        System.Threading.Tasks.Task.WaitAll(container.CreateIfNotExistsAsync());
#else
                        container.CreateIfNotExists();
#endif
                        ret = container;
                    }
                }
            }

            return ret;
        }

        public static FileStream TryGetStream(string containerName, string fileName)
        {
            {
                var localFileName = Path.Combine(Path.GetTempPath(), fileName.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(localFileName))
                {
                    try
                    {
                        CloudBlockBlob blockBlob = Container(containerName).GetBlockBlobReference(fileName);
                        var tmpName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");
#if !JDESKTOP
                        System.Threading.Tasks.Task.WaitAll(blockBlob.DownloadToFileAsync(tmpName, FileMode.CreateNew));
#else
                        blockBlob.DownloadToFile(tmpName, FileMode.CreateNew);
#endif

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

        public static bool BlobExists(string containerName, string fileName)
        {
            var localFileName = Path.Combine(Path.GetTempPath(), fileName.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(localFileName))
            {
                return true;
            }

            CloudBlockBlob blockBlob = Container(containerName).GetBlockBlobReference(fileName);
#if !JDESKTOP
            return blockBlob.ExistsAsync().Result;
#else
            return blockBlob.Exists();
#endif
        }

        public static IEnumerable<string> ReadAllLines(string containerName, string fileName)
        {
            List<string> ret = new List<string>();
            using (var stream = TryGetStream(containerName, fileName))
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

        public static void WriteStream(string containerName, string fileName, MemoryStream stream)
        {
            CloudBlockBlob blockBlob = Container(containerName).GetBlockBlobReference(fileName);
#if !JDESKTOP
            System.Threading.Tasks.Task.WaitAll(blockBlob.UploadFromStreamAsync(stream));
#else
            blockBlob.UploadFromStream(stream);
#endif
        }

        public static string WriteStream(string containerName, string fileName, string sourceName)
        {
            CloudBlockBlob blockBlob = Container(containerName).GetBlockBlobReference(fileName);
#if !JDESKTOP
            System.Threading.Tasks.Task.WaitAll(blockBlob.UploadFromFileAsync(sourceName));
#else
            blockBlob.UploadFromFile(sourceName);
#endif
            return blockBlob.Uri.ToString();
        }

        public static IEnumerable<string> GetDirectories(string containerName, string directoryPrefix)
        {
            var blobList = Container(containerName)
#if !JDESKTOP
                .ListBlobsSegmentedAsync(directoryPrefix, false, BlobListingDetails.None, int.MaxValue, null, null, null).Result;
#else
                .ListBlobsSegmented(directoryPrefix, false, BlobListingDetails.None, int.MaxValue, null, null, null);
#endif
            var x = blobList.Results.OfType<CloudBlobDirectory>().Select(p => p.Prefix.TrimEnd('/')).ToArray();
            return x;
        }

        public static IEnumerable<string> GetFiles(string containerName, string directory)
        {
            List<string> ret = new List<string>();
            var dir = Container(containerName).GetDirectoryReference(directory);
            BlobContinuationToken bcc = null;
            while (true)
            {
#if !JDESKTOP
                var blobList = dir.ListBlobsSegmentedAsync(
#else
                var blobList = dir.ListBlobsSegmented(
#endif
                    useFlatBlobListing: true,
                    blobListingDetails: BlobListingDetails.None,
                    maxResults: int.MaxValue,
                    currentToken: bcc,
                    options: null,
                    operationContext: null)
#if !JDESKTOP
                    .Result
#endif
                    ;
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
