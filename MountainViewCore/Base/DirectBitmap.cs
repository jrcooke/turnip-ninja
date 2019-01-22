using FreeImageAPI;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace MountainView.Base
{
    public class DirectBitmap : IDisposable
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public byte[] PixelBuffer { get; private set; }

        private bool disposed;
        private GCHandle bitsHandle;
        private IntPtr arrayPtr;

        public DirectBitmap(int width, int height)
        {
            Width = width;
            Height = height;
            PixelBuffer = new byte[4 * width * height];
            bitsHandle = GCHandle.Alloc(PixelBuffer, GCHandleType.Pinned);
            arrayPtr = bitsHandle.AddrOfPinnedObject();
        }

        public DirectBitmap(Stream stream)
        {
            using (var srcBitmap = new FreeImageBitmap(stream))
            {
                Width = srcBitmap.Width;
                Height = srcBitmap.Height;
                PixelBuffer = new byte[4 * Width * Height];
                bitsHandle = GCHandle.Alloc(PixelBuffer, GCHandleType.Pinned);
                arrayPtr = bitsHandle.AddrOfPinnedObject();

                var srcBits = srcBitmap.Bits;
                unsafe
                {
                    byte* dst = (byte*)arrayPtr.ToPointer();
                    for (int y = 0; y < Height; y++)
                    {
                        byte* src1 = (byte*)srcBits.ToPointer() + srcBitmap.Pitch * (Height - y - 1);
                        for (int x = 0; x < Width; x++)
                        {
                            *dst++ = *src1++;
                            *dst++ = *src1++;
                            *dst++ = *src1++;
                            *dst++ = 255;
                        }
                    }
                }
            }
        }

        public void SetPixel(int i, int j, MyColor color)
        {
            unsafe
            {
                byte* dst = (byte*)arrayPtr.ToPointer();
                dst += 4 * ((Height - 1 - j) * Width + i);
                *dst++ = color.B;
                *dst++ = color.G;
                *dst++ = color.R;
                *dst++ = color.A;
            }
        }

        internal void SetAllPixels(MyColor color)
        {
            unsafe
            {
                byte* dst = (byte*)arrayPtr.ToPointer();
                for (int i = 0; i <= Width * Height; i++)
                {
                    *dst++ = color.B;
                    *dst++ = color.G;
                    *dst++ = color.R;
                    *dst++ = color.A;
                }
            }
        }

        internal void GetPixel(int x, int y, ref MyColor color)
        {
            unsafe
            {
                int pos = (x + y * Width) * 4;
                color.B = PixelBuffer[pos++];
                color.G = PixelBuffer[pos++];
                color.R = PixelBuffer[pos++];
                color.A = PixelBuffer[pos++];
            }
        }

        public void WriteFile(OutputType outputType, Stream stream)
        {
            using (var bitmap = new FreeImageBitmap(Width, Height, Width * 4, PixelFormat.Format32bppArgb, bitsHandle.AddrOfPinnedObject()))
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
                    case OutputType.PNG:
                        bitmap.Save(stream, FREE_IMAGE_FORMAT.FIF_PNG);
                        break;
                    case OutputType.Bitmap:
                        bitmap.Save(stream, FREE_IMAGE_FORMAT.FIF_BMP);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("outputType");
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            bitsHandle.Free();
        }

        public void DrawOn(DirectBitmap chunkBmp)
        {
            if (Width != chunkBmp.Width || Height != chunkBmp.Height)
            {
                throw new InvalidOperationException("Bitmaps must be same size");
            }

            unsafe
            {
                byte* dst = (byte*)arrayPtr.ToPointer();
                byte* toAdd = (byte*)chunkBmp.arrayPtr.ToPointer();
                for (int i = 0; i <= Width * Height; i++)
                {
                    byte B0 = *toAdd++;
                    byte G0 = *toAdd++;
                    byte R0 = *toAdd++;
                    double A0 = (*toAdd++) / 255.0;

                    byte B1 = *dst++;
                    byte G1 = *dst++;
                    byte R1 = *dst++;
                    double A1 = (*dst++) / 255.0;

                    double A01 = (1 - A0) * A1 + A0;
                    double R01 = A01 == 0.0 ? 0.0 : ((1 - A0) * A1 * R1 + A0 * R0) / A01;
                    double G01 = A01 == 0.0 ? 0.0 : ((1 - A0) * A1 * G1 + A0 * G0) / A01;
                    double B01 = A01 == 0.0 ? 0.0 : ((1 - A0) * A1 * B1 + A0 * B0) / A01;

                    dst -= 4;
                    *dst++ = (byte)B01;
                    *dst++ = (byte)G01;
                    *dst++ = (byte)R01;
                    *dst++ = (byte)(A01 * 255);
                }
            }
        }
    }
}
