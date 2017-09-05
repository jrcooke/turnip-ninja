using MountainView.ChunkManagement;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MountainView.Base
{
    public static class Utils
    {
        private static Dictionary<long, string> filenameCache = new Dictionary<long, string>();

        private const double AlphaMeters = 6378000.0;

        // Alpha is radius.
        public const double LengthOfLatDegree = AlphaMeters * Math.PI / 180.0;

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

        internal static SKColor GetColorForHeight(float a)
        {
            return new SKColor(
                (byte)((Math.Sin(a / 10.000) + 1.0) * 128.0),
                (byte)((Math.Sin(a / 30.000) + 1.0) * 128.0),
                (byte)((Math.Sin(a / 70.000) + 1.0) * 128.0));
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

        public static SKColor WeightedColorAverage(int prevAveraged, SKColor prevAverage, SKColor toAdd)
        {
            if (prevAveraged == 0)
            {
                return toAdd;
            }
            else
            {
                return new SKColor(
                    (byte)((prevAverage.Red * prevAveraged + toAdd.Red) / (prevAveraged + 1)),
                    (byte)((prevAverage.Green * prevAveraged + toAdd.Green) / (prevAveraged + 1)),
                    (byte)((prevAverage.Blue * prevAveraged + toAdd.Blue) / (prevAveraged + 1)));
            }
        }

        public static float WeightedFloatAverage(int prevAveraged, float prevAverage, float toAdd)
        {
            return prevAveraged == 0 ? toAdd : (prevAverage * prevAveraged + toAdd) / (prevAveraged + 1);
        }

        public static void WriteImageFile<T>(
            ChunkHolder<T> colorBuff,
            string fileName,
            Func<T, SKColor> transform)
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