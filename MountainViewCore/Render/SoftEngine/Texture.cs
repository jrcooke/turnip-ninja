// Much of this code is based on what is in
// https://www.davrous.com/2013/07/18/tutorial-part-6-learning-how-to-write-a-3d-software-engine-in-c-ts-or-js-texture-mapping-back-face-culling-webgl/

using MountainView.Base;
using System;

namespace SoftEngine
{
    public class Texture
    {
        private readonly DirectBitmap bmp;
        private readonly int width;
        private readonly int height;

        public Texture(DirectBitmap bmp)
        {
            this.bmp = bmp;
            width = bmp.Width;
            height = bmp.Height;
        }

        public Texture(string filename)
        {
            using (var stream = System.IO.File.OpenRead(filename))
            {
                bmp = new DirectBitmap(stream);
                width = bmp.Width;
                height = bmp.Height;
            }
        }

        // Takes the U & V coordinates exported by Blender
        // and return the corresponding pixel color in the texture
        public MyColor Map(float tu, float tv, float scale)
        {
            // using a % operator to cycle/repeat the texture if needed
            int u = Math.Abs((int)(tu * width) % width);
            int v = Math.Abs((int)(tv * height) % height);

            var color = bmp.GetPixel(u, v);
            return color.Scale(scale);
        }
    }
}
