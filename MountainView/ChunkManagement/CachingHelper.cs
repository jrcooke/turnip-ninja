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
        private const string cachedFileTemplate = "{0}.v5.{1}";
        private readonly string fileExt;
        private readonly string description;
        private readonly string rootMapFolder;
        private readonly int pixelDataSize;
        private readonly ConcurrentDictionary<long, string> filenameCache;

        public CachingHelper(string fileExt, string description, int pixelDataSize)
        {
            this.fileExt = fileExt;
            this.description = description;
            this.pixelDataSize = pixelDataSize;
            this.rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];
            this.filenameCache = new ConcurrentDictionary<long, string>();
        }

        public IEnumerable<Tuple<string, ChunkHolder<T>>> ScanAll()
        {
            DirectoryInfo di = new DirectoryInfo(rootMapFolder);
            foreach (var file in di.GetFiles(string.Format(cachedFileTemplate, "*", fileExt)))
            {
                yield return new Tuple<string, ChunkHolder<T>>(file.FullName, ReadChunk(file.FullName, StandardChunkMetadata.GetEmpty()));
            }
        }

        public async Task<ChunkHolder<T>> GetData(StandardChunkMetadata template)
        {
            ChunkHolder<T> ret = null;

            if (!filenameCache.TryGetValue(template.Key, out string filename))
            {
                filename = string.Format("{0}{1}{2:D2}",
                    template.LatLo.ToLatString(),
                    template.LonLo.ToLonString(), template.ZoomLevel);
                filenameCache.AddOrUpdate(template.Key, filename, (a, b) => b);
            }

            string fullName = Path.Combine(rootMapFolder, string.Format(cachedFileTemplate, filename, fileExt));
            if (File.Exists(fullName))
            {
                Console.WriteLine("Reading " + description + " chunk file '" + filename);
                ret = ReadChunk(fullName, template);
                Console.WriteLine("Read " + description + " chunk file '" + filename);
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
