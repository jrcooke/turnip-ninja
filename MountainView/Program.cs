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
            try
            {
                string outputFolder = Path.Combine(ConfigurationManager.AppSettings["OutputFolder"], "Output");
                if (true)
                {
                    var homeLat = Angle.FromDecimalDegrees(47.6867797);
                    var homeLon = Angle.FromDecimalDegrees(-122.2907541);

                    var xxx = ImageWorker2.GenerateData(homeLat, homeLon, 10).Result;
                    Utils.WriteImageFile(xxx, Path.Combine(outputFolder, "xxx.png"), a => a);

                    var yyy = Heights.GenerateData(homeLat, homeLon, 10);
                    Utils.WriteImageFile(yyy, Path.Combine(outputFolder, "yyy.png"), a => Utils.GetColorForHeight(a));

//                    var newONe = AdfReaderWorker.GetChunk(@"C:\Users\jrcoo\Desktop\Map\n48w123\grdn48w123_13");
                    //// Utils.WriteImageFile(newONe, Path.Combine(outputFolder, "newONe.png"), a => Utils.GetColorForHeight(a));
                    //ChunkHolder<float> ddd = newONe.RenderSubChunk(homeLat, homeLon,
                    //    Angle.FromMinutes(2), Angle.FromMinutes(2),
                    //    Angle.FromThirds(20), Angle.FromThirds(20),
                    //    Utils.WeightedFloatAverage);
                    //Utils.WriteImageFile(ddd, Path.Combine(outputFolder, "ddd.png"), a => Utils.GetColorForHeight(a));

                    var tttt = ImageWorker2.GetColors(homeLat, homeLon, 13).Result;
                    ChunkHolder<SKColor> ddd2 = tttt.RenderSubChunk(homeLat, homeLon,
                        Angle.FromMinutes(2), Angle.FromMinutes(2),
                        Angle.FromThirds(20), Angle.FromThirds(20),
                        Utils.WeightedColorAverage);
                    Utils.WriteImageFile(tttt, Path.Combine(outputFolder, "tttt.png"), a => a);
                    Utils.WriteImageFile(ddd2, Path.Combine(outputFolder, "ddd2.png"), a => a);
                }

                // Near Juanteta
                Config c = new Config();
                c = new Config()
                {
                    Lat = Angle.FromDecimalDegrees(47.695736),
                    Lon = Angle.FromDecimalDegrees(-122.232330),
                    R = 20000, // 60000,
                    DeltaR = 5,
                    MinAngle = 85,
                    MaxAngle = 95,
                    ElevationViewMin = -10.0,
                    ElevationViewMax = 10.0,
                    AngularResolution = 0.01,
                };

                // owego
                //c = new Config()
                //{
                //    Lat = 42.130303,
                //    Lon = -76.243376,
                //    R = 5000,
                //    DeltaR = 1,
                //    MinAngle = 180,
                //    MaxAngle = 270,
                //    ElevationViewMin = -25.0,
                //    ElevationViewMax = 5.0,
                //    AngularResolution = 0.05,
                //};


                if (true)
                {
                    for (int zoomLevel = 10; zoomLevel <= 14; zoomLevel++)
                    {
                        var pixels2 = Heights.GetChunk(c.Lat, c.Lon, zoomLevel);
                        Utils.WriteImageFile(
                            pixels2,
                            Path.Combine(outputFolder, "ChunkH" + zoomLevel + ".png"),
                            a => Utils.GetColorForHeight(a));

                        var pixels = Images.GetChunk(c.Lat, c.Lon, zoomLevel);
                        Utils.WriteImageFile(
                            pixels,
                            Path.Combine(outputFolder, "ChunkC" + zoomLevel + ".png"),
                            a => a);
                    }
                }

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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static Func<T[]>[] GetPolarData<T>(
            Angle lat, Angle lon,
            double R, double deltaR,
            double minTheta, double maxTheta, double deltaTheta,
            Func<Angle, Angle, double, double, T> getValue)
        {
            double deltaThetaRad = deltaTheta * Math.PI / 180;
            double cosLat = Math.Cos(lat.DecimalDegree * Math.PI / 180.0);

            List<Func<T[]>> actions = new List<Func<T[]>>();

            int iThetaMin = (int)(minTheta / deltaTheta);
            int iThetaMax = (int)(maxTheta / deltaTheta);

            for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
            {
                int i = iTheta;
                actions.Add(() => ComputeAlongRadius(lat, lon, R, deltaR, getValue, deltaThetaRad, cosLat, i));
            }

            return actions.ToArray();
        }

        public class Config
        {
            public Angle Lat { get; set; }
            public Angle Lon { get; set; }
            public double R { get; set; }
            public double DeltaR { get; set; }
            public double MinAngle { get; set; }
            public double MaxAngle { get; set; }
            public double ElevationViewMin { get; set; }
            public double ElevationViewMax { get; set; }
            public double AngularResolution { get; set; }
        }

        private static T[] ComputeAlongRadius<T>(Angle lat, Angle lon,
            double R, double deltaR,
            Func<Angle, Angle, double, double, T> getValue,
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
