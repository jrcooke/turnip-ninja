using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;
using SkiaSharp;
using System.IO;

namespace AdfReader
{
    class Program
    {
        static void Main(string[] args)
        {
            string outputFolder = ConfigurationManager.AppSettings["OutputFolder"];

            //// Home
            //double lat = 47.684124;
            //double lon = -122.292357;

            // Near Juanteta
            Config c = new Config();

            c = new Config()
            {
                Lat = 47.695736,
                Lon = -122.232330,
                R = 10000,
                DeltaR = 10,
                MinAngle = 85,
                MaxAngle = 95,
                ElevationViewMin = -5.0,
                ElevationViewMax = 5.0,
                AngularResolution = 0.001,
            };

            // owego
            c = new Config()
            {
                Lat = 42.130303,
                Lon = -76.243376,
                R = 1000,
                DeltaR = 1,
                MinAngle = 0,
                MaxAngle = 360,
                ElevationViewMin = -5.0,
                ElevationViewMax = 5.0,
                AngularResolution = 0.1,
            };

            //var pixels = Utils.Transpose(Images.GetChunk(c.Lat, c.Lon, 12));
            //Utils.WriteImageFile(
            //    pixels.Select((p, i) => new Tuple<int, SKColor[]>(i, p)),
            //    pixels.Length, pixels[0].Length,
            //    Path.Combine(outputFolder, "test.png"),
            //    (a) => a);

            var data = RawChunksV2.GetRawHeightsInMeters((int)c.Lat, (int)c.Lon);

            var heights = Utils.Transpose(data);
            Utils.WriteImageFile(
                heights.Select((p, i) => new Tuple<int, float[]>(i, p)),
                heights.Length, heights[0].Length,
                Path.Combine(outputFolder, "test2.png"),
                (a) => new SKColor(
                    (byte)((int)(a / 1.000) % 256),
                    (byte)((int)(a / 10.00) % 256),
                    (byte)((int)(a / 100.0) % 256)));

            var bothData = GetPolarData(
                c.Lat, c.Lon,
                c.R, c.DeltaR,
                c.MinAngle, c.MaxAngle, c.AngularResolution,
                (lat2, lon2, cosLat, metersPerElement) =>
                {
                    var col = Images.GetColor(lat2, lon2, cosLat, metersPerElement);
                    var h = Heights.GetHeight(lat2, lon2, cosLat, metersPerElement);
                    return new Tuple<float, SKColor>(h, col);
                });

            // Cache the function results.
            bothData = bothData
                .Select(p => p())
                .Select(p => new Func<Tuple<int, Tuple<float, SKColor>[]>>(() => p))
                .ToArray();

            int height = (int)(c.R / c.DeltaR) - 1;
            Utils.WriteImageFile(
                bothData.Select(p => p()),
                bothData.Length, height,
                Path.Combine(outputFolder, "bbb.png"),
                (a) => a.Item2);

            Utils.WriteImageFile(
                bothData.Select(p => p()),
                bothData.Length, height,
                Path.Combine(outputFolder, "aaa.png"),
                (a) => new SKColor(
                    (byte)((int)(a.Item1 / 1.000) % 256),
                    (byte)((int)(a.Item1 / 10.00) % 256),
                    (byte)((int)(a.Item1 / 100.0) % 256)));

            int numParts = (int)(bothData.Length * (c.ElevationViewMax - c.ElevationViewMin) / (c.MaxAngle - c.MinAngle));
            IEnumerable<Tuple<int, Tuple<double, SKColor>[]>> polimage = CollapseToViewFromHere(bothData, c.DeltaR, c.ElevationViewMin, c.ElevationViewMax, numParts);
            Utils.WriteImageFile(
                polimage,
                bothData.Length, numParts,
                Path.Combine(outputFolder, "testPol.png"),
                (a) => a == null ? default(SKColor) : a.Item2);
        }

        public static Func<Tuple<int, T[]>>[] GetPolarData<T>(
            double lat, double lon,
            double R, double deltaR,
            double minTheta, double maxTheta, double deltaTheta,
            Func<double, double, double, double, T> getValue)
        {
            double deltaThetaRad = deltaTheta * Math.PI / 180;
            double cosLat = Math.Cos(lat * Math.PI / 180.0);

            List<Func<Tuple<int, T[]>>> actions = new List<Func<Tuple<int, T[]>>>();

            int iThetaMin = (int)(minTheta / deltaTheta);
            int iThetaMax = (int)(maxTheta / deltaTheta);

            for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
            {
                int i = iTheta;
                actions.Add(() => new Tuple<int, T[]>(i - iThetaMin, ComputeAlongRadius(lat, lon, R, deltaR, getValue, deltaThetaRad, cosLat, i)));
            }

            return actions.ToArray();
        }

