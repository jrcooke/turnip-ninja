using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MountainView
{
    class Program
    {
        public static bool ShowOutput;

        static void Main(string[] args)
        {
            NRCubicSplineInterpolator.Test();
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
            double cosLat = Math.Cos(config.Lat.DecimalDegree * Math.PI / 180.0);

            int iThetaMin = Angle.FloorDivide(config.MinAngle, config.AngularResolution);
            int iThetaMax = Angle.FloorDivide(config.MaxAngle, config.AngularResolution);
            HashSet<long> chunkKeys = new HashSet<long>();
            for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
            {
                Angle theta = Angle.Multiply(config.AngularResolution, iTheta);
                double cosTheta = Math.Cos(theta.DecimalDegree);
                double sinTheta = Math.Sin(theta.DecimalDegree);
                for (int iR = 1; iR < (int)(config.R / config.DeltaR); iR++)
                {
                    double r = iR * config.DeltaR;
                    var point = Utils.APlusDeltaMeters(config.Lat, config.Lon, r * sinTheta, r * cosTheta, cosLat);
                    double metersPerElement = config.DeltaR / 100.0;
                    var len = Utils.LengthOfLatDegree * cosLat;
                    var zoomLevel = (int)(12 - Math.Log(metersPerElement * 540 * 20 / len, 2));
                    zoomLevel = zoomLevel > StandardChunkMetadata.MaxZoomLevel ? StandardChunkMetadata.MaxZoomLevel : zoomLevel;

                    long key = StandardChunkMetadata.GetKey(point.Item1, point.Item2, zoomLevel);
                    chunkKeys.Add(key);
                }
            }

            int nTheta = iThetaMax - iThetaMin;
            int nR = (int)(config.R / config.DeltaR);

            float[][] ret = new float[nTheta][];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new float[nR];
            }

            int counter = 0;

            // TODO: Add a function to partition these loose chunks into a few mega chunks to render in parallel

            Parallel.ForEach(chunkKeys, (chunkKey) =>
//            foreach (var chunkKey in chunkKeys)
            {
                StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);

                var heightChunk = Heights.Current.GetData(chunk).Result;
                if (heightChunk.LonSteps == 0)
                {
                    // TODO: Get right fix
                    return;
                }

                var interpChunk = heightChunk.GetInterpolator(p => p, p => (float)p);

                // Now do that again, but do the rendering per chunk.
                for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
                {
                    Angle theta = Angle.Multiply(config.AngularResolution, iTheta);

                    // Use this angle to compute a heading.
                    var endRLat = Utils.DeltaMetersLat(theta, config.R);
                    var endRLon = Utils.DeltaMetersLon(theta, config.R, cosLat);

                    var interp = interpChunk; // interpChunk.GetInterpolatorForLine(config.Lat, config.Lon, Angle.Add(config.Lat, endRLat), Angle.Add(config.Lon, endRLon));

                    List<Angle> lats = new List<Angle>();
                    List<Angle> lons = new List<Angle>();
                    for (int iR = 1; iR < (int)(config.R / config.DeltaR); iR++)
                    {
                        var mult = iR * config.DeltaR / config.R;
                        var curLat = Angle.Add(config.Lat, Angle.Multiply(endRLat, mult));
                        var curLon = Angle.Add(config.Lon, Angle.Multiply(endRLon, mult));
                        lats.Add(curLat);
                        lons.Add(curLon);

                        float data;
                        if (interp.TryGetDataAtPoint(curLat, curLon, out data))
                        {
                            ret[iTheta - iThetaMin][iR] = data;
                          //  Console.WriteLine(curLat.ToLatString() + ", " + curLon.ToLonString() + " = " + data);
                        }
                        else
                        {
                            //             Console.WriteLine(curLat.ToLatString() + ", " + curLon.ToLonString() + " = N/A");
                        }
                    }
                }

                counter++;
                Console.WriteLine(counter);
                if (counter % 50 == 0)
                {
                    Utils.WriteImageFile(ret, ret.Length, ret[0].Length,
                        @"C:\Users\jrcoo\Desktop\tmp" + counter + ".png",
                        a => Utils.GetColorForHeight(a));
                    ShowOutput = false;
                }


                //   var pixels2 = await Heights.Current.GetData(chunk);
                //   var pixels = await Images.Current.GetData(chunk);
            });

            Utils.WriteImageFile(ret, ret.Length, ret[0].Length,
                @"C:\Users\jrcoo\Desktop\tmp" + counter + ".png",
                a => Utils.GetColorForHeight(a));

        }

        public static Task ForEachAsync<T>(IEnumerable<T> source, int concurrency, Func<T, Task> body)
        {
            return Task.WhenAll(
                Partitioner.Create(source)
                    .GetPartitions(concurrency)
                    .Select(partition =>
                        Task.Run(async delegate
                        {
                            using (partition)
                            {
                                while (partition.MoveNext())
                                {
                                    await body(partition.Current);
                                }
                            }
                        })));
        }





        private static IEnumerable<Tuple<double, SKColor>[]> CollapseToViewFromHere(
            Func<Tuple<float, SKColor>[]>[] thetaRad,
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
                ConcurrentQueue<Tuple<double, SKColor>[]> ret = new ConcurrentQueue<Tuple<double, SKColor>[]>();

                var workers = thetaRad.Skip(outerThetaLoop).Take(batchSize);
                Parallel.ForEach(workers, (a) =>
                {
                    var itemResult = a();
                    Tuple<float, SKColor>[] heightsAtAngle = itemResult;
                    var item = CollapseToViewAlongRay(heightsAtAngle, deltaR, minViewAngle, deltaTheta, numParts);
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
