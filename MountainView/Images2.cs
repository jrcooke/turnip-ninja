﻿using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
        private static string bingMapsKey = ConfigurationManager.AppSettings["BingMapsKey"];
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        public static async Task<ChunkHolder<SKColor>> GenerateData(Angle lat, Angle lon, int zoomLevel)
        {
            ChunkMetadata template = ChunkMetadata.GetStandardRangeContaingPoint(lat, lon, zoomLevel);
            ChunkHolder<SKColor> ret = new ChunkHolder<SKColor>(
                template.LatSteps, template.LonSteps,
                template.LatLo, template.LonLo,
                template.LatHi, template.LonHi);

            // Need to get the images that optimally fill this chunk.
            // Start by picking a chunk in the center at the same zoom level.
            Angle midLat = Angle.Add(template.LatLo, Angle.Divide(template.LatDelta, 2));
            Angle midLon = Angle.Add(template.LonLo, Angle.Divide(template.LonDelta, 2));

            var chunks = new List<ChunkHolder<SKColor>>();
            var tmp = await GetColors(midLat, midLon, zoomLevel + 2);
            chunks.Add(tmp);

            // Compare the chunk we got with the area we need to fill, to determine how many more are needed.
            Angle subLatDelta = Angle.Divide(tmp.LatDelta, 1.2);
            Angle subLonDelta = Angle.Divide(tmp.LonDelta, 1.2);
            int latRange = Angle.Divide(ret.LatDelta, subLatDelta) + 1;
            int lonRange = Angle.Divide(ret.LonDelta, subLonDelta) + 1;

            List<Task<ChunkHolder<SKColor>>> workers = new List<Task<ChunkHolder<SKColor>>>();
            for (int i = -latRange; i <= latRange; i++)
            {
                for (int j = -lonRange; j <= lonRange; j++)
                {
                    if (i == 0 && j == 0)
                    {
                        continue;
                    }

                    workers.Add(GetColors(
                        Angle.Add(midLat, Angle.Multiply(subLatDelta, i)),
                        Angle.Add(midLon, Angle.Multiply(subLonDelta, j)), zoomLevel + 2));
                }
            }

            Task.WaitAll(workers.ToArray());
            foreach(var worker in workers)
            {
                chunks.Add(worker.Result);
            }

            ChunkHolder<SKColor>.RenderChunksInto(chunks, ret);
            return ret;
        }

        public static async Task<ChunkHolder<SKColor>> GetColors(Angle lat, Angle lon, int zoomLevel)
        {
            // Need to figure out which chunks to load.
            string inputFile = Path.Combine(rootMapFolder, string.Format(imageCacheTemplate, lat.ToLatString(), lon.ToLonString(), zoomLevel));
            string metadFile = Path.Combine(rootMapFolder, string.Format(metadCacheTemplate, lat.ToLatString(), lon.ToLonString(), zoomLevel));
            if (!File.Exists(metadFile))
            {
                using (HttpClient client = new HttpClient())
                {
                    Uri inputUrl = new Uri(string.Format(imageUrlTemplate, lat.DecimalDegree, lon.DecimalDegree, zoomLevel, bingMapsKey));
                    Uri metadUrl = new Uri(string.Format(metadUrlTemplate, lat.DecimalDegree, lon.DecimalDegree, zoomLevel, bingMapsKey));
                    HttpResponseMessage message = await client.GetAsync(inputUrl);
                    var content = await message.Content.ReadAsByteArrayAsync();
                    File.WriteAllBytes(inputFile, content);

                    string rawMetadata = await client.GetStringAsync(metadUrl);
                    File.WriteAllText(metadFile, rawMetadata);
                }
            }

            JObject jObject = JObject.Parse(File.ReadAllText(metadFile));
            double[] bbox = jObject["resourceSets"].First["resources"].First["bbox"].ToObject<double[]>();
            Angle latLo = Angle.FromDecimalDegrees(bbox[0]);
            Angle lonLo = Angle.FromDecimalDegrees(bbox[1]);
            Angle latHi = Angle.FromDecimalDegrees(bbox[2]);
            Angle lonHi = Angle.FromDecimalDegrees(bbox[3]);
            using (SKBitmap bm = SKBitmap.Decode(inputFile))
            {
                Angle footerSize = Angle.Divide(Angle.Multiply(Angle.Subtract(latHi, latLo), footerHeight), bm.Height);
                Angle adjustedlatLo = Angle.Add(latLo, footerSize);
                return new ChunkHolder<SKColor>(bm.Height - footerHeight, bm.Width,
                    adjustedlatLo, lonLo,
                    latHi, lonHi,
                    (i, j) => bm.GetPixel(bm.Width - 1 - j, bm.Height - footerHeight - 1 - i));
            }
        }
    }
}
