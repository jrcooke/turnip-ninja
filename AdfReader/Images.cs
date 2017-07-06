using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using SkiaSharp;
using Newtonsoft.Json;
using System.Threading;
using System.Net;

namespace AdfReader
{
    public static class Images
    {
        private const int smallBatch = 540; // Number of 1/3 arc seconds per 3 minutes.
        private const string cachedFileTemplate = "colorCache{0}.data";
        private const string description = "Colors";
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        private static CachingHelper<SKColor> ch = new CachingHelper<SKColor>(
            smallBatch,
            Path.Combine(rootMapFolder, cachedFileTemplate),
            description,
            WriteElement,
            ReadElement,
            GenerateData);

        public static SKColor GetColor(double lat, double lon, double cosLat, double metersPerElement)
        {
            return ch.GetValue(lat, lon, cosLat, metersPerElement);
        }

        public static SKColor[][] GetChunk(double lat, double lon, int zoomLevel)
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

        private static SKColor[][] GenerateData(
            int zoomLevel,
            int size,
            int latTotMin,
            int lonTotMin,
            int latTotMin2,
            int lonTotMin2,
            int minLatTotMin,
            int minLonTotMin)
        {
            var ret2 = new List<SKColor>[smallBatch + 1][];
            for (int i = 0; i <= smallBatch; i++)
            {
                ret2[i] = new List<SKColor>[smallBatch + 1];
            }

            var chunks = ImageWorker.GetColors(latTotMin / 60.0, lonTotMin / 60.0, latTotMin2 / 60.0, lonTotMin2 / 60.0, zoomLevel + 2);
            foreach (var chunk in chunks)
            {
                LoadRawChunksIntoProcessedChunk(size, minLatTotMin, minLonTotMin, ret2, chunk);
            }

            SKColor[][] ret;
            ret = new SKColor[smallBatch + 1][];
            for (int i = 0; i <= smallBatch; i++)
            {
                ret[i] = new SKColor[smallBatch + 1];
                for (int j = 0; j <= smallBatch; j++)
                {
                    if (ret2[i][j] != null)
                    {
                        byte r = (byte)ret2[i][j].Where(p => p.Alpha == 255).Average(p => p.Red);
                        byte g = (byte)ret2[i][j].Where(p => p.Alpha == 255).Average(p => p.Green);
                        byte b = (byte)ret2[i][j].Where(p => p.Alpha == 255).Average(p => p.Blue);
                        ret[i][j] = new SKColor(r, g, b);
                    }
                }
            }

            return ret;
        }

        private static void LoadRawChunksIntoProcessedChunk(
            int size,
            int minLatTotMin,
            int minLonTotMin,
            List<SKColor>[][] ret,
            Tuple<double, double, SKColor>[] chunk)
        {
            foreach (var element in chunk)
            {
                // The chunk has smallBatch+1 elements, so each element is
                // TargetElementCoord = angle * smallBatch / Size

                int targetLat = (int)Math.Round(((element.Item1 * 60 - minLatTotMin) * smallBatch / size));
                int targetLon = (int)Math.Round(((element.Item2 * 60 - minLonTotMin) * smallBatch / size));
                if (targetLat >= 0 && targetLat <= smallBatch && targetLon >= 0 && targetLon <= smallBatch)
                {
                    if (ret[targetLat][targetLon] == null)
                    {
                        ret[targetLat][targetLon] = new List<SKColor>();
                    }

                    ret[targetLat][targetLon].Add(element.Item3);
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

        public static IEnumerable<Tuple<double, double, SKColor>[]> GetColors(double latA, double lonA, double latB, double lonB, int zoomLevel)
        {
            // Need to figure out which chunks to load.
            double invDeltaDegAtZoom = 15 * Math.Pow(2, zoomLevel - 12);

            int latMin = Utils.TruncateTowardsZero(Math.Min(latA, latB) * invDeltaDegAtZoom - 0.0001);
            int latMax = Utils.TruncateTowardsZero(Math.Max(latA, latB) * invDeltaDegAtZoom + 0.0001);
            int lonMin = Utils.TruncateTowardsZero(Math.Min(lonA, lonB) * invDeltaDegAtZoom - 0.0001) - 1;
            int lonMax = Utils.TruncateTowardsZero(Math.Max(lonA, lonB) * invDeltaDegAtZoom + 0.0001) - 1;

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

        private static Tuple<double, double, SKColor>[] GetColorsFromCache(int latDelta, double lonDelta, int zoomLevel)
        {
            double invDeltaDegAtZoom = 15 * Math.Pow(2, zoomLevel - 12);

            double lat = latDelta * 1.0 / invDeltaDegAtZoom;
            double lon = lonDelta * 1.0 / invDeltaDegAtZoom;

            string inputFile = Path.Combine(rootMapFolder, string.Format(imageCacheTemplate, lat, lon, zoomLevel));
            string metadFile = Path.Combine(rootMapFolder, string.Format(metadCacheTemplate, lat, lon, zoomLevel));

            if (!File.Exists(inputFile))
            {
                using (HttpClient client = new HttpClient())
                {
                    var url = new Uri(string.Format(imageUrlTemplate, lat, lon, zoomLevel, bingMapsKey));
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

                    if (message != null && message.StatusCode == HttpStatusCode.OK)
                    {
                        var content = message.Content.ReadAsByteArrayAsync().Result;
                        File.WriteAllBytes(inputFile, content);
                    }
                    else
                    {
                     //   throw new InvalidOperationException("Bad response: " + message.StatusCode.ToString());
                    }
                }
            }

            if (!File.Exists(inputFile))
            {
                return null;
            }

            if (!File.Exists(metadFile))
            {
                using (HttpClient client = new HttpClient())
                {
                    var metadataUrl = new Uri(string.Format(metadUrlTemplate, lat, lon, zoomLevel, bingMapsKey));
                    string rawMetadata = client.GetStringAsync(metadataUrl).Result;
                    var deserializedMetadata = JsonConvert.DeserializeObject<Metadata>(rawMetadata);
                    var metadataResource = deserializedMetadata.resourceSets[0].resources[0];
                    var processedMetadata = new CachedResource(inputFile, metadataResource.MinLat, metadataResource.MinLon, metadataResource.MaxLat, metadataResource.MaxLon);
                    var serializedProcessedMetaedata = JsonConvert.SerializeObject(processedMetadata);
                    File.WriteAllText(metadFile, serializedProcessedMetaedata);
                }
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
