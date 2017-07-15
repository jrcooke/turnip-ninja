using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;

namespace MountainView
{
    public static class Heights
    {
        private const int smallBatch = 540; // Number of 1/3 arc seconds per 3 minutes.
        private const string cachedFileTemplate = "{0}.hdata";
        private const string description = "Heights";
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        private static CachingHelper<float> ch = new CachingHelper<float>(
            smallBatch,
            Path.Combine(rootMapFolder, cachedFileTemplate),
            description,
            WriteElement,
            ReadElement,
            GenerateData);

        public static float GetHeight(Angle lat, Angle lon, double cosLat, double metersPerElement)
        {
            return ch.GetValue(lat, lon, cosLat, metersPerElement);
        }

        public static ChunkHolder<float> GetChunk(Angle lat, Angle lon, int zoomLevel)
        {
            var raw = ch.GetValuesFromCache(lat, lon, zoomLevel);
            return raw;
        }

        private static void WriteElement(float item, FileStream stream)
        {
            stream.Write(BitConverter.GetBytes(item), 0, 4);
        }

        private static float ReadElement(FileStream stream, byte[] buffer)
        {
            stream.Read(buffer, 0, 4);
            float i3 = BitConverter.ToSingle(buffer, 0);
            return i3;
        }

        private static ChunkHolder<float> GenerateData(
            int zoomLevel,
            int size,
            Angle lat1,
            Angle lon1,
            Angle lat2,
            Angle lon2,
            Angle minLat,
            Angle minLon)
        {
            var maxLat = Angle.Add(minLat, 1.0);
            var maxLon = Angle.Add(minLon, 1.0);
            ChunkHolder<float> ret = new ChunkHolder<float>(smallBatch + 1, smallBatch + 1, lat1, lon1, lat2, lon2);

            var chunks = RawChunks.GetRawHeightsInMeters(lat1, lon1, lat2, lon2);
            foreach (var chunk in chunks)
            {
                LoadRawChunksIntoProcessedChunk(size, minLat, minLon, ret, chunk);
            }

            return ret;
        }

