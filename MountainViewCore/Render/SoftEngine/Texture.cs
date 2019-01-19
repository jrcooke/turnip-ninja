// Much of this code is based on what is in
// https://www.davrous.com/2013/07/18/tutorial-part-6-learning-how-to-write-a-3d-software-engine-in-c-ts-or-js-texture-mapping-back-face-culling-webgl/

using MountainView.Base;
using System;

namespace SoftEngine
{
    public class Texture
    {
        public readonly DirectBitmap bmp;
        public readonly int width;
        public readonly int height;

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
    }
}
