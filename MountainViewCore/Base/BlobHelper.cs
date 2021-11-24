using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MountainView.Base
{
    public static class BlobHelper
    {
        private static ConcurrentDictionary<string, CloudBlobContainer> singleton = new ConcurrentDictionary<string, CloudBlobContainer>();

        private static string connectionString;
        public static void SetConnectionString(string connectionString)
        {
            BlobHelper.connectionString = connectionString;
        }

        private static async Task<CloudBlobContainer> GetContainerAsync(string containerName, TraceListener log)
        {
            if (!singleton.TryGetValue(containerName, out CloudBlobContainer ret))
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    log?.WriteLine("Must set the 'connectionString' property prior to use");
                    throw new MountainViewException("Must set the 'connectionString' property prior to use");
                }

                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                ret = blobClient.GetContainerReference(containerName);
                await ret.CreateIfNotExistsAsync();
                singleton.AddOrUpdate(containerName, ret, (a, b) => b);
            }

            return ret;
        }

        public static async Task<DeletableFileStream> TryGetStreamAsync(string containerName, string fileName, TraceListener log)
        {
            var localFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");
            var cacheFileName = Path.Combine(Path.GetTempPath(), fileName);

            foreach (DriveInfo d in DriveInfo.GetDrives().Where(p => p.Name.ToLower()[0] == localFileName.ToLower()[0] && p.IsReady))
            {
                log?.WriteLine(string.Format("{0} has {1, 15} bytes available", d.Name, d.AvailableFreeSpace));
            }

            if (!File.Exists(cacheFileName))
            {
                try
                {
                    CloudBlockBlob blockBlob = (await GetContainerAsync(containerName, log)).GetBlockBlobReference(fileName);
                    await blockBlob.DownloadToFileAsync(localFileName, FileMode.CreateNew);
                }
                catch (Exception ex)
                {
                    log?.WriteLine("Missing blob: " + fileName);
                    log?.WriteLine("Error was:" + ex.ToString());
                    return null;
                }

                var fi = new FileInfo(localFileName);
                if (!File.Exists(cacheFileName))
                {
                    fi.CopyTo(cacheFileName);
                }
            }
            else
            {
                var fi = new FileInfo(cacheFileName);
                fi.CopyTo(localFileName);
            }

            var fs = File.OpenRead(localFileName);
            fs.Position = 0;

            var ret = new DeletableFileStream(localFileName, fs);
            return ret;
        }

       // TODO: Figure out why this was failing to compiile
        public static async Task<IEnumerable<string>> GetFileNames(string containerName, string prefix, TraceListener log)
        {
           var container = await GetContainerAsync(containerName, log);
            List<IListBlobItem> files = new List<IListBlobItem>();
            BlobContinuationToken token = null;
            do
            {
                var segment = await container.ListBlobsSegmentedAsync(prefix, token);
                files.AddRange(segment.Results);
                token = segment.ContinuationToken;
            } while (token != null);

            return files
               .Select(p => p.Uri.ToString())
               .Select(p =>
               {
                   var i = p.IndexOf("blob.core.windows.net/");
                   return p.Substring(i + 22 + containerName.Length + 1);
               })
               .ToArray();
        }

        public static async Task Rename(string containerName, string oldName, string newName, TraceListener log)
        {
            var container = await GetContainerAsync(containerName, log);
            var oldBlob = container.GetBlobReference(oldName);
            var newBlob = container.GetBlobReference(newName);

            if (await newBlob.ExistsAsync())
            {
                await newBlob.DeleteIfExistsAsync();
            }

            await newBlob.StartCopyAsync(oldBlob.Uri);
            await oldBlob.DeleteIfExistsAsync();
        }

        public static async Task Delete(string containerName, string name, TraceListener log)
        {
            var container = await GetContainerAsync(containerName, log);
            var blob = container.GetBlobReference(name);
            await blob.DeleteIfExistsAsync();
        }

        public class DeletableFileStream : IDisposable
        {
            private readonly string localFileName;
            public FileStream Stream { get; private set; }

            public DeletableFileStream(string localFileName, FileStream fs)
            {
                this.localFileName = localFileName;
                Stream = fs;
            }

            public void Seek(long offset, SeekOrigin origin)
            {
                Stream.Seek(offset, origin);
            }

            internal void Read(byte[] array, int offset, int count)
            {
                Stream.Read(array, offset, count);
            }

            internal int ReadByte()
            {
                return Stream.ReadByte();
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        Stream.Dispose();
                    }

                    File.Delete(localFileName);
                    disposedValue = true;
                }
            }

            ~DeletableFileStream()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            #endregion
        }

        public static async Task<bool> BlobExists(string containerName, string fileName, TraceListener log)
        {
            var localFileName = Path.Combine(Path.GetTempPath(), fileName.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(localFileName))
            {
                return true;
            }

            CloudBlockBlob blockBlob = (await GetContainerAsync(containerName, log)).GetBlockBlobReference(fileName);
            return await blockBlob.ExistsAsync();
        }

        public static async Task<IEnumerable<string>> ReadAllLines(string containerName, string fileName, TraceListener log)
        {
            List<string> ret = new List<string>();
            using (var stream = await TryGetStreamAsync(containerName, fileName, log))
            {
                using (var reader = new StreamReader(stream.Stream))
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

        public static async Task WriteStream(string containerName, string fileName, MemoryStream stream, TraceListener log)
        {
            CloudBlockBlob blockBlob = (await GetContainerAsync(containerName, log)).GetBlockBlobReference(fileName);
            await blockBlob.UploadFromStreamAsync(stream);
        }

        public static async Task<string> WriteStream(string containerName, string fileName, string sourceName, TraceListener log)
        {
            CloudBlockBlob blockBlob = (await GetContainerAsync(containerName, log)).GetBlockBlobReference(fileName);
            await blockBlob.UploadFromFileAsync(sourceName);
            return blockBlob.Uri.ToString();
        }

        public static async Task<IEnumerable<string>> GetDirectories(string containerName, string directoryPrefix, TraceListener log)
        {
            var blobList = await (await GetContainerAsync(containerName, log))
                .ListBlobsSegmentedAsync(directoryPrefix, false, BlobListingDetails.None, int.MaxValue, null, null, null);
            var x = blobList.Results.OfType<CloudBlobDirectory>().Select(p => p.Prefix.TrimEnd('/')).ToArray();
            return x;
        }

        public static async Task<IEnumerable<string>> GetFiles(string containerName, string directory, TraceListener log)
        {
            List<string> ret = new List<string>();
            var dir = (await GetContainerAsync(containerName, log)).GetDirectoryReference(directory);
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