        public class Config
        {
            public double Lat { get; set; }
            public double Lon { get; set; }
            public double R { get; set; }
            public double DeltaR { get; set; }
            public double MinAngle { get; set; }
            public double MaxAngle { get; set; }
            public double ElevationViewMin { get; set; }
            public double ElevationViewMax { get; set; }
            public double AngularResolution { get; set; }
        }

        private static T[] ComputeAlongRadius<T>(double lat, double lon,
            double R, double deltaR,
            Func<double, double, double, double, T> getValue,
            double deltaThetaRad,
            double cosLat,
            int iTheta)
        {
            var ret = new T[(int)(R / deltaR) - 1];
            double cosTheta = Math.Cos(iTheta * deltaThetaRad);
            double sinTheta = Math.Sin(iTheta * deltaThetaRad);

            for (int iR = 1; iR < (int)(R / deltaR); iR++)
            {
                double r = iR * deltaR;
                var point = Utils.APlusDeltaMeters(lat, lon, r * sinTheta, r * cosTheta, cosLat);
                ret[iR - 1] = getValue(point.Item1, point.Item2, cosLat,
                    //Math.Max(deltaR, r * deltaThetaRad)
                    //r * deltaThetaRad
                    deltaR / 100.0
                    );
            }

            return ret;
        }

        private static IEnumerable<Tuple<int, Tuple<double, SKColor>[]>> CollapseToViewFromHere(
            Func<Tuple<int, Tuple<float, SKColor>[]>>[] thetaRad,
            double deltaR,
            double elevationViewMin, double elevationViewMax,
            int numParts)
        {
            int w = thetaRad.Length;

            double minViewAngle = elevationViewMin * Math.PI / 180.0;
            double maxViewAngle = elevationViewMax * Math.PI / 180.0;
            double deltaTheta = (maxViewAngle - minViewAngle) / numParts;

            int batchSize = 50;
            for (int outerThetaLoop = 0; outerThetaLoop < w; outerThetaLoop += batchSize)
            {
                ConcurrentQueue<Tuple<int, Tuple<double, SKColor>[]>> ret = new ConcurrentQueue<Tuple<int, Tuple<double, SKColor>[]>>();

                var workers = thetaRad.Skip(outerThetaLoop).Take(batchSize);
                Parallel.ForEach(workers, (a) =>
                {
                    var itemResult = a();
                    Tuple<float, SKColor>[] heightsAtAngle = itemResult.Item2;
                    var item = new Tuple<int, Tuple<double, SKColor>[]>(
                        itemResult.Item1,
                        CollapseToViewAlongRay(heightsAtAngle, deltaR, minViewAngle, deltaTheta, numParts));
                    ret.Enqueue(item);
                });

                foreach (var i2 in ret)
                {
                    yield return i2;
                }

                Console.WriteLine((w - outerThetaLoop) * 100.0 / w);
            }
        }

        private static Tuple<double, SKColor>[] CollapseToViewAlongRay(
            Tuple<float, SKColor>[] heightsAtAngle,
            double deltaR,
            double minViewAngle,
            double deltaTheta,
            int numParts)
        {
            Tuple<double, SKColor>[] ret = new Tuple<double, SKColor>[numParts];
            float eyeHeight = 10;
            float heightOffset = heightsAtAngle[0].Item1 + eyeHeight;

            int i = 0;
            for (int r = 1; r < heightsAtAngle.Length; r++)
            {
                var value = heightsAtAngle[r];
                double dist = deltaR * r;

                SKColor col = value.Item2;
                // Haze adds bluish overlay to colors. Say (195, 240, 247)
                double clearWeight = 0.2 + 0.8 / (1.0 + dist * dist * 1.0e-8);
                col = new SKColor(
                    (byte)(int)(col.Red * clearWeight + 195 * (1 - clearWeight)),
                    (byte)(int)(col.Green * clearWeight + 240 * (1 - clearWeight)),
                    (byte)(int)(col.Blue * clearWeight + 247 * (1 - clearWeight)));

                double curTheta = Math.Atan2(value.Item1 - heightOffset, dist);
                while ((minViewAngle + i * deltaTheta) < curTheta && i < numParts)
                {
                    ret[i++] = new Tuple<double, SKColor>(dist, col);
                }
            }

            // Fill in the rest of the sky.
            while (i < numParts)
            {
                ret[i++] = new Tuple<double, SKColor>(1.0e10, new SKColor(195, 240, 247));
            }

            return ret;
        }
    }
}
