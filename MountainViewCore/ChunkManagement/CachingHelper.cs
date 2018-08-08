using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using static MountainView.Base.BlobHelper;

namespace MountainView
{
    public abstract class CachingHelper<T>
    {
        private static readonly string cachedFileTemplate = "{0}.v8.{1}";
        private static readonly string cachedFileContainer = "mapv8";

        private readonly string fileExt;
        private readonly string description;
        private readonly int pixelDataSize;
        public int SourceDataZoom { get; private set; }
        protected readonly Func<T, double>[] toDouble;
        protected readonly Func<double[], T> fromDouble;
        protected readonly Func<int, T, T, T> aggregate;
        private readonly ConcurrentDictionary<long, string> filenameCache;

        public CachingHelper(string fileExt, string description, int pixelDataSize, int sourceDataZoom,
            Func<T, double>[] toDouble,
            Func<double[], T> fromDouble,
            Func<int, T, T, T> aggregate)
        {
            this.fileExt = fileExt;
            this.description = description;
            this.pixelDataSize = pixelDataSize;
            this.SourceDataZoom = sourceDataZoom;
            this.toDouble = toDouble;
            this.fromDouble = fromDouble;
            this.aggregate = aggregate;

            this.filenameCache = new ConcurrentDictionary<long, string>();
        }

        public string GetShortFilename(StandardChunkMetadata template)
        {
            if (!filenameCache.TryGetValue(template.Key, out string filename))
            {
                filename = GetBaseFileName(template);
                filenameCache.AddOrUpdate(template.Key, filename, (a, b) => b);
            }

            return filename;
        }

        public async Task<ChunkHolder<T>> ProcessRawData(StandardChunkMetadata template, TraceListener log)
        {
            var computedChunk = await GetComputedChunk(template, log);
            string fileName = computedChunk.Item1;
            ChunkHolder<T> ret = computedChunk.Item2;
            if (computedChunk.Item2 != null)
            {
                log?.WriteLine("Cached " + description + " chunk file exists: " + fileName);
                return computedChunk.Item2;
            }

            log?.WriteLine("Cached " + description + " chunk file does not exist: " + fileName);

            if (template.ZoomLevel > this.SourceDataZoom)
            {
                // Nothing to do for processing
                return null;
            }
            else if (template.ZoomLevel == this.SourceDataZoom)
            {
                log?.WriteLine("Starting generation...");
                ret = await GenerateData(template, log);
                await WriteChunk(ret, fileName, log);
                log?.WriteLine("Finished generation of " + description + " cached chunk file: " + fileName);
                return ret;
            }

            log?.WriteLine("Need to aggregate up from higher zoom data");
            var children = template.GetChildChunks();
            List<ChunkHolder<T>> chunks = new List<ChunkHolder<T>>();
            foreach (var child in children)
            {
                log?.WriteLine(child);
                chunks.Add(await ProcessRawData(child, log));
            }

            ret = new ChunkHolder<T>(
                 template.LatSteps, template.LonSteps,
                 template.LatLo, template.LonLo,
                 template.LatHi, template.LonHi,
                 null,
                 toDouble,
                 fromDouble);

            ret.RenderChunksInto(chunks, aggregate, log);
            await WriteChunk(ret, fileName, log);
            log?.WriteLine("Finished generation of " + description + " cached chunk file: " + fileName);

            return ret;
        }

        public async Task<ChunkHolder<T>> GetData(StandardChunkMetadata template, TraceListener log)
        {
            var computedChunk = await GetComputedChunk(template, log);
            string fileName = computedChunk.Item1;
            ChunkHolder<T> ret = computedChunk.Item2;
            if (computedChunk.Item2 != null)
            {
                log?.WriteLine("Cached " + description + " chunk file exists: " + fileName);
                return computedChunk.Item2;
            }

            log?.WriteLine("Cached " + description + " chunk file does not exist: " + fileName);

            if (template.ZoomLevel <= this.SourceDataZoom)
            {
                throw new InvalidOperationException("Source data is missing for chunk " + template.ToString());

                //log?.WriteLine("Starting generation...");
                //ret = await GenerateData(template);
                //await WriteChunk(ret, fileName);
                //log?.WriteLine("Finished generation of " + description + " cached chunk file: " + fileName);
                //return ret;
            }

            log?.WriteLine("Need to interpolate from lower zoom data");
            var parent = template.GetParentChunk();
            var chunks = new ChunkHolder<T>[] { await GetData(parent, log) };

            ret = new ChunkHolder<T>(
                 template.LatSteps, template.LonSteps,
                 template.LatLo, template.LonLo,
                 template.LatHi, template.LonHi,
                 null,
                 toDouble,
                 fromDouble);
            ret.RenderChunksInto(chunks, aggregate, log);
            await WriteChunk(ret, fileName, log);
            log?.WriteLine("Finished generation of " + description + " cached chunk file: " + fileName);
            return ret;
        }

