// Much of this code is based on what is in
// https://www.davrous.com/2013/07/18/tutorial-part-6-learning-how-to-write-a-3d-software-engine-in-c-ts-or-js-texture-mapping-back-face-culling-webgl/

using MountainView.Base;
using System;
using System.IO;

namespace SoftEngine
{
    public class Texture : IDisposable
    {
        private readonly DirectBitmap bmp;
        private readonly int width;
        private readonly int height;

        public Texture(byte[] bits)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(bits, 0, bits.Length);
                ms.Position = 0;
                bmp = new DirectBitmap(ms);
            }

            width = bmp.Width;
            height = bmp.Height;
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (bmp != null)
                    {
                        bmp.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }

        internal void GetPixel(float u, float v, ref MyColor textureColor)
        {
            bmp.GetPixel((int)(u * (width - 1)), (int)(v * (height - 1)), ref textureColor);
        }
    }
}
