namespace MountainView.Base
{
    public struct MyColor
    {
        public byte R;// { get; private set; }
        public byte G;// { get; private set; }
        public byte B;// { get; private set; }
        public byte A;// { get; private set; }

        public MyColor(float x)
        {
            R = (byte)(255 * x);
            G = R;
            B = R;
            A = R;
        }

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

        public override string ToString()
        {
            return "(" + R + "," + G + "," + B + "," + A + ")";
        }
    }
}
