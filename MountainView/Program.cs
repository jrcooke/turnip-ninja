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
            for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
            {
                Angle theta = Angle.Multiply(config.AngularResolution, iTheta);
                double cosTheta = Math.Cos(theta.Radians);
                double sinTheta = Math.Sin(theta.Radians);
                for (int iR = 1; iR < numR; iR++)
                {
                    double r = iR * config.DeltaR;
                    var point = Utils.APlusDeltaMeters(config.Lat, config.Lon, r * sinTheta, r * cosTheta, cosLat);
                    double metersPerElement = Math.Max(config.DeltaR / 10, r * config.AngularResolution.Radians);
                    var decimalDegreesPerElement = metersPerElement / (Utils.LengthOfLatDegree * cosLat);
                    var zoomLevel = StandardChunkMetadata.GetZoomLevel(decimalDegreesPerElement);
                    chunkKeys.Add(StandardChunkMetadata.GetKey(point.Item1, point.Item2, zoomLevel));
                }
            }

            ColorHeight[][] ret = new ColorHeight[iThetaMax - iThetaMin][];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new ColorHeight[numR];
            }

            int counter = 0;

            // TODO: Add a function to partition these loose chunks into a few mega chunks to render in parallel
            await Utils.ForEachAsync(chunkKeys, 5, async (chunkKey) =>
            {
                StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);

                NearestInterpolatingChunk<float> interpChunkH = null;
                NearestInterpolatingChunk<MyColor> interpChunkI = null;
                try
                {
                    interpChunkH = (await Heights.Current.GetData(chunk)).GetSimpleInterpolator(InterpolatonType.Nearest);
                    interpChunkI = (await Images.Current.GetData(chunk)).GetSimpleInterpolator(InterpolatonType.Nearest);

                    // Now do that again, but do the rendering per chunk.
                    for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
                    {
                        Angle theta = Angle.Multiply(config.AngularResolution, iTheta);

                        // Use this angle to compute a heading.
                        var endRLat = Utils.DeltaMetersLat(theta, config.R);
                        var endRLon = Utils.DeltaMetersLon(theta, config.R, cosLat);

                        // Get intersection between the chunk and the line we are going along.

                        //                        int numR = (int)(config.R / config.DeltaR);

                        // See which range of R intersects with chunk.
                        if (interpChunkH.TryGetIntersectLine(
                            config.Lat.DecimalDegree, endRLat.DecimalDegree,
                            config.Lon.DecimalDegree, endRLon.DecimalDegree,
                            out double loX, out double hiX))
                        {
                            int loR = Math.Max(1, (int)(loX * numR));
                            int hiR = Math.Min(numR, (int)(hiX * numR));
                            for (int iR = loR; iR < hiR; iR++)
                            {
                                var mult = iR * config.DeltaR / config.R;
                                var curLatDegree = config.Lat.DecimalDegree + endRLat.DecimalDegree * mult;
                                var curLonDegree = config.Lon.DecimalDegree + endRLon.DecimalDegree * mult;
                                if (interpChunkH.TryGetDataAtPoint(curLatDegree, curLonDegree, out float data) &&
                                    interpChunkI.TryGetDataAtPoint(curLatDegree, curLonDegree, out MyColor color))
                                {
                                    var feature = UsgsRawFeatures.GetData(curLatDegree, curLonDegree);
                                    ret[iTheta - iThetaMin][iR] = new ColorHeight { Color = color, Height = data, Feature = feature };
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                counter++;
                Console.WriteLine(counter + " of " + chunkKeys.Count);
                if (counter % 50 == 0)
                {
                    NewMethod(outputFolder, config, ret, counter);
                }
            });

            NewMethod(outputFolder, config, ret, counter);
        }

        private static void NewMethod(string outputFolder, Config config, ColorHeight[][] ret, int counter)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            Utils.WriteImageFile(ret, Path.Combine(outputFolder, "tmp" + counter + ".jpg"), a => Utils.GetColorForHeight(a.Height), OutputType.JPEG);
            Utils.WriteImageFile(ret, Path.Combine(outputFolder, "tmi" + counter + ".jpg"), a => a.Color, OutputType.JPEG);
            Utils.WriteImageFile(ret, Path.Combine(outputFolder, "tmf" + counter + ".bmp"), a => new MyColor(
                (byte)(((a.Feature?.Id ?? short.MinValue) - short.MinValue) / 256 % 256),
                (byte)(((a.Feature?.Id ?? short.MinValue) - short.MinValue) % 256),
                (byte)(((a.Feature?.Id ?? short.MinValue) - short.MinValue) / 256 / 256 % 256)), OutputType.Bitmap);

            var xxx = CollapseToViewFromHere(
                ret,
                (p, dist) =>
                {
                    double clearWeight = 0.2 + 0.8 / (1.0 + dist * dist * 1.0e-8);
                    return new MyColor(
                        (byte)(int)(p.Color.R * clearWeight + skyColor.R * (1 - clearWeight)),
                        (byte)(int)(p.Color.G * clearWeight + skyColor.G * (1 - clearWeight)),
                        (byte)(int)(p.Color.B * clearWeight + skyColor.B * (1 - clearWeight)));
                },
                skyColor,
                config.DeltaR,
                config.ElevationViewMin,
                config.ElevationViewMax,
                config.AngularResolution);
            //            Utils.WriteImageFile(xxx, Path.Combine(outputFolder, "xxx" + counter + ".png"), a => Utils.GetColorForHeight((float)a.Distance));
            Utils.WriteImageFile(xxx, Path.Combine(outputFolder, "xxi" + counter + ".jpg"), a => a, OutputType.JPEG);

            var xxx2 = CollapseToViewFromHere(
                ret,
                (p, dist) => p.Feature,
                null,
                config.DeltaR,
                config.ElevationViewMin,
                config.ElevationViewMax,
                config.AngularResolution);


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

            Utils.WriteImageFile(xxx2, Path.Combine(outputFolder, "xxf" + counter + ".bmp"), p =>
            {
                return p == null ? new MyColor(0, 0, 0) :
                new MyColor(
                    (byte)((p.Id - short.MinValue) / 256 % 256),
                    (byte)((p.Id - short.MinValue) % 256),
                    (byte)((p.Id - short.MinValue) / 256 / 256 % 256));
            }, OutputType.Bitmap);
        }

        public struct ColorHeight
        {
            public MyColor Color;
            public float Height;
            public FeatureInfo Feature;
        }

        // Haze adds bluish overlay to colors. Say (195, 240, 247)
        private static readonly MyColor skyColor = new MyColor(195, 240, 247);

        private static T[][] CollapseToViewFromHere<T>(
            ColorHeight[][] thetaRad,
            Func<ColorHeight, double, T> colorGetter,
            T defaultValue,
            double deltaR,
            Angle elevationViewMin, Angle elevationViewMax,
            Angle angularRes)
        {
            T[][] ret = new T[thetaRad.Length][];
            int numParts = (int)((elevationViewMax.Radians - elevationViewMin.Radians) / angularRes.Radians);
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new T[numParts];
                float eyeHeight = 10;
                float heightOffset = thetaRad[i][0].Height + eyeHeight;

                int j = 0;
                for (int r = 1; r < thetaRad[i].Length; r++)
                {
                    double dist = deltaR * r;
                    T col = colorGetter(thetaRad[i][r], dist);

                    double curTheta = Math.Atan2(thetaRad[i][r].Height - heightOffset, dist);
                    while ((elevationViewMin.Radians + j * angularRes.Radians) < curTheta && j < numParts)
                    {
                        ret[i][j++] = col;
                    }
                }

                // Fill in the rest of the sky.
                while (j < numParts)
                {
                    ret[i][j++] = defaultValue;
                }
            }

            return ret;
        }
    }
}
