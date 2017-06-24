using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Linq;

namespace AdfReader
{
    public static class Heights
    {
        private const int smallBatch = 540; // Number of 1/3 arc seconds per 3 minutes.
        private const string cachedFileTemplate = "heightCache{0}.adf";
        private const string description = "Heights";
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        private static CachingHelper<Tuple<float, float, float>> ch = new CachingHelper<Tuple<float, float, float>>(
            smallBatch,
            Path.Combine(rootMapFolder, cachedFileTemplate),
            description,
            WriteElement,
            ReadElement,
            GenerateData);

        public static Tuple<float, float, float> GetHeight(double lat, double lon, double cosLat, double metersPerElement)
        {
            return ch.GetValue(lat, lon, cosLat, metersPerElement);
        }

        private static void WriteElement(Tuple<float, float, float> item, FileStream stream)
        {
            if (item != null)
            {
                stream.Write(BitConverter.GetBytes(item.Item1), 0, 4);
                stream.Write(BitConverter.GetBytes(item.Item2), 0, 4);
                stream.Write(BitConverter.GetBytes(item.Item3), 0, 4);
            }
            else
            {
                stream.Write(BitConverter.GetBytes(float.NaN), 0, 4);
                stream.Write(BitConverter.GetBytes(float.NaN), 0, 4);
                stream.Write(BitConverter.GetBytes(float.NaN), 0, 4);
            }
        }

        private static Tuple<float, float, float> ReadElement(FileStream stream, byte[] buffer)
        {
            stream.Read(buffer, 0, 4);
            float i1 = BitConverter.ToSingle(buffer, 0);
            stream.Read(buffer, 0, 4);
            float i2 = BitConverter.ToSingle(buffer, 0);
            stream.Read(buffer, 0, 4);
            float i3 = BitConverter.ToSingle(buffer, 0);
            return new Tuple<float, float, float>(i1, i2, i3);
        }

        private static Tuple<float, float, float>[][] GenerateData(
            int zoomLevel,
            int size,
            int latTotMin,
            int lonTotMin,
            int latTotMin2,
            int lonTotMin2,
            int minLatTotMin,
            int minLonTotMin)
        {
            Tuple<float, float, float>[][] ret = new Tuple<float, float, float>[smallBatch + 1][];
            for (int i = 0; i <= smallBatch; i++)
            {
                ret[i] = new Tuple<float, float, float>[smallBatch + 1];
            }

            var chunks = RawChunks.GetRawHeightsInMeters(latTotMin / 60.0, lonTotMin / 60.0, latTotMin2 / 60.0, lonTotMin2 / 60.0);
            foreach (var chunk in chunks)
            {
                LoadRawChunksIntoProcessedChunk(size, minLatTotMin, minLonTotMin, ret, chunk);
            }

            return ret;
        }

        private static void LoadRawChunksIntoProcessedChunk(
            int size,
            int minLatTotMin,
            int minLonTotMin,
            Tuple<float, float, float>[][] ret,
            Tuple<double, double, float[][]> chunk)
        {
            int minLatRoot = Math.Min(
                Utils.TruncateTowardsZero(chunk.Item1),
                Utils.AddAwayFromZero(Utils.TruncateTowardsZero(chunk.Item1), 1));
            int minLonRoot = Math.Min(
                Utils.TruncateTowardsZero(chunk.Item2),
                Utils.AddAwayFromZero(Utils.TruncateTowardsZero(chunk.Item2), 1));
            for (int j = 0; j <= RawChunks.trueElements; j++)
            {
                // The chunk has smallBatch+1 elements, so each element is
                // TargetElementCoord = angle * smallBatch / Size

                float lat2 = (float)(minLatRoot + j * 1.0 / RawChunks.trueElements);
                int targetDeltaLat = (int)Math.Round(((lat2 * 60 - minLatTotMin) * smallBatch / size));
                if (targetDeltaLat >= 0 && targetDeltaLat <= smallBatch)
                {
                    for (int i = 0; i <= RawChunks.trueElements; i++)
                    {
                        float lon2 = (float)(minLonRoot + i * 1.0 / RawChunks.trueElements);
                        int targetLon = (int)Math.Round(((lon2 * 60 - minLonTotMin) * smallBatch / size));
                        if (targetLon >= 0 && targetLon <= smallBatch)
                        {
                            var val = chunk.Item3[RawChunks.trueElements - 1 - j + RawChunks.boundaryElements][i + RawChunks.boundaryElements];
                            var cur = ret[targetDeltaLat][targetLon];
                            if (cur == null || cur.Item3 < val)
                            {
                                ret[targetDeltaLat][targetLon] = new Tuple<float, float, float>(lat2, lon2, val);
                            }
                        }
                    }
                }
            }
        }
    }

    internal static class RawChunks
    {
        private const string description = "USGS";
        public const int boundaryElements = 6;
        public const int trueElements = 10800; // Number of 1/3 arc seconds per degree, 60*60*3
        private const string inputFileTemplate = @"{0}\grd{0}_13\w001001.adf";
        private const string sourceUrlTemplate = @"https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/13/ArcGrid/{0}.zip";
        private const string sourceZipFileTemplate = "{0}.zip";
        private const string sourceZipDestTemplate = "{0}";
        private static Dictionary<string, float[][]> cache = new Dictionary<string, float[][]>();
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        public static IEnumerable<Tuple<double, double, float[][]>> GetRawHeightsInMeters(double latA, double lonA, double latB, double lonB)
        {
            int latMin = Utils.TruncateTowardsZero(Math.Min(latA, latB) - 0.0001);
            int latMax = Utils.TruncateTowardsZero(Math.Max(latA, latB) + 0.0001);
            int lonMin = Utils.TruncateTowardsZero(Math.Min(lonA, lonB) - 0.0001);
            int lonMax = Utils.TruncateTowardsZero(Math.Max(lonA, lonB) + 0.0001);

            for (int latInt = latMin; latInt <= latMax; latInt++)
            {
                for (int lonInt = lonMin; lonInt <= lonMax; lonInt++)
                {
                    double lat = Utils.AddAwayFromZero(latInt, 0.01);
                    double lon = Utils.AddAwayFromZero(lonInt, 0.01);
                    var chunk = GetRawHeightsInMeters(lat, lon);
                    if (chunk != null)
                    {
                        yield return new Tuple<double, double, float[][]>(lat, lon, chunk);
                    }
                }
            }
        }

