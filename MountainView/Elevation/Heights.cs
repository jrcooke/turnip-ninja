using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.IO;

namespace MountainView.Elevation
{
    public static class Heights
    {
        private const string cachedFileTemplate = "{0}.v2.hdata";
        private const string description = "Heights";
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        private static Cache<long, ChunkHolder<float>> chunkCache = new Cache<long, ChunkHolder<float>>(TimeSpan.FromSeconds(15));
        private static Dictionary<long, string> filenameCache = new Dictionary<long, string>();

        public static ChunkHolder<float> GenerateData(StandardChunkMetadata template)
        {
            ChunkHolder<float> ret;
            if (chunkCache.TryGetValue(template.Key, out ret))
            {
                return ret;
            }

            string filename;
            if (!filenameCache.TryGetValue(template.Key, out filename))
            {
                filename = string.Format("{0}{1}{2:D2}",
                    template.LatLo.ToLatString(),
                    template.LonLo.ToLonString(), template.ZoomLevel);
                filenameCache.Add(template.Key, filename);
            }

            string fullName = Path.Combine(rootMapFolder, string.Format(cachedFileTemplate, filename));
            if (File.Exists(fullName))
            {
                Console.WriteLine("Reading " + description + " chunk file '" + filename + "'to cache...");
                ret = ReadChunk(fullName, template);
                chunkCache.Add(template.Key, ret);
                Console.WriteLine("Read " + description + " chunk file '" + filename + "'to cache.");
                return ret;
            }

            // Need to generate the data
            lock (filename)
            {
                Console.WriteLine("Cached " + description + " chunk file does not exist: " + fullName);
                Console.WriteLine("Starting generation...");

                int latMin = (int)Math.Min(template.LatLo.SignedDegrees, template.LatHi.SignedDegrees);
                int latMax = (int)Math.Max(template.LatLo.SignedDegrees, template.LatHi.SignedDegrees);
                int lonMin = (int)Math.Min(template.LonLo.SignedDegrees, template.LonHi.SignedDegrees);
                int lonMax = (int)Math.Max(template.LonLo.SignedDegrees, template.LonHi.SignedDegrees);
                List<ChunkHolder<float>> chunks = new List<ChunkHolder<float>>();
                for (int latInt = latMin; latInt <= latMax; latInt++)
                {
                    for (int lonInt = lonMin; lonInt <= lonMax; lonInt++)
                    {
                        chunks.Add(UsgsRawChunks.GetRawHeightsInMeters(latInt, lonInt).Result);
                    }
                }

                ret = new ChunkHolder<float>(
                    template.LatSteps, template.LonSteps,
                    template.LatLo, template.LonLo,
                    template.LatHi, template.LonHi,
                    null,
                    p => p,
                    p => (float)p);
                ret.RenderChunksInto(chunks, Utils.WeightedFloatAverage);

                WriteChunk(ret, fullName);
                Console.WriteLine("Finished generation of " + description + " cached chunk file: " + fullName);

            }
            return ret;
        }

        private static void WriteChunk(ChunkHolder<float> ret, string fullName)
        {
            using (FileStream stream = File.OpenWrite(fullName))
            {
                stream.Write(BitConverter.GetBytes(ret.LatSteps), 0, 4);
                stream.Write(BitConverter.GetBytes(ret.LonSteps), 0, 4);
                for (int i = 0; i < ret.LatSteps; i++)
                {
                    for (int j = 0; j < ret.LonSteps; j++)
                    {
                        stream.Write(BitConverter.GetBytes(ret.Data[i][j]), 0, 4);
                    }
                }
            }
        }

        private static ChunkHolder<float> ReadChunk(string fullName, StandardChunkMetadata template)
        {
            ChunkHolder<float> ret = null;
            byte[] buffer = new byte[4];
            using (var stream = File.OpenRead(fullName))
            {
                stream.Read(buffer, 0, 4);
                int width = BitConverter.ToInt32(buffer, 0);
                stream.Read(buffer, 0, 4);
                int height = BitConverter.ToInt32(buffer, 0);

                ret = new ChunkHolder<float>(width, height,
                    template.LonLo, template.LonLo,
                    template.LatHi, template.LonHi,
                    null, null, null);
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        stream.Read(buffer, 0, 4);
                        ret.Data[i][j] = BitConverter.ToSingle(buffer, 0);
                    }
                }
            }

            return ret;
        }
    }
}
