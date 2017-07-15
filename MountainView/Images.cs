using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using SkiaSharp;
using Newtonsoft.Json;
using System.Net;
using System.Threading.Tasks;

namespace MountainView
{
    public static class Images
    {
        private const int smallBatch = 540; // Number of 1/3 arc seconds per 3 minutes.
        private const string cachedFileTemplate = "{0}.cdata";
        private const string description = "Colors";
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        private static CachingHelper<SKColor> ch = new CachingHelper<SKColor>(
            smallBatch,
            Path.Combine(rootMapFolder, cachedFileTemplate),
            description,
            WriteElement,
            ReadElement,
            GenerateData);

        public static SKColor GetColor(Angle lat, Angle lon, double cosLat, double metersPerElement)
        {
            return ch.GetValue(lat, lon, cosLat, metersPerElement);
        }

        public static ChunkHolder<SKColor> GetChunk(Angle lat, Angle lon, int zoomLevel)
        {
            return ch.GetValuesFromCache(lat, lon, zoomLevel);
        }

        private static void WriteElement(SKColor item, FileStream stream)
        {
            stream.WriteByte(item.Alpha);
            stream.WriteByte(item.Red);
            stream.WriteByte(item.Green);
            stream.WriteByte(item.Blue);
        }

        private static SKColor ReadElement(FileStream stream, byte[] buffer)
        {
            var alpha = stream.ReadByte();
            return new SKColor(
                (byte)stream.ReadByte(),
                (byte)stream.ReadByte(),
                (byte)stream.ReadByte(),
                (byte)alpha);
        }

        private static ChunkHolder<SKColor> GenerateData(
            int zoomLevel,
            int size,
            Angle lat1,
            Angle lon1,
            Angle lat2,
            Angle lon2,
            Angle minLat,
            Angle minLon)
        {
            var ret2 = new List<SKColor>[smallBatch + 1][];
            for (int i = 0; i <= smallBatch; i++)
            {
                ret2[i] = new List<SKColor>[smallBatch + 1];
            }

            var chunks = ImageWorker.GetColors(lat1, lon1, lat2, lon2, zoomLevel + 2);
            foreach (var chunk in chunks)
            {
                LoadRawChunksIntoProcessedChunk(size, minLat, minLon, ret2, chunk);
            }

            ChunkHolder<SKColor> ret = new ChunkHolder<SKColor>(smallBatch + 1, smallBatch + 1, lat1, lon1, lat2, lon2);
            for (int i = 0; i <= smallBatch; i++)
            {
                for (int j = 0; j <= smallBatch; j++)
                {
                    if (ret2[i][j] != null)
                    {
                        byte r = (byte)ret2[i][j].Where(p => p.Alpha == 255).Average(p => p.Red);
                        byte g = (byte)ret2[i][j].Where(p => p.Alpha == 255).Average(p => p.Green);
                        byte b = (byte)ret2[i][j].Where(p => p.Alpha == 255).Average(p => p.Blue);
                        ret.Data[i][j] = new SKColor(r, g, b);
                    }
                }
            }

            return ret;
        }

        private static void LoadRawChunksIntoProcessedChunk(
            int size,
            Angle minLat,
            Angle minLon,
            List<SKColor>[][] ret,
            Tuple<double, double, SKColor>[] chunk)
        {
            double minLatDecimal = minLat.DecimalDegree;
            double minLonDecimal = minLon.DecimalDegree;
            int minLatRoot = Math.Sign(chunk.Average(p => p.Item1)) * Math.Min(
                Utils.TruncateTowardsZero(Math.Abs(chunk.Average(p => p.Item1))),
                Utils.AddAwayFromZero(Utils.TruncateTowardsZero(Math.Abs(chunk.Average(p => p.Item1))), 1));
            int minLonRoot = Math.Sign(chunk.Average(p => p.Item2)) * Math.Min(
                Utils.TruncateTowardsZero(Math.Abs(chunk.Average(p => p.Item2))),
                Utils.AddAwayFromZero(Utils.TruncateTowardsZero(Math.Abs(chunk.Average(p => p.Item2))), 1));

            var lat2Min = (float)(minLatRoot + 0 * 1.0 / RawChunks.trueElements);
            var lat2Max = (float)(minLatRoot + RawChunks.trueElements * 1.0 / RawChunks.trueElements);
            int targetDeltaLatMin = (int)Math.Round((lat2Min - minLatDecimal) * 60 * 60 * smallBatch / size);
            int targetDeltaLatMax = (int)Math.Round((lat2Max - minLatDecimal) * 60 * 60 * smallBatch / size);

            var lon2Min = (float)(minLonRoot + 0 * 1.0 / RawChunks.trueElements);
            var lon2Max = (float)(minLonRoot + RawChunks.trueElements * 1.0 / RawChunks.trueElements);
            int targetDeltaLonMin = (int)Math.Round((lon2Min - minLonDecimal) * 60 * 60 * smallBatch / size);
            int targetDeltaLonMax = (int)Math.Round((lon2Max - minLonDecimal) * 60 * 60 * smallBatch / size);

            foreach (var element in chunk)
            {
                // The chunk has smallBatch+1 elements, so each element is
                // TargetElementCoord = angle * smallBatch / Size

                var lat2 = element.Item1;
                int targetDeltaLat = (int)Math.Round((lat2 - minLatDecimal) * 60 * 60 * smallBatch / size);
                if (targetDeltaLat >= 0 && targetDeltaLat <= smallBatch)
                {
                    var lon2 = element.Item2;
                    int targetDeltaLon = (int)Math.Round((lon2 - minLonDecimal) * 60 * 60 * smallBatch / size);
                    if (targetDeltaLon >= 0 && targetDeltaLon <= smallBatch)
                    {
                        if (ret[targetDeltaLat][targetDeltaLon] == null)
                        {
                            ret[targetDeltaLat][targetDeltaLon] = new List<SKColor>();
                        }
                        ret[targetDeltaLat][targetDeltaLon].Add(element.Item3);
                    }
                }
            }
        }
    }