        public async Task<NearestInterpolatingChunk<T>> GetLazySimpleInterpolator(StandardChunkMetadata template, TraceListener log)
        {
            if (template == null) return null;

            string filename = GetShortFilename(template);
            string fullFileName = GetFullFileName(template, filename);
            while (
                !(await BlobHelper.BlobExists(cachedFileContainer, fullFileName, log)) &&
                template.ZoomLevel > SourceDataZoom)
            {
                template = template.GetParentChunk();
                return await GetLazySimpleInterpolator(template, log);
            }

            byte[] buffer = new byte[Math.Max(4, pixelDataSize)];
            return new NearestInterpolatingChunk<T>(
                template.LatLo.DecimalDegree, template.LonLo.DecimalDegree,
                template.LatHi.DecimalDegree, template.LonHi.DecimalDegree,
                template.LatSteps, template.LonSteps,
                cachedFileContainer, fullFileName,
                (ms, i, j) =>
                {
                    ms.Seek(8 + pixelDataSize * (i * template.LatSteps + j), SeekOrigin.Begin);
                    return ReadPixel(ms, buffer);
                });
        }

        private async Task<Tuple<string, ChunkHolder<T>>> GetComputedChunk(StandardChunkMetadata template, TraceListener log)
        {
            string filename = GetShortFilename(template);
            Tuple<string, ChunkHolder<T>> ret = new Tuple<string, ChunkHolder<T>>(GetFullFileName(template, filename), null);
            using (var ms = await BlobHelper.TryGetStreamAsync(cachedFileContainer, ret.Item1, log))
            {
                if (ms != null)
                {
                    ret = new Tuple<string, ChunkHolder<T>>(ret.Item1, ReadChunk(ms, template));
                }
            }

            return ret;
        }

        public async Task<bool> ExistsComputedChunk(StandardChunkMetadata template, TraceListener log)
        {
            string filename = GetShortFilename(template);
            return await BlobHelper.BlobExists(cachedFileContainer, GetFullFileName(template, filename), log);
        }

        public string GetFileName(StandardChunkMetadata template)
        {
            string baseFileName = GetBaseFileName(template);
            return GetFullFileName(template, baseFileName);
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

        private async Task WriteChunk(ChunkHolder<T> ret, string fileName, TraceListener log)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                stream.Write(BitConverter.GetBytes(ret.LatSteps), 0, 4);
                stream.Write(BitConverter.GetBytes(ret.LonSteps), 0, 4);
                for (int i = 0; i < ret.LatSteps; i++)
                {
                    for (int j = 0; j < ret.LonSteps; j++)
                    {
                        WritePixel(stream, ret.Data[i][j]);
                    }
                }

                stream.Position = 0;
                await BlobHelper.WriteStream(cachedFileContainer, fileName, stream, log);
            }
        }

        private ChunkHolder<T> ReadChunk(DeletableFileStream stream, StandardChunkMetadata template)
        {
            byte[] buffer = new byte[Math.Max(4, pixelDataSize)];
            stream.Read(buffer, 0, 4);
            int width = BitConverter.ToInt32(buffer, 0);
            stream.Read(buffer, 0, 4);
            int height = BitConverter.ToInt32(buffer, 0);

            var ret = new ChunkHolder<T>(width, height,
                template.LatLo, template.LonLo,
                template.LatHi, template.LonHi,
                null, toDouble, fromDouble);
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    ret.Data[i][j] = ReadPixel(stream, buffer);
                }
            }

            return ret;
        }

        protected abstract Task<ChunkHolder<T>> GenerateData(StandardChunkMetadata template, TraceListener log);
        protected abstract void WritePixel(MemoryStream stream, T pixel);
        //protected abstract Task<T> ReadPixel(DeletableFileStream stream, byte[] buffer);
        protected abstract T ReadPixel(DeletableFileStream stream, byte[] buffer);
    }
}
