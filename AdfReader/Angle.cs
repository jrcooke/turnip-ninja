using System;

namespace AdfReader
{
    public struct Angle
    {
        public bool IsNegative;
        public int Degrees;
        public int Minutes;
        public int Seconds;
        public double DecimalDegree
        {
            get
            {
                return TotalSeconds / (60.0 * 60.0);
            }
        }

        public int TotalSeconds
        {
            get
            {
                return (IsNegative ? -1 : 1) * ((Degrees * 60 + Minutes) * 60 + Seconds);
            }
        }

        internal static Angle FromSeconds(int totalSeconds)
        {
            bool isNeg = totalSeconds < 0;
            int abs = isNeg ? -totalSeconds : totalSeconds;
            return new Angle()
            {
                IsNegative = isNeg,
                Seconds = abs % 60,
                Minutes = abs / 60 % 60,
                Degrees = abs / (60 * 60),
            };
        }

        internal static Angle FromDecimalDegrees(double v)
        {
            return FromSeconds((int)(v * 60 * 60));
        }

        internal static Angle Min(Angle a, Angle b)
        {
            return (a.TotalSeconds < b.TotalSeconds) ? a : b;
        }

        public string ToLatString()
        {
            return ToXString() + (IsNegative ? 's': 'n');
        }

        public string ToLonString()
        {
            return ToXString() + (IsNegative ? 'w': 'e');
        }

        internal static Angle Add(Angle a, double b)
        {
            return Angle.FromSeconds(a.TotalSeconds + (int)(b * 60 * 60));
        }

        public override string ToString()
        {
            return (IsNegative ? "-" : "") + ToXString();
        }

        public string ToXString()
        {
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
