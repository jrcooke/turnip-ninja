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
            A = 255;
        }

        public void ScaleSelf(float scale)
        {
            R = (byte)(R * scale);
            G = (byte)(G * scale);
            B = (byte)(B * scale);
        }

        public override string ToString()
        {
            return "(" + R + "," + G + "," + B + "," + A + ")";
        }
    }
    public struct MyDColor
    {
        public double R;
        public double G;
        public double B;

        public MyColor ToMyColor()
        {
            return new MyColor(
                (byte)(R),
                (byte)(G),
                (byte)(B),
                255);
        }

        public override string ToString()
        {
            return "(" + R + "," + G + "," + B + ")";
        }

        internal MyDColor Mult(double a)
        {
            return new MyDColor()
            {
                R = R * a,
                G = G * a,
                B = B * a,
            };
        }

        internal MyDColor Mult(MyDColor a)
        {
            return new MyDColor()
            {
                R = R * a.R,
                G = G * a.G,
                B = B * a.B,
            };
        }

        internal MyDColor Add(MyDColor a)
        {
            return new MyDColor()
            {
                R = R + a.R,
                G = G + a.G,
                B = B + a.B,
            };
        }

        internal MyDColor Add(double a)
        {
            return new MyDColor()
            {
                R = R + a,
                G = G + a,
                B = B + a,
            };
        }
    }
}
