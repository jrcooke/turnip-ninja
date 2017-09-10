using System;

namespace MountainView.Base
{
    public struct Angle
    {
        public readonly static Angle Whole = new Angle() { Abs = 360L * 60 * 60 * 60 * 60, DecimalDegree = 360 };

        public bool IsNegative;
        public long Abs;
        public double DecimalDegree;

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
            return FromFourths((int)Math.Round(fourths));
        }

        public static Angle FromThirds(long totalThirds)
        {
            return FromFourths(totalThirds * 60);
        }

        private static Angle FromFourths(long totalFourths)
        {
            bool isNeg = totalFourths < 0;
            return new Angle()
            {
                IsNegative = isNeg,
                Abs = isNeg ? -totalFourths : totalFourths,
                DecimalDegree = totalFourths / (60.0 * 60.0 * 60.0 * 60.0),
            };
        }

        public long Fourths
        {
            get
            {
                return (IsNegative ? -1 : 1) * Abs;
            }
        }

        public static Angle Multiply(Angle a, int b)
        {
            return Angle.FromFourths(a.Fourths * b);
        }

        public static Angle Divide(Angle a, int b)
        {
            return Angle.FromFourths(a.Fourths / b);
        }

        public static int FloorDivide(Angle a, Angle b)
        {
            if (!a.IsNegative)
            {
                return (int)(a.Fourths / b.Fourths);
            }
            else
            {
                return -1 - (int)((-a.Fourths) / b.Fourths);
            }
        }

        internal static Angle Subtract(Angle a, Angle b)
        {
            return Angle.FromFourths(a.Fourths - b.Fourths);
        }

        public static Angle Add(Angle a, Angle b)
        {
            return Angle.FromFourths(a.Fourths + b.Fourths);
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
            int fourths = (int)(Abs % 60);
            int thirds = (int)((Abs / (60)) % 60);
            int seconds = (int)((Abs / (60 * 60)) % 60);
            int minutes = (int)((Abs / (60 * 60 * 60) % 60));
            int degrees = (int)((Abs / (60 * 60 * 60 * 60)));

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

        internal Angle Truncate()
        {
            return Angle.FromThirds((this.Fourths / 60 / 60 / 60) * 60 * 60);
        }
    }
}