        private static void LoadRawChunksIntoProcessedChunk(
            int size,
            Angle minLat,
            Angle minLon,
            ChunkHolder<float> ret,
            ChunkHolder<float> chunk)
        {
            double minLatDecimal = minLat.DecimalDegree;
            double minLonDecimal = minLon.DecimalDegree;
            double minLatRoot = Angle.Min(chunk.LatLo, chunk.LatHi).DecimalDegree;
            double minLonRoot = Angle.Min(chunk.LonLo, chunk.LonHi).DecimalDegree;

            var lat2Min = (float)(minLatRoot + 0 * 1.0 / RawChunks.trueElements);
            var lat2Max = (float)(minLatRoot + RawChunks.trueElements * 1.0 / RawChunks.trueElements);
            var lon2Min = (float)(minLonRoot + 0 * 1.0 / RawChunks.trueElements);
            var lon2Max = (float)(minLonRoot + RawChunks.trueElements * 1.0 / RawChunks.trueElements);

            int targetDeltaLatMin = (int)Math.Round((lat2Min - minLatDecimal) * 60 * 60 * smallBatch / size);
            int targetDeltaLatMax = (int)Math.Round((lat2Max - minLatDecimal) * 60 * 60 * smallBatch / size);

            int targetDeltaLonMin = (int)Math.Round((lon2Min - minLonDecimal) * 60 * 60 * smallBatch / size);
            int targetDeltaLonMax = (int)Math.Round((lon2Max - minLonDecimal) * 60 * 60 * smallBatch / size);

            for (int j = 0; j <= RawChunks.trueElements; j++)
            {
                // The chunk has smallBatch+1 elements, so each element is
                // TargetElementCoord = angle * smallBatch / Size

                var lat2 = (float)(minLatRoot + j * 1.0 / RawChunks.trueElements);
                int targetDeltaLat = (int)Math.Round((lat2 - minLatDecimal) * 60 * 60 * smallBatch / size);
                if (targetDeltaLat >= 0 && targetDeltaLat <= smallBatch)
                {
                    for (int i = 0; i <= RawChunks.trueElements; i++)
                    {
                        var lon2 = (float)(minLonRoot + i * 1.0 / RawChunks.trueElements);
                        int targetDeltaLon = (int)Math.Round((lon2 - minLonDecimal) * 60 * 60 * smallBatch / size);
                        if (targetDeltaLon >= 0 && targetDeltaLon <= smallBatch)
                        {
                            var val = chunk.Data[i + RawChunks.boundaryElements][RawChunks.trueElements - 1 - j + RawChunks.boundaryElements];
                            var cur = ret.Data[targetDeltaLat][targetDeltaLon];
                            if (cur < val)
                            {
                                ret.Data[targetDeltaLat][targetDeltaLon] = val;
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
        private const string sourceUrlTemplate = @"https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/13/ArcGrid/USGS_NED_13_{0}_ArcGrid.zip";
        private const string sourceUrlTemplate2 = @"https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/13/ArcGrid/{0}.zip";
        private const string sourceZipFileTemplate = "USGS_NED_13_{0}_ArcGrid.zip";
        private const string sourceZipDestTemplate = "{0}";
        private static Dictionary<string, ChunkHolder<float>> cache = new Dictionary<string, ChunkHolder<float>>();
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        public static IEnumerable<ChunkHolder<float>> GetRawHeightsInMeters(Angle latA, Angle lonA, Angle latB, Angle lonB)
        {
            int latMin = Utils.TruncateTowardsZero(Math.Min(latA.DecimalDegree, latB.DecimalDegree) - 0.0001);
            int latMax = Utils.TruncateTowardsZero(Math.Max(latA.DecimalDegree, latB.DecimalDegree) + 0.0001);
            int lonMin = Utils.TruncateTowardsZero(Math.Min(lonA.DecimalDegree, lonB.DecimalDegree) - 0.0001);
            int lonMax = Utils.TruncateTowardsZero(Math.Max(lonA.DecimalDegree, lonB.DecimalDegree) + 0.0001);

            for (int latInt = latMin; latInt <= latMax; latInt++)
            {
                for (int lonInt = lonMin; lonInt <= lonMax; lonInt++)
                {
                    double lat = Utils.AddAwayFromZero(latInt, 0.01);
                    double lon = Utils.AddAwayFromZero(lonInt, 0.01);
                    var chunk = GetRawHeightsInMeters((int)lat, (int)lon);
                    if (chunk != null)
                    {
                        yield return chunk;
                    }
                }
            }
        }

        public static ChunkHolder<float> GetRawHeightsInMeters(int lat, int lon)
        {
            string fileName =
                (lat > 0 ? 'n' : 's') + ((int)Math.Abs(lat) + 1).ToString() +
                (lon > 0 ? 'e' : 'w') + ((int)Math.Abs(lon) + 1).ToString();

            bool missing = false;
            if (!cache.ContainsKey(fileName))
            {
                Console.WriteLine("Cached " + description + " raw data does not exist: " + fileName);
                Console.WriteLine("Loading to cache...");

                var shortWebFile =
                    (lat > 0 ? 'n' : 's') + ((int)Math.Abs(lat) + 1).ToString("D2") +
                    (lon > 0 ? 'e' : 'w') + ((int)Math.Abs(lon) + 1).ToString("D3");
                string inputFile = Path.Combine(rootMapFolder, string.Format(inputFileTemplate, shortWebFile));
                if (!File.Exists(inputFile))
                {
                    Console.WriteLine("Missing " + description + " data file: " + inputFile);
                    // Need to get fresh data:

                    var target = Path.Combine(rootMapFolder, string.Format(sourceZipFileTemplate, fileName));
                    if (!File.Exists(target))
                    {
                        Console.WriteLine("Attemping to download " + description + " source zip to '" + target + "'...");
                        using (HttpClient client = new HttpClient())
                        {
                            HttpResponseMessage message = TryDownloadDifferentFormats(shortWebFile, client);
                            if (message != null && message.StatusCode == HttpStatusCode.OK)
                            {
                                var content = message.Content.ReadAsByteArrayAsync().Result;
                                File.WriteAllBytes(target, content);
                            }
                            else if (message != null && message.StatusCode == HttpStatusCode.NotFound)
                            {
                                missing = true;
                            }
                            else
                            {
                                throw new InvalidOperationException("Bad response: " + message.StatusCode.ToString());
                            }
                        }

                        if (!missing)
                        {
                            Console.WriteLine("Downloaded " + description + " source zip to '" + target + "'");
                        }
                        else
                        {
                            throw new InvalidOperationException("Source is missing. This is expected when asking for data outside of USA");
                            // Console.WriteLine("Source is missing.");
                        }
                    }

                    if (!missing)
                    {
                        Console.WriteLine("Extracting raw " + description + " data from zip file '" + target + "'...");
                        ZipFile.ExtractToDirectory(target, Path.Combine(rootMapFolder, string.Format(sourceZipDestTemplate, shortWebFile)));
                        Console.WriteLine("Extracted raw " + description + " data from zip file.");
                        //                File.Delete(target);
                    }
                }

                if (!missing)
                {
                    //cache[fileName] = ReadDataToChunks(inputFile);
                    cache[fileName] = AdfReaderWorker.GetChunk(new FileInfo(inputFile).Directory.ToString());
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

        private static HttpResponseMessage TryDownloadDifferentFormats(string shortWebFile, HttpClient client)
        {
            var ret = TryDownloadDifferentFormats(sourceUrlTemplate, shortWebFile, client);
            if (ret == null || ret.StatusCode != HttpStatusCode.OK)
            {
                ret = TryDownloadDifferentFormats(sourceUrlTemplate2, shortWebFile, client);
            }

            return ret;
        }

        private static HttpResponseMessage TryDownloadDifferentFormats(string template, string shortWebFile, HttpClient client)
        {
            var url = new Uri(string.Format(template, shortWebFile));
            HttpResponseMessage message = null;
            try
            {
                var messageTask = client.GetAsync(url);
                while (!messageTask.IsCompleted)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                    Console.Write(".");
                }

                message = messageTask.Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return message;
        }
    }
}
