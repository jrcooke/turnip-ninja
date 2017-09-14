using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using MountainViewDesktop.Interpolation;
using SkiaSharp;
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
            //foreach (var x in Images.Current.ScanAll())
            //{
            //    Console.WriteLine(x.Item1);
            //    bool keepGoing = true;
            //    foreach (var y in x.Item2.Data)
            //    {
            //        foreach (var z in y)
            //        {
            //            if (z.Red == 0 && z.Green == 0 && z.Blue == 0)
            //            {
            //                Console.WriteLine("Bad chunk!");
            //                File.Delete(x.Item1);
            //                keepGoing = false;
            //                break;
            //            }
            //        }

            //        if (!keepGoing) break;
            //    }
            //}

            //Images.ShowRange();
            OneDInterpolator.Test();

            string of = Path.Combine(ConfigurationManager.AppSettings["OutputFolder"], "Output");
            Tests.Test3(of, Config.Juaneta());

            try
            {
                string outputFolder = Path.Combine(ConfigurationManager.AppSettings["OutputFolder"], "Output");
                Config c = Config.Juaneta();
                Task.WaitAll(GetPolarData(c));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static async Task GetPolarData(Config config)
        {
            double cosLat = Math.Cos(config.Lat.Radians);

            int iThetaMin = Angle.FloorDivide(config.MinAngle, config.AngularResolution);
            int iThetaMax = Angle.FloorDivide(config.MaxAngle, config.AngularResolution);
            HashSet<long> chunkKeys = new HashSet<long>();
            for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
            {
                Angle theta = Angle.Multiply(config.AngularResolution, iTheta);
                double cosTheta = Math.Cos(theta.Radians);
                double sinTheta = Math.Sin(theta.Radians);
                for (int iR = 1; iR < (int)(config.R / config.DeltaR); iR++)
                {
                    double r = iR * config.DeltaR;
                    var point = Utils.APlusDeltaMeters(config.Lat, config.Lon, r * sinTheta, r * cosTheta, cosLat);
                    double metersPerElement = Math.Max(config.DeltaR / 10, r * config.AngularResolution.Radians);
                    var zoomLevel = (int)(12 - Math.Log(metersPerElement * 540 * 20 / (Utils.LengthOfLatDegree * cosLat), 2));
                    zoomLevel = zoomLevel > StandardChunkMetadata.MaxZoomLevel ? StandardChunkMetadata.MaxZoomLevel : zoomLevel;
                    chunkKeys.Add(StandardChunkMetadata.GetKey(point.Item1, point.Item2, zoomLevel));
                }
            }

            ColorHeight[][] ret = new ColorHeight[iThetaMax - iThetaMin][];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new ColorHeight[(int)(config.R / config.DeltaR)];
            }

            int counter = 0;

            // TODO: Add a function to partition these loose chunks into a few mega chunks to render in parallel
            await Utils.ForEachAsync(chunkKeys, 3, async (chunkKey) =>
            {
                double[] buffer = new double[1];
                double[] bufferR = new double[3];

                StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);
                var interpChunk = (await Heights.Current.GetData(chunk))
                    .GetInterpolator(new Func<float, double>[] { p => p }, p => (float)p[0], InterpolatonType.Nearest);
                var interpChunkR = (await Images.Current.GetData(chunk))
                    .GetInterpolator(Utils.ColorToDoubleArray, Utils.ColorFromDoubleArray, InterpolatonType.Nearest);

                // Now do that again, but do the rendering per chunk.
                for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
                {
                    Angle theta = Angle.Multiply(config.AngularResolution, iTheta);

                    // Use this angle to compute a heading.
                    var endRLat = Utils.DeltaMetersLat(theta, config.R);
                    var endRLon = Utils.DeltaMetersLon(theta, config.R, cosLat);

                    for (int iR = 1; iR < (int)(config.R / config.DeltaR); iR++)
                    {
                        var mult = iR * config.DeltaR / config.R;

                        var curLatDegree = config.Lat.DecimalDegree + endRLat.DecimalDegree * mult;
                        var curLonDegree = config.Lon.DecimalDegree + endRLon.DecimalDegree * mult;
                        if (interpChunk.TryGetDataAtPoint(curLatDegree, curLonDegree, buffer, out float data) &&
                            interpChunkR.TryGetDataAtPoint(curLatDegree, curLonDegree, bufferR, out SKColor color))
                        {
                            ret[iTheta - iThetaMin][iR] = new ColorHeight { Color = color, Height = data };
                        }
                    }
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
            public SKColor Color;
            public float Height;
        }

        public struct ColorDistance
        {
            public SKColor Color;
            public double Distance;
        }

        // Haze adds bluish overlay to colors. Say (195, 240, 247)
        private static readonly SKColor skyColor = new SKColor(195, 240, 247);

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
                    SKColor col = thetaRad[i][r].Color;
                    double clearWeight = 0.2 + 0.8 / (1.0 + dist * dist * 1.0e-8);
                    col = new SKColor(
                        (byte)(int)(col.Red * clearWeight + skyColor.Red * (1 - clearWeight)),
                        (byte)(int)(col.Green * clearWeight + skyColor.Green * (1 - clearWeight)),
                        (byte)(int)(col.Blue * clearWeight + skyColor.Blue * (1 - clearWeight)));

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
