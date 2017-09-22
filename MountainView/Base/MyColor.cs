using SkiaSharp;

namespace MountainView.Base
{
    public struct MyColor
    {
        public byte R { get; private set; }
        public byte G { get; private set; }
        public byte B { get; private set; }

        public MyColor(byte red, byte green, byte blue)
        {
            R = red;
            G = green;
            B = blue;
        }

        public MyColor(SKColor color) : this(color.Red, color.Green, color.Blue)
        {
        }
    }
}
