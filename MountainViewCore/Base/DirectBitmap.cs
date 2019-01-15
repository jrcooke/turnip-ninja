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

        public static DirectBitmap ReadFile(Stream stream)
        {
            DirectBitmap bmp;
            using (var bmp2 = new FreeImageBitmap(stream))
            {
                bmp = new DirectBitmap(bmp2.Width, bmp2.Height);
                for (int i = 0; i < bmp2.Width; i++)
                {
                    for (int j = 0; j < bmp2.Height; j++)
                    {
                        var oldColor = bmp2.GetPixel(i, j);
                        bmp.SetPixel(i, j, new MyColor(oldColor.R, oldColor.G, oldColor.B, oldColor.A));
                    }
                }
            }

            return bmp;
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
                        // JPEG_QUALITYGOOD is 75 JPEG.
                        // JPEG_BASELINE strips metadata (EXIF, etc.)
                        bitmap.Save(stream, FREE_IMAGE_FORMAT.FIF_PNG);
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

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            bitsHandle.Free();
        }
    }
}
