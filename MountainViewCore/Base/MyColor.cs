namespace MountainView.Base
{
    public struct MyColor
    {
        public static readonly MyColor White = new MyColor(255, 255, 255, 255);
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;
        public readonly byte A;

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

        public MyColor Scale(float scale)
        {
            return new MyColor(
                (byte)(R * scale),
                (byte)(G * scale),
                (byte)(B * scale),
                (byte)(A * scale));
        }

        public override string ToString()
        {
            return "(" + R + "," + G + "," + B + "," + A + ")";
        }
    }
}