    internal class ImageWorker
    {
        private const string imageUrlTemplate = "https://dev.virtualearth.net/REST/v1/Imagery/Map/Aerial/{0},{1}/{2}?format=png&key={3}";
        private const string metadUrlTemplate = "https://dev.virtualearth.net/REST/v1/Imagery/Map/Aerial/{0},{1}/{2}?mapMetadata=1&key={3}";
        private const string imageCacheTemplate = "image{0}_{1}_{2}.png";
        private const string metadCacheTemplate = "image{0}_{1}_{2}.meta";
        private const int footerHeight = 25;
        private const double baseScale = 156543.04;
        private static string bingMapsKey = ConfigurationManager.AppSettings["BingMapsKey"];
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        public static IEnumerable<Tuple<double, double, SKColor>[]> GetColors(Angle latA, Angle lonA, Angle latB, Angle lonB, int zoomLevel)
        {
            // Need to figure out which chunks to load.
            double invDeltaDegAtZoom = 15 * Math.Pow(2, zoomLevel - 12);

            int latMin = Utils.TruncateTowardsZero(Math.Min(latA.DecimalDegree, latB.DecimalDegree) * invDeltaDegAtZoom - 0.0001);
            int latMax = Utils.TruncateTowardsZero(Math.Max(latA.DecimalDegree, latB.DecimalDegree) * invDeltaDegAtZoom + 0.0001);
            int lonMin = Utils.TruncateTowardsZero(Math.Min(lonA.DecimalDegree, lonB.DecimalDegree) * invDeltaDegAtZoom - 0.0001) - 1;
            int lonMax = Utils.TruncateTowardsZero(Math.Max(lonA.DecimalDegree, lonB.DecimalDegree) * invDeltaDegAtZoom + 0.0001) - 1;

            Dictionary<string, SourceColorInfo> missingChunks = new Dictionary<string, SourceColorInfo>();
            for (int latInt = latMin; latInt <= latMax; latInt++)
            {
                for (int lonInt = lonMin; lonInt <= lonMax; lonInt++)
                {
                    var chunk = MissingColorFilesFromWeb(latInt, lonInt, zoomLevel);
                    if (chunk != null && !missingChunks.ContainsKey(chunk.metadFile))
                    {
                        missingChunks.Add(chunk.metadFile, chunk);
                    }
                }
            }

            // This pattern should be fine because the number of tasks is small.
            var tasks = missingChunks.Select(p => LoadMissingColorsFromWeb(p.Value)).ToArray();
            Task.WaitAll(tasks);

            for (int latInt = latMin; latInt <= latMax; latInt++)
            {
                for (int lonInt = lonMin; lonInt <= lonMax; lonInt++)
                {
                    var chunk = GetColorsFromCache(latInt, lonInt, zoomLevel);
                    if (chunk != null)
                    {
                        yield return chunk;
                    }
                }
            }
        }

