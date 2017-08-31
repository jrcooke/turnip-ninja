using System;

namespace MountainView.Base
{
    public struct Angle
    {
        public readonly static Angle Whole = new Angle() { abs = 360L * 60 * 60 * 60 * 60 };

        private bool IsNegative;
        private long abs;

        public double SignedDegrees
        {
            get
            {
                return (IsNegative ? -1 : 1) * DecimalDegree;
            }
        }

        public double DecimalDegree
        {
            get
            {
                return Total / (60.0 * 60.0 * 60.0 * 60.0);
            }
        }

        public double Radians
        {
            get
            {
                return DecimalDegree * Math.PI / 180.0;
            }
        }

        public static Angle FromDecimalDegrees(double v)
        {
            double fourths = v * 60 * 60 * 60 * 60;
            return FromTotal((int)Math.Round(fourths));
        }

        public static Angle FromThirds(long totalThirds)
        {
            return FromTotal(totalThirds * 60);
        }

        private static Angle FromTotal(long totalFourths)
        {
            bool isNeg = totalFourths < 0;
            return new Angle()
            {
                IsNegative = isNeg,
                abs = isNeg ? -totalFourths : totalFourths,
            };
        }

        internal static void Swap(ref Angle a, ref Angle b)
        {
            var tmp = a;
            a = b;
            b = tmp;
        }

        private long Total
        {
            get
            {
                return (IsNegative ? -1 : 1) * abs;
            }
        }

        public static Angle Min(Angle a, Angle b)
        {
            return (a.Total < b.Total) ? a : b;
        }

        public static Angle Multiply(Angle a, int b)
        {
            return Angle.FromTotal(a.Total * b);
        }

        public static Angle Multiply(Angle a, double b)
        {
            return Angle.FromTotal((int)(a.Total * b));
        }

        public static int Divide(Angle a, Angle b)
        {
            return (int)(a.Total / b.Total);
        }

        public static Angle Divide(Angle a, int b)
        {
            return Angle.FromTotal(a.Total / b);
        }

        public static Angle Divide(Angle a, double b)
        {
            return Angle.FromTotal((int)(a.Total / b));
        }

        public static int FloorDivide(Angle a, Angle b)
        {
            if (!a.IsNegative)
            {
                return (int)(a.Total / b.Total);
            }
            else
            {
                return -1 - (int)((-a.Total) / b.Total);
            }
        }

        internal static Angle Subtract(Angle a, Angle b)
        {
            return Angle.FromTotal(a.Total - b.Total);
        }

        public static Angle Add(Angle a, double b)
        {
            return Angle.FromTotal(a.Total + FromDecimalDegrees(b).Total);
        }

        public static Angle Add(Angle a, Angle b)
        {
            return Angle.FromTotal(a.Total + b.Total);
        }

        public string ToLatString()
        {
            return ToXString() + (IsNegative ? 's' : 'n');
        }

        public string ToLonString()
        {
            return ToXString() + (IsNegative ? 'w' : 'e');
        }

        public override string ToString()
        {
            return (IsNegative ? "-" : "") + ToXString();
        }

        private string ToXString()
        {
            int fourths = (int)(abs % 60);
            int thirds = (int)((abs / (60)) % 60);
            int seconds = (int)((abs / (60 * 60)) % 60);
            int minutes = (int)((abs / (60 * 60 * 60) % 60));
            int degrees = (int)((abs / (60 * 60 * 60 * 60)));

            if (fourths > 0)
            {
                return string.Format("{0:D3}D{1:D2}M{2:D2}S{3:D2}T{4:D2}F", degrees, minutes, seconds, thirds, fourths);
            }
            if (thirds > 0)
            {
                return string.Format("{0:D3}D{1:D2}M{2:D2}S{3:D2}T", degrees, minutes, seconds, thirds);
            }
            if (seconds > 0)
            {
                return string.Format("{0:D3}D{1:D2}M{2:D2}S", degrees, minutes, seconds);
            }
            if (minutes > 0)
            {
                return string.Format("{0:D3}D{1:D2}M", degrees, minutes);
            }
            else
            {
                return string.Format("{0:D3}D", degrees);
            }
        }
    }
}