        private static float[][] GetRawHeightsInMeters(double lat, double lon)
        {
            int latRoot = (int)lat;
            int lonRoot = (int)lon - 1;

            string fileName =
                (lat > 0 ? 'n' : 's') + ((int)Math.Abs(lat) + 1).ToString() +
                (lon > 0 ? 'e' : 'w') + ((int)Math.Abs(lon) + 1).ToString();

            bool missing = false;
            if (!cache.ContainsKey(fileName))
            {
                Console.WriteLine("Cached " + description + " raw data does not exist: " + fileName);
                Console.WriteLine("Loading to cache...");

                string inputFile = Path.Combine(rootMapFolder, string.Format(inputFileTemplate, fileName));
                if (!File.Exists(inputFile))
                {
                    Console.WriteLine("Missing " + description + " data file: " + inputFile);
                    // Need to get fresh data:

                    var target = Path.Combine(rootMapFolder, string.Format(sourceZipFileTemplate, fileName));
                    if (!File.Exists(target))
                    {
                        Console.WriteLine("Attemping to download " + description + " source zip to '" + target + "'...");
                        using (WebClient client = new WebClient())
                        {
                            var url = new Uri(string.Format(sourceUrlTemplate, fileName));
                            try
                            {
                                client.DownloadFile(url, target);
                            }
                            catch (WebException ex)
                            {
                                missing = true;
                                if (((HttpWebResponse)ex.Response).StatusCode != HttpStatusCode.NotFound)
                                {
                                    throw;
                                }
                            }
                        }

                        if (!missing)
                        {
                            Console.WriteLine("Downloaded " + description + " source zip to '" + target + "'");
                        }
                        else
                        {
                            Console.WriteLine("Source is missing.");
                        }
                    }

                    if (!missing)
                    {
                        Console.WriteLine("Extracting raw " + description + " data from zip file '" + target + "'...");
                        ZipFile.ExtractToDirectory(target, Path.Combine(rootMapFolder, string.Format(sourceZipDestTemplate, fileName)));
                        Console.WriteLine("Extracted raw " + description + " data from zip file.");
                        File.Delete(target);
                    }
                }

                if (!missing)
                {
                    cache[fileName] = ReadDataToChunks(inputFile);
                    Console.WriteLine("Loaded raw " + description + " data to cache: " + fileName);
                }
                else
                {
                    cache[fileName] = null;
                    Console.WriteLine("Data not available to cache.");
                }
            }

            return cache[fileName];
        }

        private static float[][] ReadDataToChunks(string adfFile)
        {
            int elements = trueElements + boundaryElements * 2;
            var bytes = File.ReadAllBytes(adfFile);

            byte[] buff = new byte[4];

            int frameIndex = 0;
            int indexWithinFrame = 0;
            float[] currentFrame2 = null;

            int index = 16 * 6 + 4;
            int terminator1 = bytes[index];
            int terminator2 = bytes[index + 1];

            int numberOfBatches = bytes[index] / 2;

            List<float[]> runningList = new List<float[]>();
            while (index < bytes.Length)
            {
                if (bytes[index] == terminator1 && bytes[index + 1] == terminator2)
                {
                    index += 2;
                    frameIndex++;

                    indexWithinFrame = 0;
                    currentFrame2 = new float[256];
                }

                // Need to map explicitly because in the opposite end-ness in file.
                buff[0] = bytes[index + 3];
                buff[1] = bytes[index + 2];
                buff[2] = bytes[index + 1];
                buff[3] = bytes[index + 0];
                index += 4;

                currentFrame2[indexWithinFrame] = BitConverter.ToSingle(buff, 0);

                indexWithinFrame++;
                if (indexWithinFrame % 256 == 0)
                {
                    runningList.Add(currentFrame2);
                    indexWithinFrame = 0;
                }
            }

            if (indexWithinFrame != 0)
            {
                throw new InvalidOperationException("Incomplete frame read");
            }

            float[][] rows = new float[numberOfBatches][];
            for (int i = 0; i < numberOfBatches; i++)
            {
                rows[i] = new float[elements];
            }

            int widthIndex = 0;

            float[][] batchData = new float[numberOfBatches][];
            int batch = 0;

            float[][] data = new float[elements][];
            int dataIndex = 0;

            foreach (var part in runningList)
            {
                batchData[batch] = part;
                batch++;

                if (batch == numberOfBatches)
                {
                    batch = 0;
                    for (int k = 0; k < 256; k++)
                    {
                        if (widthIndex < elements)
                        {
                            for (int i = 0; i < numberOfBatches; i++)
                            {
                                rows[i][widthIndex] = (batchData[i][k]);
                            }

                            widthIndex++;
                        }
                    }

                    if (widthIndex == elements)
                    {
                        widthIndex = 0;
                        for (int i = 0; i < numberOfBatches; i++)
                        {
                            if (dataIndex < data.Length)
                            {
                                data[dataIndex++] = rows[i];
                            }
                            else
                            {

                            }
                            rows[i] = new float[elements];
                        }
                    }
                }
            }

            return data;
        }
    }
}
