using System;
using System.Diagnostics;

namespace MountainView
{
    public struct Angle
    {
        public bool IsNegative;
        public int Degrees;
        public int Minutes;
        public int Seconds;
        public int Thirds;

        public double DecimalDegree
        {
            get
            {
                return TotalThirds / (60.0 * 60.0 * 60.0);
            }
        }

        public int TotalSeconds
        {
            get
            {
                return (IsNegative ? -1 : 1) * ((Degrees * 60 + Minutes) * 60 + Seconds);
            }
        }

        public int TotalThirds
        {
            get
            {
                return (IsNegative ? -1 : 1) * (((Degrees * 60 + Minutes) * 60 + Seconds) * 60 + Thirds);
            }
        }

        internal static Angle FromThirds(int totalThirds)
        {
            bool isNeg = totalThirds < 0;
            int abs = isNeg ? -totalThirds : totalThirds;
            var ret= new Angle()
            {
                IsNegative = isNeg,
                Thirds = abs % 60,
                Seconds = (abs / (60)) % 60,
                Minutes = (abs / (60 * 60)) % 60,
                Degrees = (abs / (60 * 60 * 60)),
            };
            return ret;
        }

        internal static Angle FromSeconds(int totalSeconds)
        {
            return FromThirds(totalSeconds * 60);
        }

        internal static Angle FromMinutes(int totalMinutes)
        {
            return FromThirds(totalMinutes * 60 * 60);
        }

        internal static Angle FromDecimalDegrees(double v)
        {
            double thirds = v * 60 * 60 * 60;
            thirds = Math.Round(thirds);

            return FromThirds((int)thirds);
        }

        internal static Angle Min(Angle a, Angle b)
        {
            return (a.TotalThirds < b.TotalThirds) ? a : b;
        }

        internal static Angle Multiply(Angle a, int b)
        {
            return Angle.FromThirds(a.TotalThirds * b);
        }

        internal static int Divide(Angle a, Angle b)
        {
            var remainder = a.TotalThirds % b.TotalThirds;
            Debug.WriteLine(remainder);
            return a.TotalThirds / b.TotalThirds;
        }

        internal static Angle Divide(Angle a, int b)
        {
            var remainder = a.TotalThirds % b;
            Debug.WriteLine(remainder);
            return Angle.FromThirds(a.TotalThirds / b);
        }

        internal static Angle Subtract(Angle a, Angle b)
        {
            return Angle.FromThirds(a.TotalThirds - b.TotalThirds);
        }

        internal static Angle Add(Angle a, double b)
        {
            return Angle.FromThirds(a.TotalThirds + (int)(b * 60 * 60 * 60));
        }

        internal static Angle Add(Angle a, Angle b)
        {
            return Angle.FromThirds(a.TotalThirds + b.TotalThirds);
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

        public string ToXString()
        {
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
