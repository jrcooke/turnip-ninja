using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace AdfReader
{
    public static class Utils
    {
        private static Dictionary<int, string> filenameCache = new Dictionary<int, string>();

        private const double AlphaMeters = 6378000.0;

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

        public static Tuple<double, double> APlusDeltaMeters(double lat, double lon, double deltaX, double deltaY, double? cosLat = null)
        {
            double cosLatVal = cosLat.HasValue ? cosLat.Value : Math.Cos(lat * Math.PI / 180);
            return new Tuple<double, double>(
                lat + deltaY / LengthOfLatDegree,
                lon + deltaX / LengthOfLatDegree / cosLatVal);
        }

        internal static T[][] Transpose<T>(T[][] items)
        {
            int width = items.Length;
            int height = items[0].Length;
            var ret = new T[height][];
            for (int i = 0; i < height; i++)
            {
                ret[i] = new T[width];
                for (int j = 0; j < width; j++)
                {
                    ret[i][j] = items[j][i];
                }
            }

            return ret;
        }

        internal static U[][] Apply<T,U>(T[][] items, Func<T,U> map)
        {
            var ret = new U[items.Length][];
            for (int i = 0; i < items.Length; i++)
            {
                ret[i] = new U[items[i].Length];
                for (int j = 0; j < items[i].Length; j++)
                {
                    ret[i][j] = map(items[i][j]);
                }
            }

            return ret;
        }

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
            Func<T, SKColor> transform)
        {
            using (DirectBitmap bm = new DirectBitmap(width, height))
            {
                foreach (var col in colorBuff)
                {
                    int i = col.Item1;
                    var cbc = col.Item2;
                    for (int j = 0; j < height; j++)
                    {
                        bm.SetPixel(i, j, transform(cbc[j]));
                    }
                }

                bm.WriteFile(fileName);
            }
        }

        private class DirectBitmap : IDisposable
        {
            private SKBitmap bitmap;
            private byte[] bits;
            private bool disposed;
            private GCHandle bitsHandle;

            public DirectBitmap(int width, int height)
            {
                bits = new byte[width * height * 4];
                bitsHandle = GCHandle.Alloc(bits, GCHandleType.Pinned);
                bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
                bitmap.SetPixels(bitsHandle.AddrOfPinnedObject());
            }

            public void SetPixel(int i, int j, SKColor color)
            {
                int offset = 4 * ((bitmap.Height - 1 - j) * bitmap.Width + i);
                bits[offset++] = color.Red;
                bits[offset++] = color.Green;
                bits[offset++] = color.Blue;
                bits[offset++] = color.Alpha;
            }

            public void WriteFile(string fileName)
            {
                using (var image = SKImage.FromBitmap(bitmap))
                {
                    using (var data = image.Encode(SKEncodedImageFormat.Png, 80))
                    {
                        using (var stream = File.OpenWrite(fileName))
                        {
                            data.SaveTo(stream);
                        }
                    }
                }
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                bitmap.Dispose();
                bitsHandle.Free();
            }
        }
    }
}