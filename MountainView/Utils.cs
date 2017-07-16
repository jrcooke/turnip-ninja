﻿using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace MountainView
{
    public static class Utils
    {
        private static Dictionary<long, string> filenameCache = new Dictionary<long, string>();

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

        public static Tuple<Angle, Angle> APlusDeltaMeters(Angle lat, Angle lon, double deltaX, double deltaY, double? cosLat = null)
        {
            double cosLatVal = cosLat ?? Math.Cos(lat.DecimalDegree * Math.PI / 180);
            return new Tuple<Angle, Angle>(
                Angle.Add(lat, deltaY / LengthOfLatDegree),
                Angle.Add(lon, deltaX / LengthOfLatDegree / cosLatVal));
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

        internal static SKColor GetColorForHeight(float a)
        {
            return new SKColor(
                (byte)((Math.Sin(a / 10.000) + 1.0) * 128.0),
                (byte)((Math.Sin(a / 30.000) + 1.0) * 128.0),
                (byte)((Math.Sin(a / 70.000) + 1.0) * 128.0));
        }

        internal static U[][] Apply<T, U>(T[][] items, Func<T, U> map)
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

        public static long GetKey(int zoomLevel, Angle lat, Angle lon)
        {
            var key =
                (long)(lat.TotalSeconds + 180 * 60 * 60) * (long)0x100000000 +
                (long)(lon.TotalSeconds + 180 * 60 * 60) * (long)0x10 +
                zoomLevel;
            return key;
        }

        public static string GetFileName(long key)
        {
            string filename;
            while (!filenameCache.TryGetValue(key, out filename))
            {
                lock (filenameCache)
                {
                    if (!filenameCache.TryGetValue(key, out filename))
                    {
                        int zoomLevel = (int)(key % 0x10);
                        int lonTotSec = (int)(key % (long)(0x100000000)) / 0x10 - 180 * 60 * 60;
                        int latTotSec = (int)(key / (long)(0x100000000)) - 180 * 60 * 60;
                        Angle lat = Angle.FromSeconds(latTotSec);
                        Angle lon = Angle.FromSeconds(lonTotSec);
                        filename = string.Format("{0}{1}{2:D2}", lat.ToLatString(), lon.ToLonString(), zoomLevel);
                        filenameCache.Add(key, filename);
                    }
                }
            }

            return filename;
        }

        public static void WriteImageFile<T>(
            ChunkHolder<T> colorBuff,
            string fileName,
            Func<T, SKColor> transform)
        {
            using (DirectBitmap bm = new DirectBitmap(colorBuff.Width, colorBuff.Height))
            {
                for (int i = 0; i < colorBuff.Width; i++)
                {
                    var col = colorBuff.Data[i];
                    for (int j = 0; j < colorBuff.Height; j++)
                    {
                        bm.SetPixel(i, j, transform(col[colorBuff.Height - 1 - j]));
                    }
                }

                File.Delete(fileName);
                bm.WriteFile(fileName);
            }
        }


        public static void WriteImageFile<T>(
            IEnumerable<T[]> colorBuff,
            int width,
            int height,
            string fileName,
            Func<T, SKColor> transform)
        {
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