﻿using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using MountainViewDesktop.Interpolation;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
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
            OneDInterpolator.Test();
            try
            {
                string outputFolder = Path.Combine(ConfigurationManager.AppSettings["OutputFolder"], "Output");
                Config c = Config.Juaneta();

                //var bothData =
                Task.WaitAll(GetPolarData(c));

                /*
                // Cache the function results.
                bothData = bothData
                    .Select(p => p())
                    .Select(p => new Func<Tuple<float, SKColor>[]>(() => p))
                    .ToArray();

                int height = (int)(c.R / c.DeltaR) - 1;
                Utils.WriteImageFile(
                    bothData.Select(p => p()).ToArray(),
                    bothData.Length, height,
                    Path.Combine(outputFolder, "bbb.png"),
                    (a) => a.Item2);

                Utils.WriteImageFile(
                    bothData.Select(p => p()).ToArray(),
                    bothData.Length, height,
                    Path.Combine(outputFolder, "aaa.png"),
                    (a) => new SKColor(
                        (byte)((Math.Sin(a.Item1 / 20.0 / 1.000) + 1.0) * 128.0),
                        (byte)((Math.Sin(a.Item1 / 20.0 / 10.00) + 1.0) * 128.0),
                        (byte)((Math.Sin(a.Item1 / 20.0 / 100.0) + 1.0) * 128.0)));

                int numParts = (int)(bothData.Length * (c.ElevationViewMax - c.ElevationViewMin) / (c.MaxAngle - c.MinAngle));
                IEnumerable<Tuple<double, SKColor>[]> polimage = CollapseToViewFromHere(bothData, c.DeltaR, c.ElevationViewMin, c.ElevationViewMax, numParts);
                Utils.WriteImageFile(
                    polimage.ToArray(),
                    bothData.Length, numParts,
                    Path.Combine(outputFolder, "testPol.png"),
                    (a) => a == null ? default(SKColor) : a.Item2);


    */
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
                    var len = Utils.LengthOfLatDegree * cosLat;
                    var zoomLevel = (int)(12 - Math.Log(metersPerElement * 540 * 20 / len, 2));
                    zoomLevel = zoomLevel > StandardChunkMetadata.MaxZoomLevel ? StandardChunkMetadata.MaxZoomLevel : zoomLevel;
                    long key = StandardChunkMetadata.GetKey(point.Item1, point.Item2, zoomLevel);
                    chunkKeys.Add(key);
                }
            }

            int nTheta = iThetaMax - iThetaMin;
            int nR = (int)(config.R / config.DeltaR);

            ColorHeight[][] ret = new ColorHeight[nTheta][];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new ColorHeight[nR];
                for (int j = 0; j < nR; j++)
                {
                    ret[i][j] = new ColorHeight();
                }
            }

            int counter = 0;

            // TODO: Add a function to partition these loose chunks into a few mega chunks to render in parallel
            await Utils.ForEachAsync(chunkKeys, 3, async (chunkKey) =>
            {
                StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);

                var heightChunk = await Heights.Current.GetData(chunk);
                var imageChunk = await Images.Current.GetData(chunk);

                var interpChunk = heightChunk.GetInterpolator(
                    p => p,
                    p => (float)p,
                    InterpolatonType.Nearest);
                var interpChunkR = imageChunk.GetInterpolator(
                    p => p.Red,
                    p => new SKColor((byte)(p < 0 ? 0 : p > 255 ? 255 : p), 0, 0),
                    InterpolatonType.Nearest);
                var interpChunkG = imageChunk.GetInterpolator(
                    p => p.Green,
                    p => new SKColor(0, (byte)(p < 0 ? 0 : p > 255 ? 255 : p), 0),
                    InterpolatonType.Nearest);
                var interpChunkB = imageChunk.GetInterpolator(
                    p => p.Blue,
                    p => new SKColor(0, 0, (byte)(p < 0 ? 0 : p > 255 ? 255 : p)),
                    InterpolatonType.Nearest);

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
                        if (interpChunk.TryGetDataAtPoint(curLatDegree, curLonDegree, out float data))
                        {
                            byte r = 0;
                            byte g = 0;
                            byte b = 0;
                            if (interpChunkR.TryGetDataAtPoint(curLatDegree, curLonDegree, out SKColor color)) r = color.Red;
                            if (interpChunkG.TryGetDataAtPoint(curLatDegree, curLonDegree, out color)) g = color.Green;
                            if (interpChunkB.TryGetDataAtPoint(curLatDegree, curLonDegree, out color)) b = color.Blue;

                            ret[iTheta - iThetaMin][iR].Height = data;
                            ret[iTheta - iThetaMin][iR].Color = new SKColor(r, g, b);
                        }
                    }
                }

                counter++;
                Console.WriteLine(counter);
                if (counter % 50 == 0)
                {
                    //Utils.WriteImageFile(ret, ret.Length, ret[0].Length,
                    //    @"C:\Users\jrcoo\Desktop\tmp" + counter + ".png",
                    //    a => Utils.GetColorForHeight(a.Height));
                    //Utils.WriteImageFile(ret, ret.Length, ret[0].Length,
                    //    @"C:\Users\jrcoo\Desktop\tmi" + counter + ".png",
                    //    a => a.Color);

                    var xxx1 = CollapseToViewFromHere(ret, config.DeltaR, config.ElevationViewMin, config.ElevationViewMax, config.AngularResolution);

                    Utils.WriteImageFile(xxx1, xxx1.Length, xxx1[0].Length,
                        @"C:\Users\jrcoo\Desktop\xxx" + counter + ".png",
                        a => Utils.GetColorForHeight((float)a.Distance));
                    Utils.WriteImageFile(xxx1, xxx1.Length, xxx1[0].Length,
                        @"C:\Users\jrcoo\Desktop\xxi" + counter + ".png",
                        a => a.Color);
                }
            });

            Utils.WriteImageFile(ret, ret.Length, ret[0].Length,
                @"C:\Users\jrcoo\Desktop\tmp" + counter + ".png",
                a => Utils.GetColorForHeight(a.Height));
            Utils.WriteImageFile(ret, ret.Length, ret[0].Length,
                @"C:\Users\jrcoo\Desktop\tmi" + counter + ".png",
                a => a.Color);

            var xxx = CollapseToViewFromHere(ret, config.DeltaR, config.ElevationViewMin, config.ElevationViewMax, config.AngularResolution);

            Utils.WriteImageFile(xxx, xxx.Length, xxx[0].Length,
                @"C:\Users\jrcoo\Desktop\xxx" + counter + ".png",
                a => Utils.GetColorForHeight((float)a.Distance));
            Utils.WriteImageFile(xxx, xxx.Length, xxx[0].Length,
                @"C:\Users\jrcoo\Desktop\xxi" + counter + ".png",
                a => a.Color);
        }

        public class ColorHeight
        {
            public SKColor Color { get; set; }
            public float Height { get; set; }
        }

        public class ColorDistance
        {
            public SKColor Color { get; set; }
            public double Distance { get; set; }
        }

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
                ret[i] = CollapseToViewAlongRay(thetaRad[i], deltaR, elevationViewMin.Radians, angularRes.Radians, numParts);
            }

            return ret;
        }

        private static ColorDistance[] CollapseToViewAlongRay(
            ColorHeight[] heightsAtAngle,
            double deltaR,
            double minViewAngleRad,
            double deltaThetaRad,
            int numParts)
        {
            ColorDistance[] ret = new ColorDistance[numParts];
            float eyeHeight = 10;
            float heightOffset = heightsAtAngle[0].Height + eyeHeight;

            int i = 0;
            for (int r = 1; r < heightsAtAngle.Length; r++)
            {
                var value = heightsAtAngle[r];
                double dist = deltaR * r;

                SKColor col = value.Color;
                // Haze adds bluish overlay to colors. Say (195, 240, 247)
                double clearWeight = 0.2 + 0.8 / (1.0 + dist * dist * 1.0e-8);
                col = new SKColor(
                    (byte)(int)(col.Red * clearWeight + 195 * (1 - clearWeight)),
                    (byte)(int)(col.Green * clearWeight + 240 * (1 - clearWeight)),
                    (byte)(int)(col.Blue * clearWeight + 247 * (1 - clearWeight)));

                double curTheta = Math.Atan2(value.Height - heightOffset, dist);
                while ((minViewAngleRad + i * deltaThetaRad) < curTheta && i < numParts)
                {
                    ret[i++] = new ColorDistance { Distance = dist, Color = col };
                }
            }

            // Fill in the rest of the sky.
            while (i < numParts)
            {
                ret[i++] = new ColorDistance { Distance = 1.0e10, Color = new SKColor(195, 240, 247) };
            }

            return ret;
        }
    }
}
