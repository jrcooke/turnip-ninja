using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MountainView.Base
{
    public static class BlobHelper
    {
        public static bool CacheLocally { get; set; }

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
                        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                        CloudBlobContainer container = blobClient.GetContainerReference(containerName);
                        container.CreateIfNotExists();
                        ret = container;
                    }
                }
            }

            return ret;
        }

        public static MemoryStream TryGetStream(string containerName, string fileName)
        {
            if (CacheLocally)
            {
                var localFileName = Path.Combine(Path.GetTempPath(), fileName.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(localFileName))
                {
                    try
                    {
                        CloudBlockBlob blockBlob = Container(containerName).GetBlockBlobReference(fileName);
                        var tmpName = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString() + ".tmp");
                        blockBlob.DownloadToFile(tmpName, FileMode.CreateNew);
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
                        System.Console.WriteLine("Missing blob: " + fileName);
                        return null;
                    }
                }

                var stream = new MemoryStream();
                var fs = File.OpenRead(localFileName);
                fs.Position = 0;
                fs.CopyTo(stream);
                stream.Position = 0;
                return stream;
            }
            else
            {
                try
                {
                    CloudBlockBlob blockBlob = Container(containerName).GetBlockBlobReference(fileName);
                    var stream = new MemoryStream();
                    blockBlob.DownloadToStream(stream);
                    stream.Position = 0;
                    return stream;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static bool BlobExists(string containerName, string fileName)
        {
            if (CacheLocally)
            {
                var localFileName = Path.Combine(Path.GetTempPath(), fileName.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(localFileName))
                {
                    return true;
                }
            }

            CloudBlockBlob blockBlob = Container(containerName).GetBlockBlobReference(fileName);
            return blockBlob.Exists();
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
            blockBlob.UploadFromStream(stream);
        }

        public static IEnumerable<string> GetDirectories(string containerName, string directoryPrefix)
        {
            var blobList = Container(containerName)
                .ListBlobsSegmented(directoryPrefix, false, BlobListingDetails.None, int.MaxValue, null, null, null);
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
                var blobList = dir.ListBlobsSegmented(
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
