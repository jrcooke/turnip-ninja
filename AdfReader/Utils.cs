using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AdfReader
{
    public static class Utils
    {

        public const double AlphaMeters = 6378000.0;

        // Alpha is radius.
        public const double LengthOfLatDegree = AlphaMeters * Math.PI / 180.0;

        public static int ReadInt(FileStream stream, byte[] buffer)
        {
            stream.Read(buffer, 0, 4);
            return BitConverter.ToInt32(buffer, 0);
        }

        public static int TruncateTowardsZero(double a)
        {
            return Math.Sign(a) * (int)Math.Abs(a);
        }

        public static double AddAwayFromZero(double a, double b)
        {
            return Math.Sign(a) * (Math.Abs(a) + b);
        }

        public static int AddAwayFromZero(int a, int b)
        {
            return Math.Sign(a) * (Math.Abs(a) + b);
        }


        public static Tuple<double, double> AMinusBInDeltaMeters(double latA, double lonA, double latB, double lonB, double? cosLat = null)
        {
            double cosLatVal = cosLat.HasValue ? cosLat.Value : Math.Cos(latA * Math.PI / 180);
            return new Tuple<double, double>(
                (lonA - lonB) * LengthOfLatDegree * cosLatVal,
                (latA - latB) * LengthOfLatDegree);

            //    double latRad = lat * Math.PI / 180.0;
            //    double lonRad = lon * Math.PI / 180.0;
            //    double latPrime = xMeters / alpha;
            //    double lonPrime = yMeters / alpha;
            //    double sinLat2 =
            //        (Math.Sin(latRad) * Math.Cos(latPrime) * Math.Cos(lonPrime) + Math.Cos(latRad) * Math.Sin(lonPrime));
            //    double tanLon2 =
            //        (Math.Sin(latPrime) * Math.Cos(lonPrime)) /
            //        (Math.Cos(latRad) * Math.Cos(latPrime) * Math.Cos(lonPrime) - Math.Sin(latRad) * Math.Sin(lonPrime));
            //    double lat2 = alpha * Math.PI / 180.0 * (Math.Asin(sinLat2) * 180.0 / Math.PI - lat);
            //    double lon2 = alpha * Math.PI / 180.0 * (Math.Atan(tanLon2) * 180.0 / Math.PI); // + lon;
            //    return new LatLon(phi, lambda);
        }

        public static Tuple<double, double> APlusDeltaMeters(double lat, double lon, double deltaX, double deltaY, double? cosLat = null)
        {
            double cosLatVal = cosLat.HasValue ? cosLat.Value : Math.Cos(lat * Math.PI / 180);
            return new Tuple<double, double>(
                lat + deltaY / LengthOfLatDegree,
                lon + deltaX / LengthOfLatDegree / cosLatVal);
        }

        public static U[][] ApplyMap<T, U>(T[][] imageData, Func<T, U> map)
        {
            int w = imageData.Length;
            int h = imageData[0].Length;

            U[][] ret = new U[w][];
            for (int i = 0; i < w; i++)
            {
                ret[i] = new U[h];
                for (int j = 0; j < h; j++)
                {
                    ret[i][j] = map(imageData[i][j]);
                }
            }

            return ret;
        }

        public static Tuple<T, U>[][] Merge<T, U>(T[][] a, U[][] b)
        {
            int w = a.Length;
            if (b.Length != w) throw new ArgumentOutOfRangeException();
            int h = a[0].Length;
            if (b[0].Length != h) throw new ArgumentOutOfRangeException();

            Tuple<T, U>[][] ret = new Tuple<T, U>[w][];
            for (int i = 0; i < w; i++)
            {
                ret[i] = new Tuple<T, U>[h];
                for (int j = 0; j < h; j++)
                {
                    ret[i][j] = new Tuple<T, U>(a[i][j], b[i][j]);
                }
            }

            return ret;
        }

        private static Dictionary<int, string> filenameCache = new Dictionary<int, string>();
        public static int GetKey(int zoomLevel, int latTotMin, int lonTotMin)
        {
            int laP = latTotMin;
            int loP = -lonTotMin;
            int ret0 = laP;
            int ret1 = ret0 * (60 * 360);
            int ret2 = ret1 + loP;
            int ret3 = ret2 * 20;
            int ret4 = ret3 + zoomLevel;
            return ret4;
        }
        public static string GetFileName(int key)
        {
            string filename;
            while (!filenameCache.TryGetValue(key, out filename))
            {
                lock (filenameCache)
                {
                    if (!filenameCache.TryGetValue(key, out filename))
                    {
                        int zoomLevel = key % 20;
                        int lonTotMin = -(key / 20) % (60 * 360);
                        int latTotMin = key / 20 / 60 / 360;

                        char latDir = latTotMin > 0 ? 'n' : 's';
                        char lonDir = lonTotMin > 0 ? 'e' : 'w';
                        int latMin = Math.Abs(latTotMin) % 60;
                        int lonMin = Math.Abs(lonTotMin) % 60;
                        int latDeg = Math.Abs(latTotMin) / 60;
                        int lonDeg = Math.Abs(lonTotMin) / 60;
                        filename = string.Format("{0:D3}D{1:D2}M{2}{3:D3}D{4:D2}M{5}{6:D2}",
                             latDeg, latMin, latDir,
                             lonDeg, lonMin, lonDir, zoomLevel);
                        filenameCache.Add(key, filename);
                    }
                }
            }

            return filename;
        }

        public static void WriteImageFile<T>(
            IEnumerable<Tuple<int, T[]>> colorBuff,
            int width,
            int height,
            string fileName,
            Func<T, Color> transform)
        {
            using (DirectBitmap bm = new DirectBitmap(width, height))
            //            using (Bitmap bm = new Bitmap(width, height))
            {
                foreach (var col in colorBuff)
                {
                    int i = col.Item1;
                    var cbc = col.Item2;
                    for (int j = 0; j < bm.Height; j++)
                    {
                        bm.Bits[(bm.Height - 1 - j) * bm.Width + i] = transform(cbc[j]).ToArgb();
                        //                        bm.SetPixel(i, bm.Height - 1 - j, transform(cbc[j]));
                    }
                }

                //                bm.Save(fileName, ImageFormat.Png);
                bm.Bitmap.Save(fileName, ImageFormat.Png);
            }
        }

        public static void WriteImageFile<T>(
            T[][] colorBuff,
            string fileName,
            Func<T, Color> transform)
        {
            using (DirectBitmap bm = new DirectBitmap(colorBuff.Length, colorBuff[0].Length))
            {
                int i;
                for (i = 0; i < bm.Width; i++)
                {
                    for (int j = 0; j < bm.Height; j++)
                    {
                        bm.Bits[(bm.Height - 1 - j) * bm.Width + i] = transform(colorBuff[i][j]).ToArgb();
                    }
                }

                Task t = new Task(async () =>
                {
                    try
                    {
                        while (i < bm.Width)
                        {
                            Console.WriteLine("Projecting to bitmap: " + (i * 100.0 / bm.Width) + "%");
                            await Task.Delay(5000);
                        }
                    }
                    catch { }
                });
                t.Start();

                bm.Bitmap.Save(fileName, ImageFormat.Png);
            }
        }

        private class DirectBitmap : IDisposable
        {
            public Bitmap Bitmap { get; private set; }
            public int[] Bits { get; private set; }
            public bool Disposed { get; private set; }
            public int Height { get; private set; }
            public int Width { get; private set; }

            protected GCHandle BitsHandle { get; private set; }

            public DirectBitmap(int width, int height)
            {
                Width = width;
                Height = height;
                Bits = new int[width * height];
                BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
                Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, BitsHandle.AddrOfPinnedObject());
            }

            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true;
                Bitmap.Dispose();
                BitsHandle.Free();
            }
        }
    }

 }
