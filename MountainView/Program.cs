using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using MountainViewDesktop.Interpolation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using MountainViewCore.Landmarks;

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
                    Task.WaitAll(GetPolarData(outputPath, c));
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

        public static async Task GetPolarData(string outputFolder, Config config)
        {
            double cosLat = Math.Cos(config.Lat.Radians);
            int numR = (int)(config.R / config.DeltaR);

            int iThetaMin = Angle.FloorDivide(config.MinAngle, config.AngularResolution);
            int iThetaMax = Angle.FloorDivide(config.MaxAngle, config.AngularResolution);
            HashSet<long> chunkKeys = new HashSet<long>();


            ColorHeight[][] ret = new ColorHeight[iThetaMax - iThetaMin][];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new ColorHeight[numR];
            }

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
                    var mult = iR * config.DeltaR / config.R;
                    ret[iTheta - iThetaMin][iR].LatDegrees = config.Lat.DecimalDegree + endRLat.DecimalDegree * mult;
                    ret[iTheta - iThetaMin][iR].LonDegrees = config.Lon.DecimalDegree + endRLon.DecimalDegree * mult;
                    ret[iTheta - iThetaMin][iR].Distance = iR * config.DeltaR;

                    double r = iR * config.DeltaR;
                    var point = Utils.APlusDeltaMeters(config.Lat, config.Lon, r * sinTheta, r * cosTheta, cosLat);
                    double metersPerElement = Math.Max(config.DeltaR / 10, r * config.AngularResolution.Radians);
                    var decimalDegreesPerElement = metersPerElement / (Utils.LengthOfLatDegree * cosLat);
                    var zoomLevel = StandardChunkMetadata.GetZoomLevel(decimalDegreesPerElement);
                    var chunkKey = StandardChunkMetadata.GetKey(point.Item1, point.Item2, zoomLevel);
                    chunkKeys.Add(chunkKey);
                    ret[iTheta - iThetaMin][iR].ChunkKey = chunkKey;
                }
            }

            int counter = 0;
            await Utils.ForEachAsync(chunkKeys, 5, async (chunkKey) =>
            {
                await Task.Delay(0);
                StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);

                NearestInterpolatingChunk<float> interpChunkH = null;
                NearestInterpolatingChunk<MyColor> interpChunkI = null;
                try
                {
                    interpChunkH = Heights.Current.GetLazySimpleInterpolator(chunk);
                    interpChunkI = Images.Current.GetLazySimpleInterpolator(chunk);

                    // Now do that again, but do the rendering per chunk.
                    for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
                    {
                        Angle theta = Angle.Multiply(config.AngularResolution, iTheta);
                        var endRLat = Utils.DeltaMetersLat(theta, config.R);
                        var endRLon = Utils.DeltaMetersLon(theta, config.R, cosLat);

                        // Get intersection between the chunk and the line we are going along.
                        // See which range of R intersects with chunk.
                        if (interpChunkH.TryGetIntersectLine(
                            config.Lat.DecimalDegree, endRLat.DecimalDegree,
                            config.Lon.DecimalDegree, endRLon.DecimalDegree,
                            out double loX, out double hiX))
                        {
                            int loR = Math.Max(1, (int)(loX * numR) + 1);
                            int hiR = Math.Min(numR, (int)(hiX * numR));
                            for (int iR = loR; iR < hiR; iR++)
                            {
                                if (interpChunkH.TryGetDataAtPoint(
                                    ret[iTheta - iThetaMin][iR].LatDegrees,
                                    ret[iTheta - iThetaMin][iR].LonDegrees,
                                    out float data))
                                {
                                    ret[iTheta - iThetaMin][iR].Height = data;
                                }
                            }
                        }
                    }

                    interpChunkH.Dispose();
                    interpChunkI.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                counter++;
                Console.WriteLine(counter + " of " + chunkKeys.Count);
            });

            NewMethod(outputFolder, config, ret, counter);
        }

        private static void NewMethod(string outputFolder, Config config, ColorHeight[][] ret, int counter)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var view = CollapseToViewFromHere(ret, config.DeltaR, config.ElevationViewMin, config.ElevationViewMax, config.AngularResolution);

            Dictionary<long, NearestInterpolatingChunk<MyColor>> images = new Dictionary<long, NearestInterpolatingChunk<MyColor>>();
            // Haze adds bluish overlay to colors. Say (195, 240, 247)
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

            foreach (var x in images.Values)
            {
                x.Dispose();
            }

            Utils.WriteImageFile(xxx, Path.Combine(outputFolder, "xxi" + counter + ".jpg"), a => a, OutputType.JPEG);

            var xxx2 = view.Select(q => q.Select(p => p.ChunkKey == 0 ? null : UsgsRawFeatures.GetData(p.LatDegrees, p.LonDegrees)).ToArray()).ToArray();
            Utils.WriteImageFile(xxx2, Path.Combine(outputFolder, "xxf" + counter + ".bmp"), p =>
            {
                return p == null ? new MyColor(0, 0, 0) :
                new MyColor(
                    (byte)((p.Id - short.MinValue) / 256 % 256),
                    (byte)((p.Id - short.MinValue) % 256),
                    (byte)((p.Id - short.MinValue) / 256 / 256 % 256));
            }, OutputType.Bitmap);


            /*
    <div id="image_map">
        <map name="map_example">
             <area
                 href="https://facebook.com"
                 alt="Facebook"
                 target="_blank"
                 shape=poly
                 coords="30,100, 140,50, 290,220, 180,280">
             <area
                href="https://en.wikipedia.org/wiki/Social_media"
                target="_blank"
                alt="Wikipedia Social Media Article"
                shape=poly
                coords="190,75, 200,60, 495,60, 495,165, 275,165">
        </map>
         <img
            src="../../wp-content/uploads/image_map_example_shapes.png"
            alt="image map example"
            width=500
            height=332
            usemap="#map_example">
    </div>

             */

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
