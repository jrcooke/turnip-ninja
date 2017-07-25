using System;

namespace MountainView.Base
{
    public struct Angle
    {
        public readonly static Angle Whole = new Angle() { Degrees = 360 };

        public bool IsNegative;
        public int Degrees;
        public int Minutes;
        public int Seconds;
        public int Thirds;
        public int Fourths;

        public double SignedDegrees
        {
            get
            {
                return (IsNegative ? -1 : 1) * Degrees;
            }
        }

        public double DecimalDegree
        {
            get
            {
                return Total / (60.0 * 60.0 * 60.0 * 60.0);
            }
        }

        public static Angle FromDecimalDegrees(double v)
        {
            double fourths = v * 60 * 60 * 60 * 60;
            return FromFourths((int)Math.Round(fourths));
        }

        public static Angle FromFourths(long totalFourths)
        {
            return FromTotal(totalFourths);
        }

        public static Angle FromThirds(long totalThirds)
        {
            return FromFourths(totalThirds * 60);
        }

        public static Angle FromSeconds(long totalSeconds)
        {
            return FromThirds(totalSeconds * 60);
        }

        public static Angle FromMinutes(long totalMinutes)
        {
            return FromSeconds(totalMinutes * 60);
        }

        private static Angle FromTotal(long totalFourths)
        {
            bool isNeg = totalFourths < 0;
            long abs = isNeg ? -totalFourths : totalFourths;
            var ret = new Angle()
            {
                IsNegative = isNeg,
                Fourths = (int)(abs % 60),
                Thirds = (int)((abs / (60)) % 60),
                Seconds = (int)((abs / (60 * 60)) % 60),
                Minutes = (int)((abs / (60 * 60 * 60) % 60)),
                Degrees = (int)((abs / (60 * 60 * 60 * 60))),
            };
            return ret;
        }

        private long Total
        {
            get
            {
                return (IsNegative ? -1 : 1) * ((((Degrees * 60L + Minutes) * 60 + Seconds) * 60 + Thirds) * 60 + Fourths);
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
            if (this.Fourths > 0)
            {
                return string.Format("{0:D3}D{1:D2}M{2:D2}S{3:D2}T{4:D2}F", this.Degrees, this.Minutes, this.Seconds, this.Thirds, this.Fourths);
            }
            if (this.Thirds > 0)
            {
                return string.Format("{0:D3}D{1:D2}M{2:D2}S{3:D2}T", this.Degrees, this.Minutes, this.Seconds, this.Thirds);
            }
            if (this.Seconds > 0)
            {
                return string.Format("{0:D3}D{1:D2}M{2:D2}S", this.Degrees, this.Minutes, this.Seconds);
            }
            if (this.Minutes > 0)
            {
                return string.Format("{0:D3}D{1:D2}M", this.Degrees, this.Minutes);
            }
            else
            {
                return string.Format("{0:D3}D", this.Degrees);
            }
        }
    }
}
