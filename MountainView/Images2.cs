using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
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

        public static async Task<ChunkHolder<SKColor>> GetColors(Angle lat, Angle lon, int zoomLevel)
        {
            // Need to figure out which chunks to load.
            string inputFile = Path.Combine(rootMapFolder, string.Format(imageCacheTemplate, lat.ToLatString(), lon.ToLonString(), zoomLevel));
            string metadFile = Path.Combine(rootMapFolder, string.Format(metadCacheTemplate, lat.ToLatString(), lon.ToLonString(), zoomLevel));
            Uri inputUrl = new Uri(string.Format(imageUrlTemplate, lat.DecimalDegree, lon.DecimalDegree, zoomLevel, bingMapsKey));
            Uri metadUrl = new Uri(string.Format(metadUrlTemplate, lat.DecimalDegree, lon.DecimalDegree, zoomLevel, bingMapsKey));

            if (!File.Exists(metadFile))
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage message = await client.GetAsync(inputUrl);
                    var content = await message.Content.ReadAsByteArrayAsync();
                    File.WriteAllBytes(inputFile, content);

                    string rawMetadata = await client.GetStringAsync(metadUrl);
                    File.WriteAllText(metadFile, rawMetadata);
                }
            }

            var jObject = JObject.Parse(File.ReadAllText(metadFile));
            double[] bbox = jObject["resourceSets"].First["resources"].First["bbox"].ToObject<double[]>();

            ChunkHolder <SKColor> ret = null;
            using (SKBitmap bm = SKBitmap.Decode(inputFile))
            {
                ret = new ChunkHolder<SKColor>(bm.Width, bm.Height - footerHeight,
                    Angle.FromDecimalDegrees(bbox[0]), Angle.FromDecimalDegrees(bbox[1]),
                    Angle.FromDecimalDegrees(bbox[2]), Angle.FromDecimalDegrees(bbox[3]),
                    (i, j) => bm.GetPixel(i, j));
            }

            return ret;
        }
    }
}
