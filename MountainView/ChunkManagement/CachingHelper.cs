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
        private const string cachedFileTemplate = "{0}.v6.{1}";
        private readonly string fileExt;
        private readonly string description;
        private readonly int pixelDataSize;
        private readonly int sourceDataZoom;
        private readonly Func<T, double>[] toDouble;
        private readonly Func<double[], T> fromDouble;
        private readonly Func<int, T, T, T> aggregate;
        private readonly ConcurrentDictionary<long, string> filenameCache;

        public CachingHelper(string fileExt, string description, int pixelDataSize, int sourceDataZoom,
            Func<T, double>[] toDouble,
            Func<double[], T> fromDouble,
            Func<int, T, T, T> aggregate)
        {
            this.fileExt = fileExt;
            this.description = description;
            this.pixelDataSize = pixelDataSize;
            this.sourceDataZoom = sourceDataZoom;
            this.toDouble = toDouble;
            this.fromDouble = fromDouble;
            this.aggregate = aggregate;

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

            string fileName = string.Format(cachedFileTemplate, filename, fileExt);
            using (var ms = await BlobHelper.TryGetStream(fileName))
            {
                if (ms != null)
                {
                    Console.WriteLine("Cached " + description + " chunk file exists: " + fileName);
                    return ReadChunk(ms, template);
                }
            }

            Console.WriteLine("Cached " + description + " chunk file does not exist: " + fileName);
            if (template.ZoomLevel == this.sourceDataZoom)
            {
                //throw new InvalidDataException
                Console.WriteLine("Source data is missing for chunk " + template.ToString());

                Console.WriteLine("Starting generation...");
                try
                {
                    var ret = await GenerateData(template);
                    await WriteChunk(ret, fileName);
                    Console.WriteLine("Finished generation of " + description + " cached chunk file: " + fileName);
                    return ret;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Problem generating " + description + " cached chunk file: " + fileName);
                    Console.WriteLine(ex.ToString());
                }

            }
            else if (template.ZoomLevel < this.sourceDataZoom)
            {
                Console.WriteLine("Need to aggregate up from higher zoom data");
                var children = template.GetChildChunks();
                List<ChunkHolder<T>> chunks = new List<ChunkHolder<T>>();
                foreach (var child in children)
                {
                    Console.WriteLine(child);
                    chunks.Add(await GetData(child));
                }

                var ret = new ChunkHolder<T>(
                      template.LatSteps, template.LonSteps,
                      template.LatLo, template.LonLo,
                      template.LatHi, template.LonHi,
                      null,
                      toDouble,
                      fromDouble);

                ret.RenderChunksInto(chunks, aggregate);

                await WriteChunk(ret, fileName);
                Console.WriteLine("Finished generation of " + description + " cached chunk file: " + fileName);

                return ret;
            }
            else
            {
                Console.WriteLine("Need to interpolate from lower zoom data");
                var lower = StandardChunkMetadata.GetRangeContaingPoint(
                    Angle.Divide(Angle.Add(template.LatLo, template.LatHi), 2),
                    Angle.Divide(Angle.Add(template.LonLo, template.LonHi), 2),
                    template.ZoomLevel - 1);
                Console.WriteLine("Lower data: " + lower);
                Console.WriteLine("TODO!!!");
            }
            //Console.WriteLine("Starting generation...");
            //try
            //{
            //    var ret = await GenerateData(template);
            //    await WriteChunk(ret, fileName);
            //    Console.WriteLine("Finished generation of " + description + " cached chunk file: " + fileName);
            //    return ret;
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("Problem generating " + description + " cached chunk file: " + fileName);
            //    Console.WriteLine(ex.ToString());
            //}

            return null;
        }

        private async Task WriteChunk(ChunkHolder<T> ret, string fileName)
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
                await BlobHelper.WriteStream(fileName, stream);
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
                null, null, null);
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
