using MountainView.Base;
using MountainView.ChunkManagement;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MountainView.Imaging
{
    internal class Images : CachingHelper<SKColor>
    {
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        private const string fileExt = "idata";
        private const string description = "Images";

        private Images() : base(fileExt, description, 4)
        {
        }

        private static Lazy<Images> current = new Lazy<Images>(() => new Images());
        public static Images Current
        {
            get
            {
                return current.Value;
            }
        }

        protected override async Task<ChunkHolder<SKColor>> GenerateData(StandardChunkMetadata template)
        {
            ChunkHolder<SKColor> ret = new ChunkHolder<SKColor>(
                template.LatSteps, template.LonSteps,
                template.LatLo, template.LonLo,
                template.LatHi, template.LonHi,
                null,
                null,
                null);

            // Need to get the images that optimally fill this chunk.
            // Start by picking a chunk in the center at the same zoom level.
            Angle midLat = Angle.Add(template.LatLo, Angle.Divide(template.LatDelta, 2));
            Angle midLon = Angle.Add(template.LonLo, Angle.Divide(template.LonDelta, 2));

            var chunks = new List<ChunkHolder<SKColor>>();
            var tmp = await GetColors(midLat, midLon);
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
                        Angle.Add(midLon, Angle.Multiply(subLonDelta, j))));
                }
            }

            await Utils.ForEachAsync(workers, 10, (worker) => worker);
            foreach (var worker in workers)
            {
                chunks.Add(worker.Result);
            }

            ret.RenderChunksInto(chunks, Utils.WeightedColorAverage);
            return ret;
        }

        private static async Task<ChunkHolder<SKColor>> GetColors(Angle lat, Angle lon)
        {
            Angle partitionSize = Angle.Divide(Angle.FromDecimalDegrees(1), 16);
            var latPart = Angle.FloorDivide(lat, partitionSize) % 16;
            var lonPart = Angle.FloorDivide(Angle.Multiply(lon, -1), partitionSize) % 16;

            //var tmp = new
            //{
            //    lat = int.Parse(shortName[1].Substring(0, 2)),
            //    lon = int.Parse(shortName[1].Substring(2, 3)),
            //    quadLoc = int.Parse(shortName[1].Substring(5, 2)),
            //    quadInd = shortName[2].ToUpperInvariant(),
            //    acqTime = DateTime.Parse(shortName[5].Substring(0, 4) + "-" + shortName[5].Substring(4, 2) + "-" + shortName[5].Substring(6, 2))
            //};

            throw new NotImplementedException();

            //var deltaLat = Angle.FromDecimalDegrees(tmp.lat + (15 - (2 * ((tmp.quadLoc - 1) / 8) + (tmp.quadInd[1] == 'W' ? 0 : 1))) / 16.0);
            //var deltaLon = Angle.FromDecimalDegrees(-1 * (tmp.lon + (15 - (2 * ((tmp.quadLoc - 1) % 8) + (tmp.quadInd[0] == 'S' ? 0 : 1))) / 16.0));

            //Angle a = Angle.FromDecimalDegrees(1.0 / 16.0);



            //if (!File.Exists(metadFile))
            //{
            //    using (HttpClient client = new HttpClient())
            //    {
            //        client.Timeout = TimeSpan.FromMinutes(5);
            //        Uri inputUrl = new Uri(string.Format(imageUrlTemplate, lat.DecimalDegree, lon.DecimalDegree, zoomLevel, bingMapsKey));
            //        Uri metadUrl = new Uri(string.Format(metadUrlTemplate, lat.DecimalDegree, lon.DecimalDegree, zoomLevel, bingMapsKey));
            //        HttpResponseMessage message = null;
            //        try
            //        {
            //            message = await client.GetAsync(inputUrl);
            //        }
            //        catch
            //        {
            //            message = await client.GetAsync(inputUrl);
            //        }

            //        var content = await message.Content.ReadAsByteArrayAsync();
            //        File.WriteAllBytes(inputFile, content);

            //        string rawMetadata = await client.GetStringAsync(metadUrl);
            //        File.WriteAllText(metadFile, rawMetadata);
            //    }
            //}
            //else
            //{
            //    // In case this was from another thread;
            //    await Task.Delay(TimeSpan.FromSeconds(1));
            //}

            //JObject jObject = null;
            //try
            //{
            //    jObject = JObject.Parse(File.ReadAllText(metadFile));
            //}
            //catch (Newtonsoft.Json.JsonReaderException)
            //{
            //    File.Delete(metadFile);
            //    throw;
            //}

            //double[] bbox = jObject["resourceSets"].First["resources"].First["bbox"].ToObject<double[]>();
            //Angle latLo = Angle.FromDecimalDegrees(bbox[0]);
            //Angle lonLo = Angle.FromDecimalDegrees(bbox[1]);
            //Angle latHi = Angle.FromDecimalDegrees(bbox[2]);
            //Angle lonHi = Angle.FromDecimalDegrees(bbox[3]);
            //using (SKBitmap bm = SKBitmap.Decode(inputFile))
            //{
            //    Angle adjustedlatLo = latLo;
            //    return new ChunkHolder<SKColor>(bm.Height, bm.Width,
            //        adjustedlatLo, lonLo,
            //        latHi, lonHi,
            //        (i, j) => bm.GetPixel(bm.Width - 1 - j, bm.Height - 1 - i),
            //        null,
            //        null);
            //}
        }

        protected override void WritePixel(FileStream stream, SKColor pixel)
        {
            stream.WriteByte(pixel.Red);
            stream.WriteByte(pixel.Green);
            stream.WriteByte(pixel.Blue);
        }

        protected override SKColor ReadPixel(FileStream stream, byte[] buffer)
        {
            return new SKColor(
                (byte)stream.ReadByte(),
                (byte)stream.ReadByte(),
                (byte)stream.ReadByte(),
                255);
        }
    }
}
