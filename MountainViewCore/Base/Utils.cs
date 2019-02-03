﻿using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.IO;

namespace MountainView.Base
{
    public enum OutputType
    {
        JPEG,
        Bitmap,
        PNG,
    }

    public static class Utils
    {
        public const double AlphaMeters = 6378000.0;

        // Alpha is radius.
        public const double LengthOfLatDegree = AlphaMeters * Math.PI / 180.0;

        public static double DistBetweenLatLon(Angle lat1, Angle lon1, Angle lat2, Angle lon2)
        {
            // haversine, https://www.movable-type.co.uk/scripts/latlong.html
            var φ1 = lat1.Radians;
            var φ2 = lat2.Radians;
            var Δφ = lat2.Radians - lat1.Radians;
            var Δλ = lon2.Radians - lon1.Radians;

            var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                    Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2) * Math.Cos(φ1) * Math.Cos(φ2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return AlphaMeters * c;
        }

        public static Tuple<long, long> APlusDeltaMeters(Angle lat, Angle lon, double deltaX, double deltaY, double? cosLat = null)
        {
            double cosLatVal = cosLat ?? Math.Cos(lat.Radians);
            return new Tuple<long, long>(
                lat.Fourths + (long)(60 * 60 * 60 * 60 * deltaY / LengthOfLatDegree),
                lon.Fourths + (long)(60 * 60 * 60 * 60 * deltaX / LengthOfLatDegree / cosLatVal));
        }

        public static Angle DeltaMetersLat(Angle heading, double dist)
        {
            return Angle.FromDecimalDegrees(dist * Math.Cos(heading.Radians) / LengthOfLatDegree);
        }

        public static Angle DeltaMetersLon(Angle heading, double dist, double cosLat)
        {
            return Angle.FromDecimalDegrees(dist * Math.Sin(heading.Radians) / LengthOfLatDegree / cosLat);
        }

        private static Dictionary<int, MyColor> heightCache = new Dictionary<int, MyColor>();

        public static MyColor GetColorForHeight(float a)
        {
            int i = (int)a;
            if (!heightCache.TryGetValue(i, out MyColor value))
            {
                value = new MyColor(
                    (byte)((Math.Sin(a / 10.000) + 1.0) * 128.0),
                    (byte)((Math.Sin(a / 30.000) + 1.0) * 128.0),
                    (byte)((Math.Sin(a / 70.000) + 1.0) * 128.0));
                heightCache.Add(i, value);
            };

            return value;
        }

        public static MyColor WeightedColorAverage(int prevAveraged, MyColor prevAverage, MyColor toAdd)
        {
            if (prevAveraged == 0)
            {
                return toAdd;
            }
            else
            {
                return new MyColor(
                    (byte)((prevAverage.R * prevAveraged + toAdd.R) / (prevAveraged + 1)),
                    (byte)((prevAverage.G * prevAveraged + toAdd.G) / (prevAveraged + 1)),
                    (byte)((prevAverage.B * prevAveraged + toAdd.B) / (prevAveraged + 1)));
            }
        }

        public static void WeightedColorAverage(ref int prevAveraged, ref MyDColor average, ref MyColor toAdd)
        {
            average.R = (average.R * prevAveraged + toAdd.R) / (prevAveraged + 1);
            average.G = (average.G * prevAveraged + toAdd.G) / (prevAveraged + 1);
            average.B = (average.B * prevAveraged + toAdd.B) / (prevAveraged + 1);
            prevAveraged += 1;
        }

        public static float WeightedFloatAverage(int prevAveraged, float prevAverage, float toAdd)
        {
            return prevAveraged == 0 ? toAdd : (prevAverage * prevAveraged + toAdd) / (prevAveraged + 1);
        }

        public static bool Contains(Tuple<double, double>[] points, double lat, double lon)
        {
            bool result = false;
            for (int i = 0; i < points.Length - 1; i++)
            {
                if (
                    (
                        ((points[i + 1].Item2 <= lon) && (lon < points[i].Item2)) ||
                        ((points[i].Item2 <= lon) && (lon < points[i + 1].Item2))
                    ) &&
                    (lat < (points[i].Item1 - points[i + 1].Item1) * (lon - points[i + 1].Item2) /
                           (points[i].Item2 - points[i + 1].Item2) + points[i + 1].Item1))
                {
                    result = !result;
                }
            }

            return result;
        }

        public static Func<MyColor, double>[] ColorToDoubleArray = new Func<MyColor, double>[] { p => p.R, p => p.G, p => p.B };

        public static MyColor ColorFromDoubleArray(double[] p)
        {
            return new MyColor(
                (byte)(p[0] < 0 ? 0 : p[0] > 255 ? 255 : p[0]),
                (byte)(p[1] < 0 ? 0 : p[1] > 255 ? 255 : p[1]),
                (byte)(p[2] < 0 ? 0 : p[2] > 255 ? 255 : p[2]));
        }

        public static MemoryStream GetBitmap<T>(
            ChunkHolder<T> colorBuff,
            Func<T, MyColor> transform,
            OutputType outputType)
        {
            using (DirectBitmap bm = new DirectBitmap(colorBuff.LonSteps, colorBuff.LatSteps))
            {
                for (int i = 0; i < colorBuff.LatSteps; i++)
                {
                    var col = colorBuff.Data[i];
                    for (int j = 0; j < colorBuff.LonSteps; j++)
                    {
                        bm.SetPixel(j, i, transform(col[colorBuff.LonSteps - 1 - j]));
                    }
                }

                MemoryStream stream = new MemoryStream();
                bm.WriteFile(outputType, stream);
                // Rewind the stream...
                stream.Seek(0, SeekOrigin.Begin);

                return stream;
            }
        }

        public static void WriteImageFile<T>(
            ChunkHolder<T> colorBuff,
            string fileName,
            Func<T, MyColor> transform,
            OutputType outputType)
        {
            using (DirectBitmap bm = new DirectBitmap(colorBuff.LonSteps, colorBuff.LatSteps))
            {
                for (int i = 0; i < colorBuff.LatSteps; i++)
                {
                    var col = colorBuff.Data[i];
                    for (int j = 0; j < colorBuff.LonSteps; j++)
                    {
                        bm.SetPixel(j, i, transform(col[colorBuff.LonSteps - 1 - j]));
                    }
                }

                File.Delete(fileName);
                using (FileStream stream = File.OpenWrite(fileName))
                {
                    bm.WriteFile(outputType, stream);
                }
            }
        }

        public static void WriteImageFile<T>(
            T[][] colorBuff,
            string fileName,
            Func<T, MyColor> transform,
            OutputType outputType)
        {
            int width = colorBuff.Length;
            int height = colorBuff[0].Length;
            using (DirectBitmap bm = new DirectBitmap(width, height))
            {
                int i = 0;
                foreach (var col in colorBuff)
                {
                    for (int j = 0; j < height; j++)
                    {
                        bm.SetPixel(i, j, transform(col[j]));
                    }

                    i++;
                }

                File.Delete(fileName);
                using (FileStream stream = File.OpenWrite(fileName))
                {
                    bm.WriteFile(outputType, stream);
                }
            }
        }

        public static void WriteImageFile(
            int width,
            int height,
            string fileName,
            Func<int, int, MyColor> transform,
            OutputType outputType)
        {
            using (DirectBitmap bm = new DirectBitmap(width, height))
            {
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        bm.SetPixel(i, j, transform(i, j));
                    }
                }

                File.Delete(fileName);
                using (FileStream stream = File.OpenWrite(fileName))
                {
                    bm.WriteFile(outputType, stream);
                }
            }
        }
    }
}