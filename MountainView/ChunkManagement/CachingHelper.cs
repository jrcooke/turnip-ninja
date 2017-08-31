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
        private readonly string cachedFileTemplate;
        private readonly string description;
        private readonly string rootMapFolder;
        private readonly int pixelDataSize;
        //        private readonly TimedCache<long, ChunkHolder<T>> chunkCache;
        private readonly ConcurrentDictionary<long, string> filenameCache;

        public CachingHelper(string cachedFileTemplate, string description, int pixelDataSize)
        {
            this.cachedFileTemplate = cachedFileTemplate;
            this.description = description;
            this.pixelDataSize = pixelDataSize;
            this.rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];
            //            this.chunkCache = new TimedCache<long, ChunkHolder<T>>(TimeSpan.FromSeconds(15));
            this.filenameCache = new ConcurrentDictionary<long, string>();
        }

        public async Task<ChunkHolder<T>> GetData(StandardChunkMetadata template)
        {
            ChunkHolder<T> ret = null;
            //    if (chunkCache.TryGetValue(template.Key, out ChunkHolder<T>  ret))
            //    {
            //        return ret;
            //    }

            if (!filenameCache.TryGetValue(template.Key, out string filename))
            {
                filename = string.Format("{0}{1}{2:D2}",
                    template.LatLo.ToLatString(),
                    template.LonLo.ToLonString(), template.ZoomLevel);
                filenameCache.AddOrUpdate(template.Key, filename, (a, b) => b);
            }

            string fullName = Path.Combine(rootMapFolder, string.Format(cachedFileTemplate, filename));
            if (File.Exists(fullName))
            {
                Console.WriteLine("Reading " + description + " chunk file '" + filename);// + "'to cache...");
                ret = ReadChunk(fullName, template);
                //chunkCache.Add(template.Key, ret);
                Console.WriteLine("Read " + description + " chunk file '" + filename);// + "'to cache.");
                return ret;
            }

            // Need to generate the data
            // TODO: need to implement some sort of design where we know in advance that we don't have more than one filename at once.
            //            lock (filename)
            {
                Console.WriteLine("Cached " + description + " chunk file does not exist: " + fullName);
                Console.WriteLine("Starting generation...");

                try
                {
                    ret = await GenerateData(template);
                    WriteChunk(ret, fullName);
                    // chunkCache.Add(template.Key, ret);
                    Console.WriteLine("Finished generation of " + description + " cached chunk file: " + fullName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Problem generating " + description + " cached chunk file: " + fullName);
                    Console.WriteLine(ex.ToString());
                }
            }

            return ret;
        }

        protected void WriteChunk(ChunkHolder<T> ret, string fullName)
        {
            using (FileStream stream = File.OpenWrite(fullName))
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
            }
        }

        protected ChunkHolder<T> ReadChunk(string fullName, StandardChunkMetadata template)
        {
            ChunkHolder<T> ret = null;
            byte[] buffer = new byte[Math.Max(4, pixelDataSize)];
            using (var stream = File.OpenRead(fullName))
            {
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

            return ret;
        }

        protected abstract Task<ChunkHolder<T>> GenerateData(StandardChunkMetadata template);
        protected abstract void WritePixel(FileStream stream, T pixel);
        protected abstract T ReadPixel(FileStream stream, byte[] buffer);
    }
}
