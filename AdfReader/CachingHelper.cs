using System;
using System.IO;

namespace AdfReader
{
    public class CachingHelper<T>
    {
        private int SmallBatch;
        private string cachedFileTemplate;
        private string description;
        private object locker = new object();
        private Action<T, FileStream> writeElement;
        private Func<FileStream, byte[], T> readElement;
        private Func<int, int, int, int, int, int, int, int, T[][]> generateData;

        private Cache<int, T[][]> chunkCache;

        public CachingHelper(
            int smallBatch,
            string cachedFileTemplate,
            string description,
            Action<T, FileStream> writeElement,
            Func<FileStream, byte[], T> readElement,
            Func<int, int, int, int, int, int, int, int, T[][]> generateData)
        {
            chunkCache = new Cache<int, T[][]>(TimeSpan.FromSeconds(15));
            SmallBatch = smallBatch;
            this.cachedFileTemplate = cachedFileTemplate;
            this.description = description;
            this.writeElement = writeElement;
            this.readElement = readElement;
            this.generateData = generateData;
        }

        public T GetValue(double lat, double lon, double cosLat, double metersPerElement)
        {
            // Size of a degree of lon here
            var len = Utils.LengthOfLatDegree * cosLat;

            // Chunks are Size minutes across, with SmallBatch elements.
            // So elements are Size / SmallBatch minutes large.
            // The length of smallest size of an element in meters is
            //     Size / SmallBatch / 60 degrees * LenOfDegree * cosLat.
            // Setting this equal to metersPerElement, and using
            //     size = (int)(3 * Math.Pow(2, 12 - zoomLevel));

            var zoomLevel = (int)(12 - Math.Log(metersPerElement * SmallBatch * 20 / len, 2));

            var chunk = GetValuesFromCache(lat, lon, ref zoomLevel);
            var size = (int)(3 * Math.Pow(2, 12 - zoomLevel));

            int minLatTotMin = Math.Min(Utils.AddAwayFromZero(Utils.TruncateTowardsZero(lat * 60) / size, 1) * size, (Utils.TruncateTowardsZero(lat * 60) / size) * size);
            int minLonTotMin = Math.Min(Utils.AddAwayFromZero(Utils.TruncateTowardsZero(lon * 60) / size, 1) * size, (Utils.TruncateTowardsZero(lon * 60) / size) * size);

            int targetLat = (int)Math.Round(((lat * 60 - minLatTotMin) * SmallBatch / size));
            int targetLon = (int)Math.Round(((lon * 60 - minLonTotMin) * SmallBatch / size));

            return chunk[targetLat][targetLon];
        }

        public T[][] GetValuesFromCache(double lat, double lon, ref int zoomLevel)
        {
            if (zoomLevel > 12)
            {
                zoomLevel = 12;
            }

            if (zoomLevel > 12 || zoomLevel < 4)
            {
                throw new ArgumentOutOfRangeException("zoomLevel");
            }

            // The size of the chunk in minutes.
            var size = (int)(3 * Math.Pow(2, 12 - zoomLevel));

            int latTotMin = Utils.AddAwayFromZero(Utils.TruncateTowardsZero(lat * 60) / size, 1) * size;
            int lonTotMin = Utils.AddAwayFromZero(Utils.TruncateTowardsZero(lon * 60) / size, 1) * size;

            int latTotMin2 = (Utils.TruncateTowardsZero(lat * 60) / size) * size;
            int lonTotMin2 = (Utils.TruncateTowardsZero(lon * 60) / size) * size;

            int minLatTotMin = Math.Min(latTotMin, latTotMin2);
            int minLonTotMin = Math.Min(lonTotMin, lonTotMin2);

            int key = Utils.GetKey(zoomLevel, latTotMin, lonTotMin);

            T[][] ret = null;
            while (!chunkCache.TryGetValue(key, out ret) || ret == null)
            {
                string filename = Utils.GetFileName(key);
                string fullName = string.Format(cachedFileTemplate, filename);
                lock (locker)
                {
                    if (!File.Exists(fullName))
                    {
                        Console.WriteLine("Cached " + description + " chunk file does not exist: " + fullName);
                        Console.WriteLine("Starting generation...");
                        ret = generateData(zoomLevel, size, latTotMin, lonTotMin, latTotMin2, lonTotMin2, minLatTotMin, minLonTotMin);

                        WriteChunk(ret, fullName);
                        Console.WriteLine("Finished generation of " + description + " cached chunk file: " + fullName);
                    }

                    while (!chunkCache.TryGetValue(key, out ret) || ret == null)
                    {
                        Console.WriteLine("Reading " + description + " chunk file '" + filename + "'to cache...");
                        ret = ReadChunk(fullName);
                        chunkCache.Add(key, ret);
                        Console.WriteLine("Read " + description + " chunk file '" + filename + "'to cache.");
                    }
                }
            }

            return ret;
        }

        private void WriteChunk(T[][] ret, string fullName)
        {
            using (FileStream stream = File.OpenWrite(fullName))
            {
                stream.Write(BitConverter.GetBytes(ret.Length), 0, 4);
                stream.Write(BitConverter.GetBytes(ret[0].Length), 0, 4);
                for (int i = 0; i < ret.Length; i++)
                {
                    for (int j = 0; j < ret[i].Length; j++)
                    {
                        writeElement(ret[i][j], stream);
                    }
                }
            }
        }
        private T[][] ReadChunk(string fullName)
        {
            T[][] ret = null;
            byte[] buffer = new byte[4];
            using (var stream = File.OpenRead(fullName))
            {
                int width = Utils.ReadInt(stream, buffer);
                int height = Utils.ReadInt(stream, buffer);

                ret = new T[width][];
                for (int i = 0; i < width; i++)
                {
                    ret[i] = new T[height];
                    for (int j = 0; j < height; j++)
                    {
                        ret[i][j] = readElement(stream, buffer);
                    }
                }
            }

            return ret;
        }
    }
}
