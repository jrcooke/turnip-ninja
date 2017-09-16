using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace MountainView
{
    public abstract class CachingHelper<T>
    {
        private const string cachedFileTemplate = "{0}.v5.{1}";
        private readonly string fileExt;
        private readonly string description;
        private readonly int pixelDataSize;
        private readonly ConcurrentDictionary<long, string> filenameCache;

        public CachingHelper(string fileExt, string description, int pixelDataSize)
        {
            this.fileExt = fileExt;
            this.description = description;
            this.pixelDataSize = pixelDataSize;

            this.filenameCache = new ConcurrentDictionary<long, string>();
        }

        public async Task<ChunkHolder<T>> GetData(StandardChunkMetadata template)
        {
            if (!filenameCache.TryGetValue(template.Key, out string filename))
            {
                filename = string.Format("{0}{1}{2:D2}",
                    template.LatLo.ToLatString(),
                    template.LonLo.ToLonString(),
                    template.ZoomLevel);
                filenameCache.AddOrUpdate(template.Key, filename, (a, b) => b);
            }

            CloudBlobContainer container = BlobHelper.Container;
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(string.Format(cachedFileTemplate, filename, fileExt));

            if (TryReadChunk(blockBlob, template, out ChunkHolder<T> ret))
            {
                return ret;
            }

            Console.WriteLine("Cached " + description + " chunk file does not exist: " + blockBlob.Name);
            Console.WriteLine("Starting generation...");
            try
            {
                var ret = await GenerateData(template);
                WriteChunk(ret, blockBlob);
                Console.WriteLine("Finished generation of " + description + " cached chunk file: " + blockBlob.Name);
                return ret;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Problem generating " + description + " cached chunk file: " + blockBlob.Name);
                Console.WriteLine(ex.ToString());
            }

            return null;
        }

        private void WriteChunk(ChunkHolder<T> ret, CloudBlockBlob blockBlob)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                blockBlob.UploadFromStreamAsync(stream);
                stream.Write(BitConverter.GetBytes(ret.LatSteps), 0, 4);
                stream.Write(BitConverter.GetBytes(ret.LonSteps), 0, 4);
                for (int i = 0; i < ret.LatSteps; i++)
                {
                    for (int j = 0; j < ret.LonSteps; j++)
                    {
                        WritePixel(stream, ret.Data[i][j]);
                    }
                }
            }
        }

        private bool TryReadChunk(CloudBlockBlob blockBlob, StandardChunkMetadata template, out ChunkHolder<T> ret)
        {
            ret = null;
            using (var stream = new MemoryStream())
            {
                try
                {
                    Task.WaitAll(blockBlob.DownloadToStreamAsync(stream));
                }
                catch
                {
                    return false;
                }

                byte[] buffer = new byte[Math.Max(4, pixelDataSize)];
                stream.Read(buffer, 0, 4);
                int width = BitConverter.ToInt32(buffer, 0);
                stream.Read(buffer, 0, 4);
                int height = BitConverter.ToInt32(buffer, 0);

                ret = new ChunkHolder<T>(width, height,
                    template.LatLo, template.LonLo,
                    template.LatHi, template.LonHi,
                    null, null, null);
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        ret.Data[i][j] = ReadPixel(stream, buffer);
                    }
                }
            }

            return true;
        }

        protected abstract Task<ChunkHolder<T>> GenerateData(StandardChunkMetadata template);
        protected abstract void WritePixel(MemoryStream stream, T pixel);
        protected abstract T ReadPixel(MemoryStream stream, byte[] buffer);
    }
}
