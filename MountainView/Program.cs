using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using MountainViewCore.Base;
using MountainViewCore.Landmarks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MountainView
{
    class Program
    {
        static void Main(string[] args)
        {
            DateTime start = DateTime.Now;
            int serverLat = 47;
            int serverLon = -123;

            string outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", "Output");
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            bool isServerUpload = false;
            bool isServerCompute = false;
            bool isClient = true;
            try
            {
                //Task.WaitAll(Foo());
                //Tests.Test12();

                //Task.WaitAll(Tests.Test3(     outputPath,
                //    Angle.FromDecimalDegrees(47.6867797),
                //    Angle.FromDecimalDegrees(-122.2907541)));

                if (isServerUpload)
                {
                    string uploadPath = "/home/mcuser/turnip-ninja/MountainView/bin/Debug/netcoreapp2.0";
                    UsgsRawChunks.Uploader(uploadPath, serverLat, serverLon);
                    UsgsRawImageChunks.Uploader(uploadPath, serverLat, serverLon);
                }
                else if (isServerCompute)
                {
                    Task.WaitAll(ProcessRawData(
                        Angle.FromDecimalDegrees(serverLat + 0.5),
                        Angle.FromDecimalDegrees(serverLon - 0.5)));
                }
                else if (isClient)
                {
                    BlobHelper.CacheLocally = true;
                    Config c = Config.Juaneta();
                    GetPolarData(outputPath, c);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            DateTime end = DateTime.Now;
            Console.WriteLine(start);
            Console.WriteLine(end);
            Console.WriteLine(end - start);
        }

        public static async Task ProcessRawData(Angle lat, Angle lon)
        {
            // Generate for a 1 degree square region.
            StandardChunkMetadata template = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, 2);
            await Heights.Current.ProcessRawData(template);
            await Images.Current.ProcessRawData(template);
        }

        public static void GetPolarData(string outputFolder, Config config)
        {
            float eyeHeight = 5;

            double cosLat = Math.Cos(config.Lat.Radians);
            int numR = (int)(config.R / config.DeltaR);

            int iThetaMin = Angle.FloorDivide(config.MinAngle, config.AngularResolution);
            int iThetaMax = Angle.FloorDivide(config.MaxAngle, config.AngularResolution);
            HashSet<long> chunkKeys = new HashSet<long>();
            var distToNearestPointInChunk = new Dictionary<long, int>();
            for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
            {
                // Use this angle to compute a heading.
                Angle theta = Angle.Multiply(config.AngularResolution, iTheta);
                var endRLat = Utils.DeltaMetersLat(theta, config.R);
                var endRLon = Utils.DeltaMetersLon(theta, config.R, cosLat);

                double cosTheta = Math.Cos(theta.Radians);
                double sinTheta = Math.Sin(theta.Radians);
                for (int iR = 1; iR < numR; iR++)
                {
                    double r = iR * config.DeltaR;
                    var point = Utils.APlusDeltaMeters(config.Lat, config.Lon, r * sinTheta, r * cosTheta, cosLat);
                    double metersPerElement = Math.Max(config.DeltaR / 10, r * config.AngularResolution.Radians);

                    var decimalDegreesPerElement = metersPerElement / (Utils.LengthOfLatDegree * cosLat);
                    var zoomLevel = StandardChunkMetadata.GetZoomLevel(decimalDegreesPerElement);
                    var chunkKey = StandardChunkMetadata.GetKey(point.Item1, point.Item2, zoomLevel);
                    chunkKeys.Add(chunkKey);

                    if (distToNearestPointInChunk.TryGetValue(chunkKey, out int val))
                    {
                        if (val > iR) distToNearestPointInChunk[chunkKey] = iR;
                    }
                    else
                    {
                        distToNearestPointInChunk.Add(chunkKey, iR);
                    }

                    StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);
                    if (chunk.TryGetIntersectLine(
                        config.Lat.DecimalDegree, endRLat.DecimalDegree,
                        config.Lon.DecimalDegree, endRLon.DecimalDegree,
                        out double loX, out double hiX))
                    {
                        iR = Math.Max(iR, Math.Min(numR, (int)(hiX * numR)) - 1);
                    }
                }
            }

            int numParts = (int)((config.ElevationViewMax.Radians - config.ElevationViewMin.Radians) / config.AngularResolution.Radians);
            int[] j = new int[iThetaMax - iThetaMin];
            ColorHeight[][] ret = new ColorHeight[iThetaMax - iThetaMin][];
            float? heightOffset = null;
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new ColorHeight[numParts];
            }

            int counter = 0;
            foreach (var chunkKey in chunkKeys.OrderBy(p => distToNearestPointInChunk[p]))
            {
                Console.WriteLine(distToNearestPointInChunk[chunkKey]);
                NearestInterpolatingChunk<float> interpChunkH = null;
                try
                {
                    StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);
                    interpChunkH = Heights.Current.GetLazySimpleInterpolator(chunk);

                    // Now do that again, but do the rendering per chunk.
                    for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
                    {
                        Angle theta = Angle.Multiply(config.AngularResolution, iTheta);
                        var endRLat = Utils.DeltaMetersLat(theta, config.R);
                        var endRLon = Utils.DeltaMetersLon(theta, config.R, cosLat);

                        // Get intersection between the chunk and the line we are going along.
                        // See which range of R intersects with chunk.
                        if (chunk.TryGetIntersectLine(
                            config.Lat.DecimalDegree, endRLat.DecimalDegree,
                            config.Lon.DecimalDegree, endRLon.DecimalDegree,
                            out double loX, out double hiX))
                        {
                            int loR = Math.Max(1, (int)(loX * numR) + 1);
                            int hiR = Math.Min(numR, (int)(hiX * numR));
                            for (int iR = loR; iR < hiR; iR++)
                            {
                                var mult = iR * config.DeltaR / config.R;
                                var latDegrees = config.Lat.DecimalDegree + endRLat.DecimalDegree * mult;
                                var lonDegrees = config.Lon.DecimalDegree + endRLon.DecimalDegree * mult;
                                if (interpChunkH.TryGetDataAtPoint(latDegrees, lonDegrees, out float height))
                                {
                                    if (!heightOffset.HasValue) heightOffset = height + eyeHeight;

                                    double distance = iR * config.DeltaR;
                                    double curTheta = Math.Atan2(height - heightOffset.Value, distance);
                                    var delta = curTheta - config.ElevationViewMin.Radians;
                                    var norm = delta / config.AngularResolution.Radians;
                                    while (j[iTheta - iThetaMin] < norm)
                                    {
                                        if (j[iTheta - iThetaMin] == numParts) break;
                                        ret[iTheta - iThetaMin][j[iTheta - iThetaMin]++] = new ColorHeight()
                                        {
                                            ChunkKey = chunkKey,
                                            LatDegrees = latDegrees,
                                            LonDegrees = lonDegrees,
                                            Distance = distance,
                                            Height = height,
                                        };
                                    }
                                }
                            }
                        }
                    }

                    interpChunkH.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                counter++;
                Console.WriteLine(counter + " of " + chunkKeys.Count);
            };

            ProcessOutput(outputFolder, config, ret);
        }

        private static void ProcessOutput(string outputFolder, Config config, ColorHeight[][] view)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            Dictionary<long, NearestInterpolatingChunk<MyColor>> images = new Dictionary<long, NearestInterpolatingChunk<MyColor>>();
            try
            {
                // Haze adds bluish overlay to colors
                MyColor skyColor = new MyColor(195, 240, 247);
                var xxx = view.Select(q => q.Select(p =>
                    {
                        if (p.ChunkKey == 0)
                        {
                            return skyColor;
                        }

                        if (!images.TryGetValue(p.ChunkKey, out NearestInterpolatingChunk<MyColor> image))
                        {
                            StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(p.ChunkKey);
                            image = Images.Current.GetLazySimpleInterpolator(chunk);
                            images[p.ChunkKey] = image;
                        }

                        image.TryGetDataAtPoint(p.LatDegrees, p.LonDegrees, out MyColor color);
                        double clearWeight = 0.2 + 0.8 / (1.0 + p.Distance * p.Distance * 1.0e-8);
                        return new MyColor(
                            (byte)(int)(color.R * clearWeight + skyColor.R * (1 - clearWeight)),
                            (byte)(int)(color.G * clearWeight + skyColor.G * (1 - clearWeight)),
                            (byte)(int)(color.B * clearWeight + skyColor.B * (1 - clearWeight)));
                    }).ToArray()).ToArray();
                Utils.WriteImageFile(xxx, Path.Combine(outputFolder, "xxi.jpg"), a => a, OutputType.JPEG);
            }
            finally
            {
                foreach (var x in images.Values)
                {
                    x.Dispose();
                }
            }

            var features = view.Select(q => q.Select(p => p.ChunkKey == 0 ? null : UsgsRawFeatures.GetData(p.LatDegrees, p.LonDegrees)).ToArray()).ToArray();
            var polys = GetPolygons(features);
            var polymap = polys
                .Where(p => p.Value != null)
                .Select(p => new
                {
                    id = p.Value.Id,
                    alt = p.Value.Name,
                    coords = string.Join(",", p.Border.Select(q => q.X + "," + (view[0].Length - 1 - q.Y))),
                })
                .Select(p => "<area href='" + p.alt + "' title='" + p.alt + "' alt='" + p.alt + "' shape='poly' coords='" + p.coords + "' >")
                .ToArray();
            var maptxt = @"
<HTML>
<HEAD>
<TITLE>title of page</TITLE>
</HEAD>
<BODY>
<div id='image_map'>
<map name='map_example'>
" + string.Join("\r\n", polymap) + @"
</map>
<img
    src='xxi.jpg'
    usemap='#map_example' >
</div>
</BODY>
</HTML>
";

            File.WriteAllText(Path.Combine(outputFolder, "text.html"), maptxt);
        }

        private static IEnumerable<Polygon<T>> GetPolygons<T>(T[][] values) where T : class
        {
            int minSize = 100;
            int width = values.Length;
            int height = values[0].Length;

            Polygon<T>[][] cache = new Polygon<T>[width][];
            for (int i = 0; i < width; i++)
            {
                cache[i] = new Polygon<T>[height];
            }

            var ret = new List<Polygon<T>>();
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    if (cache[i][j] == null)
                    {
                        // start a new flood fill
                        var cur = new Polygon<T>(values[i][j]);
                        cur.FloodFill(values, cache, i, j);

                        if (cur.Count < minSize && j > 0)
                        {
                            var replacement = cache[i][j - 1];
                            for (int i2 = Math.Max(0, i - minSize); i2 < Math.Min(width, i + minSize); i2++)
                            {
                                for (int j2 = Math.Max(0, j - minSize); j2 < Math.Min(height, j + minSize); j2++)
                                {
                                    if (cache[i2][j2] == cur)
                                    {
                                        cache[i2][j2] = replacement;
                                    }
                                }
                            }
                        }
                        else
                        {
                            ret.Add(cur);
                        }
                    }
                }
            }

            foreach (var poly in ret)
            {
                poly.CacheBoundary(cache);
            }

            return ret.ToArray();
        }

        public struct ColorHeight
        {
            public float Height;
            public double Distance;
            public double LatDegrees;
            public double LonDegrees;
            internal long ChunkKey;
        }

        private static ColorHeight[][] CollapseToViewFromHere(
            ColorHeight[][] thetaRad,
            double deltaR,
            Angle elevationViewMin, Angle elevationViewMax,
            Angle angularRes)
        {
            ColorHeight[][] ret = new ColorHeight[thetaRad.Length][];
            int numParts = (int)((elevationViewMax.Radians - elevationViewMin.Radians) / angularRes.Radians);
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new ColorHeight[numParts];
                float eyeHeight = 10;
                float heightOffset = thetaRad[i][0].Height + eyeHeight;

                int j = 0;
                for (int r = 1; r < thetaRad[i].Length; r++)
                {
                    double curTheta = Math.Atan2(thetaRad[i][r].Height - heightOffset, thetaRad[i][r].Distance);
                    while ((elevationViewMin.Radians + j * angularRes.Radians) < curTheta && j < numParts)
                    {
                        ret[i][j++] = thetaRad[i][r];
                    }
                }
            }

            return ret;
        }
    }
}