        private static SourceColorInfo MissingColorFilesFromWeb(int latDelta, double lonDelta, int zoomLevel)
        {
            double invDeltaDegAtZoom = 15 * Math.Pow(2, zoomLevel - 12);
            double lat = latDelta * 1.0 / invDeltaDegAtZoom;
            double lon = lonDelta * 1.0 / invDeltaDegAtZoom;

            string inputFile = Path.Combine(rootMapFolder, string.Format(imageCacheTemplate, lat, lon, zoomLevel));
            string metadFile = Path.Combine(rootMapFolder, string.Format(metadCacheTemplate, lat, lon, zoomLevel));
            if (!File.Exists(metadFile))
            {
                return new SourceColorInfo()
                {
                    inputFile = inputFile,
                    inputUrl = new Uri(string.Format(imageUrlTemplate, lat, lon, zoomLevel, bingMapsKey)),
                    metadFile = metadFile,
                    metadataUrl = new Uri(string.Format(metadUrlTemplate, lat, lon, zoomLevel, bingMapsKey)),
                };
            }

            return null;
        }

        private class SourceColorInfo
        {
            public string inputFile;
            public Uri inputUrl;
            public string metadFile;
            public Uri metadataUrl;
        }

        private static async Task LoadMissingColorsFromWeb(SourceColorInfo info)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage message = null;
                try
                {
                    message = await client.GetAsync(info.inputUrl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                if (message != null && message.StatusCode == HttpStatusCode.OK)
                {
                    var content = await message.Content.ReadAsByteArrayAsync();
                    File.WriteAllBytes(info.inputFile, content);
                }
                else
                {
                    throw new InvalidOperationException("Bad response: " + message.StatusCode.ToString());
                }
            }

            using (HttpClient client = new HttpClient())
            {
                string rawMetadata = await client.GetStringAsync(info.metadataUrl);
                var deserializedMetadata = JsonConvert.DeserializeObject<Metadata>(rawMetadata);
                var metadataResource = deserializedMetadata.resourceSets[0].resources[0];
                var processedMetadata = new CachedResource(info.inputFile, metadataResource.MinLat, metadataResource.MinLon, metadataResource.MaxLat, metadataResource.MaxLon);
                var serializedProcessedMetaedata = JsonConvert.SerializeObject(processedMetadata);
                File.WriteAllText(info.metadFile, serializedProcessedMetaedata);
            }
        }

        private static Tuple<double, double, SKColor>[] GetColorsFromCache(int latDelta, double lonDelta, int zoomLevel)
        {
            double invDeltaDegAtZoom = 15 * Math.Pow(2, zoomLevel - 12);

            double lat = latDelta * 1.0 / invDeltaDegAtZoom;
            double lon = lonDelta * 1.0 / invDeltaDegAtZoom;

            string inputFile = Path.Combine(rootMapFolder, string.Format(imageCacheTemplate, lat, lon, zoomLevel));
            string metadFile = Path.Combine(rootMapFolder, string.Format(metadCacheTemplate, lat, lon, zoomLevel));

            if (!File.Exists(metadFile))
            {
                return null;
            }

            Tuple<double, double, SKColor>[] data;
            CachedResource cr = JsonConvert.DeserializeObject<CachedResource>(File.ReadAllText(metadFile));
            using (SKBitmap bm = SKBitmap.Decode(cr.FileName))
            {
                data = new Tuple<double, double, SKColor>[bm.Width * (bm.Height - footerHeight)];
                for (int i = 0; i < bm.Width; i++)
                {
                    for (int j = 0; j < bm.Height - footerHeight; j++)
                    {
                        double loopLat = ((bm.Width - 1 - j) * cr.MaxLat + j * cr.MinLat) / (bm.Width - 1);
                        double loopLon = ((bm.Height - 1 - i) * cr.MinLon + i * cr.MaxLon) / (bm.Height - 1);
                        SKColor c = bm.GetPixel(i, j);
                        data[i * (bm.Height - footerHeight) + j] = new Tuple<double, double, SKColor>(loopLat, loopLon, c);
                    }
                }
            }

            return data;
        }

        private class CachedResource
        {
            public CachedResource(string fileName, double minLat, double minLon, double maxLat, double maxLon)
            {
                FileName = fileName;
                MinLat = minLat;
                MinLon = minLon;
                MaxLat = maxLat;
                MaxLon = maxLon;
            }

            public string FileName { get; set; }
            public double MinLat { get; private set; }
            public double MaxLat { get; private set; }
            public double MinLon { get; private set; }
            public double MaxLon { get; private set; }
        }

        private struct Resource
        {
            public double[] bbox;
            public double MinLat { get { return bbox[0]; } }
            public double MaxLat { get { return bbox[2]; } }
            public double MinLon { get { return bbox[1]; } }
            public double MaxLon { get { return bbox[3]; } }
        }

        private struct ResourceSet
        {
            public Resource[] resources;
        }

        private struct Metadata
        {
            public ResourceSet[] resourceSets;
        }
    }
}
