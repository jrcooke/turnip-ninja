using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;

namespace AdfReader
{
    class Program
    {
        static void Main(string[] args)
        {

            //// Home
            //double lat = 47.684124;
            //double lon = -122.292357;

            // Near Juanteta
            double lat = 47.695736;
            double lon = -122.232330;

            double R = 100000;
            double deltaR = 10;
            //double minAngle = 85;
            //double maxAngle = 95;
            double minAngle = 89.5;
            double maxAngle = 90.5;

            double elevationViewMin = 0.0;
            double elevationViewMax = 5.0;
            double angularResolution = 0.001;

            var bothData = GetPolarData(
                lat, lon,
                R, deltaR,
                minAngle, maxAngle, angularResolution,
                (lat2, lon2, cosLat, metersPerElement) =>
                {
                    var c = Images.GetColor(lat2, lon2, cosLat, metersPerElement);
                    var h = Heights.GetHeight(lat2, lon2, cosLat, metersPerElement);
                    return new Tuple<float, Color>(h.Item3, c);
                });

            // Cache the function results.
            bothData = bothData
                .Select(p => p())
                .Select(p => new Func<Tuple<int, Tuple<float, Color>[]>>(() => p))
                .ToArray();

            int height = (int)(R / deltaR) - 1;
            Utils.WriteImageFile(
                bothData.Select(p => p()),
                bothData.Length, height,
                @"C:\Users\jcooke\Desktop\bbb.png",
                (a) => a.Item2);

            Utils.WriteImageFile(
                bothData.Select(p => p()),
                bothData.Length, height,
                @"C:\Users\jcooke\Desktop\aaa.png",
                (a) => Color.FromArgb(
                    (int)(a.Item1 / 1.000) % 256,
                    (int)(a.Item1 / 10.00) % 256,
                    (int)(a.Item1 / 100.0) % 256));

            int numParts = (int)(bothData.Length * (elevationViewMax - elevationViewMin) / (maxAngle - minAngle));
            IEnumerable<Tuple<int, Tuple<double, Color>[]>> polimage = CollapseToViewFromHere(bothData, deltaR, elevationViewMin, elevationViewMax, numParts);
            Utils.WriteImageFile(
                polimage,
                bothData.Length, numParts,
                @"C:\Users\jcooke\Desktop\testPol.png",
                (a) => a == null ? default(Color) : a.Item2);
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

        private static IEnumerable<Tuple<int, Tuple<double, Color>[]>> CollapseToViewFromHere(
            Func<Tuple<int, Tuple<float, Color>[]>>[] thetaRad,
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
                ConcurrentQueue<Tuple<int, Tuple<double, Color>[]>> ret = new ConcurrentQueue<Tuple<int, Tuple<double, Color>[]>>();

                var workers = thetaRad.Skip(outerThetaLoop).Take(batchSize);
                Parallel.ForEach(workers, (a) =>
                {
                    var itemResult = a();
                    Tuple<float, Color>[] heightsAtAngle = itemResult.Item2;
                    var item = new Tuple<int, Tuple<double, Color>[]>(
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

        private static Tuple<double, Color>[] CollapseToViewAlongRay(
            Tuple<float, Color>[] heightsAtAngle,
            double deltaR,
            double minViewAngle,
            double deltaTheta,
            int numParts)
        {
            Tuple<double, Color>[] ret = new Tuple<double, Color>[numParts];
            float eyeHeight = 10;
            float heightOffset = heightsAtAngle[0].Item1 + eyeHeight;

            int i = 0;
            for (int r = 1; r < heightsAtAngle.Length; r++)
            {
                var value = heightsAtAngle[r];
                double dist = deltaR * r;

                Color col = value.Item2;
                // Haze adds bluish overlay to colors. Say (195, 240, 247)
                double clearWeight = 0.2 + 0.8 / (1.0 + dist * dist * 1.0e-8);
                col = Color.FromArgb(
                    (int)(col.R * clearWeight + 195 * (1 - clearWeight)),
                    (int)(col.G * clearWeight + 240 * (1 - clearWeight)),
                    (int)(col.B * clearWeight + 247 * (1 - clearWeight)));

                double curTheta = Math.Atan2(value.Item1 - heightOffset, dist);
                while ((minViewAngle + i * deltaTheta) < curTheta && i < numParts)
                {
                    ret[i++] = new Tuple<double, Color>(dist, col);
                }
            }

            // Fill in the rest of the sky.
            while (i < numParts)
            {
                ret[i++] = new Tuple<double, Color>(1.0e10, Color.FromArgb(195, 240, 247));
            }

            return ret;
        }

    }
}
