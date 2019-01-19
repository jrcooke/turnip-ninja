using System;

namespace MountainView.Base
{
    public struct MyColor
    {
        public static readonly MyColor White = new MyColor(255, 255, 255, 255);
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public MyColor(byte r, byte g, byte b) : this(r, g, b, 255)
        {
        }

        public MyColor(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        internal void WhiteScale(float scale)
        {
            R = (byte)(255 * scale);
            G = (byte)(255 * scale);
            B = (byte)(255 * scale);
            A = (byte)(255 * scale);
        }

        public void ScaleSelf(float scale)
        {
            R = (byte)(R * scale);
            G = (byte)(G * scale);
            B = (byte)(B * scale);
            A = (byte)(A * scale);
        }

        public override string ToString()
        {
            return "(" + R + "," + G + "," + B + "," + A + ")";
        }
    }
}
