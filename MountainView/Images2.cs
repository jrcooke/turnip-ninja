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
    internal class ImageWorker2
    {
        private const string imageUrlTemplate = "https://dev.virtualearth.net/REST/v1/Imagery/Map/Aerial/{0},{1}/{2}?format=png&key={3}";
        private const string metadUrlTemplate = "https://dev.virtualearth.net/REST/v1/Imagery/Map/Aerial/{0},{1}/{2}?mapMetadata=1&key={3}";
        private const string imageCacheTemplate = "image{0}_{1}_{2}.png";
        private const string metadCacheTemplate = "image{0}_{1}_{2}.meta";
        private const int footerHeight = 25;
        private const double baseScale = 156543.04;
        private static string bingMapsKey = ConfigurationManager.AppSettings["BingMapsKey"];
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        //public static ChunkHolder<SKColor> GetColors(Angle latA, Angle lonA, Angle latB, Angle lonB, int zoomLevel)
        //{
        //    // Need to figure out which chunks to load.
        //    double invDeltaDegAtZoom = 15 * Math.Pow(2, zoomLevel - 12);

        //    int latMin = Utils.TruncateTowardsZero(Math.Min(latA.DecimalDegree, latB.DecimalDegree) * invDeltaDegAtZoom - 0.0001);
        //    int latMax = Utils.TruncateTowardsZero(Math.Max(latA.DecimalDegree, latB.DecimalDegree) * invDeltaDegAtZoom + 0.0001);
        //    int lonMin = Utils.TruncateTowardsZero(Math.Min(lonA.DecimalDegree, lonB.DecimalDegree) * invDeltaDegAtZoom - 0.0001) - 1;
        //    int lonMax = Utils.TruncateTowardsZero(Math.Max(lonA.DecimalDegree, lonB.DecimalDegree) * invDeltaDegAtZoom + 0.0001) - 1;

        //    Dictionary<string, SourceColorInfo> missingChunks = new Dictionary<string, SourceColorInfo>();
        //    for (int latInt = latMin; latInt <= latMax; latInt++)
        //    {
        //        for (int lonInt = lonMin; lonInt <= lonMax; lonInt++)
        //        {
        //            var chunk = MissingColorFilesFromWeb(latInt, lonInt, zoomLevel);
        //            if (chunk != null && !missingChunks.ContainsKey(chunk.metadFile))
        //            {
        //                missingChunks.Add(chunk.metadFile, chunk);
        //            }
        //        }
        //    }

        //    // This pattern should be fine because the number of tasks is small.
        //    var tasks = missingChunks.Select(p => LoadMissingColorsFromWeb(p.Value)).ToArray();
        //    Task.WaitAll(tasks);

        //    for (int latInt = latMin; latInt <= latMax; latInt++)
        //    {
        //        for (int lonInt = lonMin; lonInt <= lonMax; lonInt++)
        //        {
        //            var chunk = GetColorsFromCache(latInt, lonInt, zoomLevel);
        //            if (chunk != null)
        //            {
        //                yield return chunk;
        //            }
        //        }
        //    }
        //}

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
                using (FileStream stream = File.OpenWrite(info.metadFile))
                {
                    stream.Write(BitConverter.GetBytes(metadataResource.MinLat), 0, 8);
                    stream.Write(BitConverter.GetBytes(metadataResource.MinLon), 0, 8);
                    stream.Write(BitConverter.GetBytes(metadataResource.MaxLat), 0, 8);
                    stream.Write(BitConverter.GetBytes(metadataResource.MaxLon), 0, 8);
                }
            }
        }

        private static ChunkHolder<SKColor> GetColorsFromCache(Angle lat, Angle lon, int zoomLevel)
        {
            string inputFile = Path.Combine(rootMapFolder, string.Format(imageCacheTemplate, lat.DecimalDegree, lon.DecimalDegree, zoomLevel));
            string metadFile = Path.Combine(rootMapFolder, string.Format(metadCacheTemplate, lat.DecimalDegree, lon.DecimalDegree, zoomLevel));

            if (!File.Exists(metadFile))
            {
                return null;
            }

            double latLo, lonLo, latHi, lonHi;
            byte[] buffer = new byte[8];
            using (FileStream fs = File.OpenRead(metadFile))
            {
                latLo = ReadDouble(fs, buffer);
                lonLo = ReadDouble(fs, buffer);
                latHi = ReadDouble(fs, buffer);
                lonHi = ReadDouble(fs, buffer);
            }

            ChunkHolder<SKColor> ret = null;
            using (SKBitmap bm = SKBitmap.Decode(inputFile))
            {
                ret = new ChunkHolder<SKColor>(bm.Width, bm.Height - footerHeight,
                    Angle.FromDecimalDegrees(latLo), Angle.FromDecimalDegrees(lonLo),
                    Angle.FromDecimalDegrees(latHi), Angle.FromDecimalDegrees(lonHi));

                for (int i = 0; i < ret.Width; i++)
                {
                    for (int j = 0; j < ret.Height; j++)
                    {
                        SKColor c = bm.GetPixel(i, j);
                        ret.Data[i][j] = c;
                    }
                }
            }

            return ret;
        }

        private static double ReadDouble(FileStream fs, byte[] bufferLen8)
        {
            fs.Read(bufferLen8, 0, 8);
            return BitConverter.ToDouble(bufferLen8, 0);
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
