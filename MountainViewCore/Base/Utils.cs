using FreeImageAPI;
using MountainView.ChunkManagement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MountainView.Base
{
    public enum OutputType
    {
        JPEG,
        Bitmap,
    }

    public static class Utils
    {
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

        public static MyColor GetColorForHeight(float a)
        {
            return new MyColor(
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
                bm.WriteFile(fileName, outputType);
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
                bm.WriteFile(fileName, outputType);
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
                bm.WriteFile(fileName, outputType);
            }
        }

        private class DirectBitmap : IDisposable
        {
            private readonly int width;
            private readonly int height;
            private byte[] bits;
            private bool disposed;
            private GCHandle bitsHandle;

            public DirectBitmap(int width, int height)
            {
                this.width = width;
                this.height = height;
                bits = new byte[width * height * 4];
                bitsHandle = GCHandle.Alloc(bits, GCHandleType.Pinned);

            }

            public void SetPixel(int i, int j, MyColor color)
            {
                int offset = 4 * ((height - 1 - j) * width + i);
                bits[offset++] = color.B;
                bits[offset++] = color.G;
                bits[offset++] = color.R;
                bits[offset++] = 255;
            }

            public void WriteFile(string fileName, OutputType outputType)
            {
                using (var bitmap = new FreeImageBitmap(width, height, width * 4, PixelFormat.Format32bppArgb, bitsHandle.AddrOfPinnedObject()))
                {
                    using (FileStream stream = File.OpenWrite(fileName))
                    {
                        switch (outputType)
                        {
                            case OutputType.JPEG:
                                // JPEG_QUALITYGOOD is 75 JPEG.
                                // JPEG_BASELINE strips metadata (EXIF, etc.)
                                bitmap.Save(stream, FREE_IMAGE_FORMAT.FIF_JPEG,
                                    FREE_IMAGE_SAVE_FLAGS.JPEG_QUALITYGOOD |
                                    FREE_IMAGE_SAVE_FLAGS.JPEG_BASELINE);
                                break;
                            case OutputType.Bitmap:
                                // JPEG_QUALITYGOOD is 75 JPEG.
                                // JPEG_BASELINE strips metadata (EXIF, etc.)
                                bitmap.Save(stream, FREE_IMAGE_FORMAT.FIF_BMP);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException("outputType");
                        }

                    }
                }
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                bitsHandle.Free();
            }
        }
    }
}