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
            return FromFourths((long)Math.Round(fourths));
        }

        public static Angle FromMinutes(long totalDegrees)
        {
            return FromSeconds(totalDegrees * 60);
        }

        public static Angle FromSeconds(long totalSeconds)
        {
            return FromThirds(totalSeconds * 60);
        }

        public static Angle FromThirds(long totalThirds)
        {
            return FromFourths(totalThirds * 60);
        }

        public static Angle FromFourths(long totalFourths)
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

        public static Angle Parse(string v)
        {
            bool isNegative = false;
            if (v[0] == '-')
            {
                isNegative = true;
                v = v.Substring(1);
            }
            else
            {
                var last = v[v.Length - 1];
                if (last == 's' || last == 'w')
                {
                    isNegative = true;
                    v = v.Substring(0, v.Length - 1);
                }
                else if (last == 'n' || last == 'e')
                {
                    v = v.Substring(0, v.Length - 1);
                }
            }

            var parts = v.Split(new char[] { 'D', 'M', 'S', 'T', 'F' }, StringSplitOptions.RemoveEmptyEntries);
            return Angle.FromFourths((isNegative ? -1 : 1) * (
                NewMethod(parts, 4) + 60L * (
                NewMethod(parts, 3) + 60L * (
                NewMethod(parts, 2) + 60L * (
                NewMethod(parts, 1) + 60L * (
                NewMethod(parts, 0)))))));
        }

        private static int NewMethod(string[] parts, int i)
        {
            return parts.Length > i ? int.Parse(parts[i]) : 0;
        }

        internal Angle Truncate()
        {
            return Angle.FromThirds((this.Fourths / 60 / 60 / 60) * 60 * 60);
        }
    }
}
