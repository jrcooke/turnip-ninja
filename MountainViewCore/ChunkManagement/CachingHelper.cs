using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MountainView
{
    public abstract class CachingHelper<T>
    {
        private static readonly string[] cachedFileTemplate = new string[]
        {
            null,
            "{0}.v7.{1}",
            "{0}.v8.{1}",
        };

        private static readonly string[] cachedFileContainer = new string[]
        {
            null,
            "mapv7",
            "mapv8",
        };

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

        public async Task<ChunkHolder<T>> ProcessRawData(StandardChunkMetadata template)
        {
            var computedChunk = await GetComputedChunk(template);
            string fileName = computedChunk.Item1;
            ChunkHolder<T> ret = computedChunk.Item2;
            if (computedChunk.Item2 != null)
            {
                Console.WriteLine("Cached " + description + " chunk file exists: " + fileName);
                return computedChunk.Item2;
            }

            Console.WriteLine("Cached " + description + " chunk file does not exist: " + fileName);

            if (template.ZoomLevel > this.SourceDataZoom)
            {
                // Nothing to do for processing
                return null;
            }
            else if (template.ZoomLevel == this.SourceDataZoom)
            {
                Console.WriteLine("Starting generation...");
                ret = await GenerateData(template);
                await WriteChunk(ret, fileName, template.Version);
                Console.WriteLine("Finished generation of " + description + " cached chunk file: " + fileName);
                return ret;
            }

            Console.WriteLine("Need to aggregate up from higher zoom data");
            var children = template.GetChildChunks();
            List<ChunkHolder<T>> chunks = new List<ChunkHolder<T>>();
            foreach (var child in children)
            {
                Console.WriteLine(child);
                chunks.Add(await ProcessRawData(child));
            }

            ret = new ChunkHolder<T>(
                 template.LatSteps, template.LonSteps,
                 template.LatLo, template.LonLo,
                 template.LatHi, template.LonHi,
                 null,
                 toDouble,
                 fromDouble);

            ret.RenderChunksInto(chunks, aggregate);
            await WriteChunk(ret, fileName, template.Version);
            Console.WriteLine("Finished generation of " + description + " cached chunk file: " + fileName);

            return ret;
        }

        public async Task ProcessRawData2(StandardChunkMetadata template)
        {
            if (!(await ExistsComputedChunk(template)))
            {
                Console.WriteLine("Cached " + description + " chunk file does not exist: " + template);
                if (template.Version == 2)
                {
                    var t1 = StandardChunkMetadata.GetRangeContaingPoint(
                        template.LatMid, template.LonMid,
                        template.ZoomLevel - 1,
                        version: 1);

                    Console.WriteLine("Need to aggregate up from v1 zoom data");
                    var children = t1.GetChildChunks();
                    List<ChunkHolder<T>> c1 = new List<ChunkHolder<T>>();
                    foreach (var child in children)
                    {
                        Console.WriteLine(child);
                        c1.Add(await GetData(child));
                    }

                    var ret = new ChunkHolder<T>(
                        template.LatSteps, template.LonSteps,
                        template.LatLo, template.LonLo,
                        template.LatHi, template.LonHi,
                        null,
                        toDouble,
                        fromDouble);

                    ret.RenderChunksInto(c1, aggregate);

                    string fileName = GetFileName(template);
                    await WriteChunk(ret, fileName, template.Version);
                    Console.WriteLine("Finished generation of " + description + " cached chunk file: " + fileName);
                }
            }
            else 
            {
                Console.WriteLine("Cached " + description + " chunk file exists: " + template);
            }


            if (template.ZoomLevel < this.SourceDataZoom)
            {
                Console.WriteLine("Need to aggregate up from higher zoom data");
                var children = template.GetChildChunks();
                foreach (var child in children)
                {
                    Console.WriteLine(child);
                    await ProcessRawData2(child);
                }
            }
        }

        public async Task<ChunkHolder<T>> GetData(StandardChunkMetadata template)
        {
            var computedChunk = await GetComputedChunk(template);
            string fileName = computedChunk.Item1;
            ChunkHolder<T> ret = computedChunk.Item2;
            if (computedChunk.Item2 != null)
            {
                Console.WriteLine("Cached " + description + " chunk file exists: " + fileName);
                return computedChunk.Item2;
            }

            Console.WriteLine("Cached " + description + " chunk file does not exist: " + fileName);

            if (template.ZoomLevel <= this.SourceDataZoom)
            {
                throw new InvalidOperationException("Source data is missing for chunk " + template.ToString());
            }

            Console.WriteLine("Need to interpolate from lower zoom data");
            var parent = template.GetParentChunk();
            var chunks = new ChunkHolder<T>[] { await GetData(parent) };

            ret = new ChunkHolder<T>(
                 template.LatSteps, template.LonSteps,
                 template.LatLo, template.LonLo,
                 template.LatHi, template.LonHi,
                 null,
                 toDouble,
                 fromDouble);
            ret.RenderChunksInto(chunks, aggregate);
            await WriteChunk(ret, fileName, template.Version);
            Console.WriteLine("Finished generation of " + description + " cached chunk file: " + fileName);
            return ret;
        }

        private async Task<Tuple<string, ChunkHolder<T>>> GetComputedChunk(StandardChunkMetadata template)
        {
            if (!filenameCache.TryGetValue(template.Key, out string filename))
            {
                filename = GetBaseFileName(template);
                filenameCache.AddOrUpdate(template.Key, filename, (a, b) => b);
            }

            Tuple<string, ChunkHolder<T>> ret = new Tuple<string, ChunkHolder<T>>(GetFullFileName(template, filename), null);
            using (var ms = await BlobHelper.TryGetStream(cachedFileContainer[template.Version], ret.Item1))
            {
                if (ms != null)
                {
                    ret = new Tuple<string, ChunkHolder<T>>(ret.Item1, ReadChunk(ms, template));
                }
            }

            return ret;
        }

        private Task<bool> ExistsComputedChunk(StandardChunkMetadata template)
        {
            if (!filenameCache.TryGetValue(template.Key, out string filename))
            {
                filename = GetBaseFileName(template);
                filenameCache.AddOrUpdate(template.Key, filename, (a, b) => b);
            }

            return BlobHelper.BlobExists(cachedFileContainer[template.Version], GetFullFileName(template, filename));
        }

        public string GetFileName(StandardChunkMetadata template)
        {
            string baseFileName = GetBaseFileName(template);
            return GetFullFileName(template, baseFileName);
        }

        private string GetFullFileName(StandardChunkMetadata template, string filename)
        {
            return string.Format(cachedFileTemplate[template.Version], filename, fileExt);
        }

        private static string GetBaseFileName(StandardChunkMetadata template)
        {
            return string.Format("{0}{1}{2:D2}",
                template.LatLo.ToLatString(),
                template.LonLo.ToLonString(),
                template.ZoomLevel);
        }

        private async Task WriteChunk(ChunkHolder<T> ret, string fileName, int version)
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
                await BlobHelper.WriteStream(cachedFileContainer[version], fileName, stream);
            }
        }

        private ChunkHolder<T> ReadChunk(MemoryStream stream, StandardChunkMetadata template)
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

        protected abstract Task<ChunkHolder<T>> GenerateData(StandardChunkMetadata template);
        protected abstract void WritePixel(MemoryStream stream, T pixel);
        protected abstract T ReadPixel(MemoryStream stream, byte[] buffer);
    }
}
