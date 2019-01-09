using Microsoft.WindowsAzure.Storage;
using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MountainView.Imaging
{
    public class JpegImages
    {
        private static readonly string cachedFileTemplate = "{0}.v8.{1}";
        private static readonly string cachedFileContainer = "mapv8";

        private readonly string fileExt = "jpegdata";
        private readonly string description = "JpegImages";
        public int SourceDataZoom { get; private set; } = 5;
        private readonly ConcurrentDictionary<long, string> filenameCache = new ConcurrentDictionary<long, string>();

        private static Lazy<JpegImages> current = new Lazy<JpegImages>(() => new JpegImages());

        public static JpegImages Current
        {
            get
            {
                return current.Value;
            }
        }

        private JpegImages()
        {
        }

        public async Task<byte[]> GetData(StandardChunkMetadata template, TraceListener log)
        {
            var computedChunk = await GetComputedChunk(template, log);
            string fileName = computedChunk.Item1;
            byte[] imageData = computedChunk.Item2;
            if (computedChunk.Item2 != null)
            {
                log?.WriteLine("Cached " + description + " chunk (" + template.ToString() + ") file exists: " + fileName);
                return computedChunk.Item2;
            }

            log?.WriteLine("Cached " + description + " chunk (" + template.ToString() + ") file does not exist: " + fileName + ", so starting generation...");

            MemoryStream ms = null;
            try
            {
                var pixels = await Images.Current.GetData(template, log);
                if (pixels != null)
                {
                    ms = Utils.GetBitmap(pixels, a => a, OutputType.JPEG);
                }
            }
            catch
            {
            }

            if (ms == null)
            {
                throw new InvalidOperationException("Source image not found for chunk " + template.ToString());
            }

            imageData = new byte[ms.Length];
            ms.Seek(0, SeekOrigin.Begin);
            ms.Read(imageData, 0, imageData.Length);

            await WriteChunk(imageData, fileName, log);
            log?.WriteLine("Finished generation of " + description + " cached chunk (" + template.ToString() + ") file: " + fileName);
            return imageData;
        }

        private async Task<Tuple<string, byte[]>> GetComputedChunk(StandardChunkMetadata template, TraceListener log)
        {
            string filename = GetShortFilename(template);
            Tuple<string, byte[]> ret = new Tuple<string, byte[]>(GetFullFileName(template, filename), null);
            try
            {
                using (BlobHelper.DeletableFileStream ms = await BlobHelper.TryGetStreamAsync(cachedFileContainer, ret.Item1, log))
                {
                    if (ms != null)
                    {
                        ret = new Tuple<string, byte[]>(ret.Item1, ReadChunk(ms, template));
                    }
                }
            }
            catch (StorageException stex)
            {
                if (stex.RequestInformation.HttpStatusCode == 404)
                {
                    log?.WriteLine("Blob not found;");
                }
                else
                {
                    throw;
                }
            }

            return ret;
        }

        private string GetShortFilename(StandardChunkMetadata template)
        {
            if (!filenameCache.TryGetValue(template.Key, out string filename))
            {
                filename = GetBaseFileName(template);
                filenameCache.AddOrUpdate(template.Key, filename, (a, b) => b);
            }

            return filename;
        }

        private string GetFullFileName(StandardChunkMetadata template, string filename)
        {
            return string.Format(cachedFileTemplate, filename, fileExt);
        }

        private static string GetBaseFileName(StandardChunkMetadata template)
        {
            return string.Format("{0}{1}{2:D2}",
                template.LatLo.ToLatString(),
                template.LonLo.ToLonString(),
                template.ZoomLevel);
        }

        private async Task WriteChunk(byte[] ret, string fileName, TraceListener log)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                stream.Write(ret, 0, ret.Length);
                stream.Position = 0;
                await BlobHelper.WriteStream(cachedFileContainer, fileName, stream, log);
            }
        }

        private byte[] ReadChunk(BlobHelper.DeletableFileStream stream, StandardChunkMetadata template)
        {
            byte[] imageData = new byte[stream.Stream.Length];
            stream.Seek(0, SeekOrigin.Begin);
            stream.Read(imageData, 0, imageData.Length);
            return imageData;
        }
    }
}
