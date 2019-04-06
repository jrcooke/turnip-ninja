using MountainView.ChunkManagement;
using MountainView.Mesh;
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

        public static double DistBetweenLatLon(GeoPolar2d p1, GeoPolar2d p2)
        {
            // haversine, https://www.movable-type.co.uk/scripts/latlong.html
            var phi1 = p1.Lat.Radians;
            var phi2 = p2.Lat.Radians;
            var deltaPhi = p2.Lat.Radians - p1.Lat.Radians;
            var deltaLam = p2.Lon.Radians - p1.Lon.Radians;

            var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                    Math.Sin(deltaLam / 2) * Math.Sin(deltaLam / 2) * Math.Cos(phi1) * Math.Cos(phi2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return AlphaMeters * c;
        }

        // https://en.wikipedia.org/wiki/Great-circle_distance#Formulas
        public static double AngleBetween(double theta1, double phi1, double theta2, double phi2)
        {
            return Math.Acos(
                Math.Sin(phi1) * Math.Sin(phi2) +
                Math.Cos(phi1) * Math.Cos(phi2) * Math.Cos(theta1 - theta2));
        }

        // https://www.movable-type.co.uk/scripts/latlong.html
        public static GeoPolar2d GetDestFromBearing(GeoPolar2d p1, Angle bearing, double d)
        {
            var phi1 = p1.Lat.Radians;
            var lam1 = p1.Lon.Radians;
            var brng = bearing.Radians;
            var R = AlphaMeters;

            var phi2 = Math.Asin(Math.Sin(phi1) * Math.Cos(d / R) +
                Math.Cos(phi1) * Math.Sin(d / R) * Math.Cos(brng));
            var lam2 = lam1 + Math.Atan2(
                Math.Sin(brng) * Math.Sin(d / R) * Math.Cos(phi1),
                Math.Cos(d / R) - Math.Sin(phi1) * Math.Sin(phi2));

            return new GeoPolar2d(phi2 * 180 / Math.PI, lam2 * 180 / Math.PI);
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

        public static GeoPolar2d GetSunPosition(Angle lat, Angle lon, DateTimeOffset curTime)
        {
            // From: http://www.powerfromthesun.net/Book/chapter03/chapter03.html

            // ts is standard time in decimal hours
            var J = curTime.ToUniversalTime().DayOfYear;
            var ts = curTime.ToUniversalTime().TimeOfDay.TotalHours;

            // solar time in radians
            var omega = Math.PI / 12 * (ts - 12) + lon.Radians
                + 0.170 / 12 * Math.PI * Math.Sin(4.0 * Math.PI * (J - 80) / 373.0)
                - 0.129 / 12 * Math.PI * Math.Sin(2.0 * Math.PI * (J - 8) / 355.0);

            // The solar declination in radians is approximated by
            var delta = 0.4093 * Math.Sin(2.0 * Math.PI * (J - 81) / 368.0);

            /*
            Local coords
            alpha is solar angle above horizon
            A is solar azimuthal angle
            S_z = sin alpha       (upward)
            S_e = cos alpha sin A (east pointing)
            S_n = cos alpha cos A (north pointing)

            Earth-center coords
            S'_m = cos delta cos omega (from center to equator, hits where observer meridian hits equator)
            S'_e = cos delta sin omega (eastward on equator)
            S'_p = sin delta           (north polar)

            Rotate up from polar up to z up
            S_z = S'_m cos lat + S'_p sin lat
            S_e = S'_e
            S_n = S'_p cos lat - S'_m sin lat

            Substituting
            sin alpha       = cos delta cos omega cos lat + sin delta           sin lon
            cos alpha sin A = cos delta sin omega
            cos alpha cos A = sin delta           cos lat - cos delta cos omega sin lon

            So
            alpha = asin (cos delta cos omega cos lat + sin delta sin lon)
            A     = atan2(cos delta sin omega , (sin delta cos lat - cos delta cos omega sin lon))

            */
            var alpha = Math.Asin(
                (Math.Cos(delta) * Math.Cos(omega) * Math.Cos(lat.Radians) + Math.Sin(delta) * Math.Sin(lat.Radians))
                );

            // Switch to A=0 be south
            var A = 2 * Math.PI - Math.Atan2(
                Math.Cos(delta) * Math.Sin(omega),
                Math.Sin(delta) * Math.Cos(lat.Radians) - Math.Cos(delta) * Math.Cos(omega) * Math.Sin(lat.Radians)
                );

            if (A > 2 * Math.PI) A -= 2 * Math.PI;

            var sunPos = new GeoPolar2d(A * 180 / Math.PI, alpha * 180 / Math.PI);
            return sunPos;
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
        public static Func<MyDColor, double>[] DColorToDoubleArray = new Func<MyDColor, double>[] { p => p.R, p => p.G, p => p.B };

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