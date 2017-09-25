using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using MountainViewDesktop.Interpolation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MountainView
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                //                Tests.Test12();

                //Task.WaitAll(ProcessRawData(
                //    Angle.FromDecimalDegrees(47.5),
                //    Angle.FromDecimalDegrees(-121.5)));

                //                Task.WaitAll(Tests.Test3(Path.Combine(ConfigurationManager.AppSettings["OutputFolder"], "Output"), Config.Home()));
                //Task.WaitAll(
                //    Tests.Test3(
                //        Path.Combine(ConfigurationManager.AppSettings["OutputFolder"], "Output"),
                //        Angle.FromDecimalDegrees(47.5),
                //        Angle.FromDecimalDegrees(-121.5)));

                // So lets have the resolutions be 60, 20, 4, 1
                // Natural resolution for the elevations is 20T per pixel, images 4T per pixel.


                //     string outputFolder = Path.Combine(ConfigurationManager.AppSettings["OutputFolder"], "Output");
                Config c = Config.Juaneta();
                Task.WaitAll(GetPolarData(c));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static async Task ProcessRawData(Angle lat, Angle lon)
        {
            // Generate for a 1 degree square region.
            StandardChunkMetadata template = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, 2);
            await Heights.Current.ProcessRawData(template);
            await Images.Current.ProcessRawData(template);
        }

        public static async Task GetPolarData(Config config)
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
            await Utils.ForEachAsync(chunkKeys, 4, async (chunkKey) =>
            {
                double[] bufferH = new double[1];
                double[] bufferI = new double[3];

                StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);

                InterpolatingChunk<float> interpChunkH = null;
                InterpolatingChunk<MyColor> interpChunkI = null;
                try
                {
                    interpChunkH = (await Heights.Current.GetData(chunk)).GetInterpolator(InterpolatonType.Nearest);
                    interpChunkI = (await Images.Current.GetData(chunk)).GetInterpolator(InterpolatonType.Nearest);

                    // Now do that again, but do the rendering per chunk.
                    for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
                    {
                        Angle theta = Angle.Multiply(config.AngularResolution, iTheta);

                        // Use this angle to compute a heading.
                        var endRLat = Utils.DeltaMetersLat(theta, config.R);
                        var endRLon = Utils.DeltaMetersLon(theta, config.R, cosLat);
                        for (int iR = 1; iR < numR; iR++)
                        {
                            var mult = iR * config.DeltaR / config.R;
                            var curLatDegree = config.Lat.DecimalDegree + endRLat.DecimalDegree * mult;
                            var curLonDegree = config.Lon.DecimalDegree + endRLon.DecimalDegree * mult;
                            if (interpChunkH.TryGetDataAtPoint(curLatDegree, curLonDegree, bufferH, out float data) &&
                                interpChunkI.TryGetDataAtPoint(curLatDegree, curLonDegree, bufferI, out MyColor color))
                            {
                                ret[iTheta - iThetaMin][iR] = new ColorHeight { Color = color, Height = data };
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
                    NewMethod(config, ret, counter);
                }
            });

            NewMethod(config, ret, counter);
        }

        private static void NewMethod(Config config, ColorHeight[][] ret, int counter)
        {
            string outputFolder = Path.Combine(ConfigurationManager.AppSettings["OutputFolder"], "Output");
            Utils.WriteImageFile(ret, Path.Combine(outputFolder, "tmp" + counter + ".png"), a => Utils.GetColorForHeight(a.Height));
            Utils.WriteImageFile(ret, Path.Combine(outputFolder, "tmi" + counter + ".png"), a => a.Color);

            var xxx = CollapseToViewFromHere(ret, config.DeltaR, config.ElevationViewMin, config.ElevationViewMax, config.AngularResolution);
            Utils.WriteImageFile(xxx, Path.Combine(outputFolder, "xxx" + counter + ".png"), a => Utils.GetColorForHeight((float)a.Distance));
            Utils.WriteImageFile(xxx, Path.Combine(outputFolder, "xxi" + counter + ".png"), a => a.Color);
        }

        public struct ColorHeight
        {
            public MyColor Color;
            public float Height;
        }

        public struct ColorDistance
        {
            public MyColor Color;
            public double Distance;
        }

        // Haze adds bluish overlay to colors. Say (195, 240, 247)
        private static readonly MyColor skyColor = new MyColor(195, 240, 247);

        private static ColorDistance[][] CollapseToViewFromHere(
            ColorHeight[][] thetaRad,
            double deltaR,
            Angle elevationViewMin, Angle elevationViewMax,
            Angle angularRes)
        {
            ColorDistance[][] ret = new ColorDistance[thetaRad.Length][];
            int numParts = (int)((elevationViewMax.Radians - elevationViewMin.Radians) / angularRes.Radians);
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new ColorDistance[numParts];
                float eyeHeight = 10;
                float heightOffset = thetaRad[i][0].Height + eyeHeight;

                int j = 0;
                for (int r = 1; r < thetaRad[i].Length; r++)
                {
                    double dist = deltaR * r;
                    MyColor col = thetaRad[i][r].Color;
                    double clearWeight = 0.2 + 0.8 / (1.0 + dist * dist * 1.0e-8);
                    col = new MyColor(
                        (byte)(int)(col.R * clearWeight + skyColor.R * (1 - clearWeight)),
                        (byte)(int)(col.G * clearWeight + skyColor.G * (1 - clearWeight)),
                        (byte)(int)(col.B * clearWeight + skyColor.B * (1 - clearWeight)));

                    double curTheta = Math.Atan2(thetaRad[i][r].Height - heightOffset, dist);
                    while ((elevationViewMin.Radians + j * angularRes.Radians) < curTheta && j < numParts)
                    {
                        ret[i][j++] = new ColorDistance { Distance = dist, Color = col };
                    }
                }

                // Fill in the rest of the sky.
                while (j < numParts)
                {
                    ret[i][j++] = new ColorDistance { Distance = 1.0e10, Color = skyColor };
                }
            }

            return ret;
        }
    }
}
