// Much of this code is based on what is in
// https://www.davrous.com/2013/07/18/tutorial-part-6-learning-how-to-write-a-3d-software-engine-in-c-ts-or-js-texture-mapping-back-face-culling-webgl/

using MountainView.Base;
using System;

namespace SoftEngine
{
    public class Texture
    {
        private readonly byte[] internalBuffer;
        private readonly int width;
        private readonly int height;

        public Texture(string filename)
        {
            using (var stream = System.IO.File.OpenRead(filename))
            {
                var bmp = DirectBitmap.ReadFile(stream);
                width = bmp.Width;
                height = bmp.Height;
                internalBuffer = bmp.PixelBuffer;
            }
        }

        // Takes the U & V coordinates exported by Blender
        // and return the corresponding pixel color in the texture
        public MyColor Map(float tu, float tv, float scale)
        {
            // using a % operator to cycle/repeat the texture if needed
            int u = Math.Abs((int)(tu * width) % width);
            int v = Math.Abs((int)(tv * height) % height);

            int pos = (u + v * width) * 4;
            byte b = internalBuffer[pos++];
            byte g = internalBuffer[pos++];
            byte r = internalBuffer[pos++];
            byte a = internalBuffer[pos++];

            return new MyColor(
                (byte)(r * scale),
                (byte)(g * scale),
                (byte)(b * scale),
                (byte)(a * scale));
        }
    }
}
