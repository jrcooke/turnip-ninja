using System;

namespace MountainView.Mesh
{
    public struct Vector3d
    {
        public double X;
        public double Y;
        public double Z;

        public Vector3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public void Normalize()
        {
            double norm = Math.Sqrt(X * X + Y * Y + Z * Z);
            X /= norm;
            Y /= norm;
            Z /= norm;
        }

        public double DeltaSq(ref Vector3d a)
        {
            double dx = X - a.X;
            double dy = Y - a.Y;
            double dz = Z - a.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        public override string ToString()
        {
            return "(" + X + "," + Y + "," + Z + ")";
        }

        public static void Average(ref Vector3d a, ref Vector3d b, ref Vector3d result)
        {
            result.X = (a.X + b.X) * 0.5;
            result.Y = (a.Y + b.Y) * 0.5;
            result.Z = (a.Z + b.Z) * 0.5;
        }

        public static void Sub(ref Vector3d a, ref Vector3d b, ref Vector3d result)
        {
            result.X = a.X - b.X;
            result.Y = a.Y - b.Y;
            result.Z = a.Z - b.Z;
        }

        /// <summary>
        /// Dot Product of two vectors.
        /// </summary>
        public static double Dot(ref Vector3d a, ref Vector3d b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        /// <summary>
        /// Cross Product of two vectors.
        /// </summary>
        public static void Cross(ref Vector3d a, ref Vector3d b, ref Vector3d result)
        {
            result.X = a.Y * b.Z - a.Z * b.Y;
            result.Y = a.Z * b.X - a.X * b.Z;
            result.Z = a.X * b.Y - a.Y * b.X;
        }

        public static void DoCrossAndDots(ref Vector3d tirNorm, ref Vector3d p, ref Vector3d v1, ref Vector3d v2, out double dot1, out double dot2)
        {
            double d1X = v1.X - p.X;
            double d1Y = v1.Y - p.Y;
            double d1Z = v1.Z - p.Z;

            double d2X = v2.X - p.X;
            double d2Y = v2.Y - p.Y;
            double d2Z = v2.Z - p.Z;

            // Do the cross product
            double nX = d1Y * d2Z - d1Z * d2Y;
            double nY = d1Z * d2X - d1X * d2Z;
            double nZ = d1X * d2Y - d1Y * d2X;

            // Bulk normalize
            double norm1 = Math.Sqrt(d1X * d1X + d1Y * d1Y + d1Z * d1Z);
            double norm2 = Math.Sqrt(d2X * d2X + d2Y * d2Y + d2Z * d2Z);
            double normN = Math.Sqrt(nX * nX + nY * nY + nZ * nZ);

            dot1 = (d1X * d2X + d1Y * d2Y + d1Z * d2Z) / (norm1 * norm2);
            dot2 = (nX * tirNorm.X + nY * tirNorm.Y + nZ * tirNorm.Z) / (normN);
        }

        internal static void WeightedAverage(ref Vector3d a, ref Vector3d b, int aWeight, int bWeight)
        {
            a.X = (a.X * aWeight + b.X * bWeight) / (aWeight + bWeight);
            a.Y = (a.Y * aWeight + b.Y * bWeight) / (aWeight + bWeight);
            a.Z = (a.Z * aWeight + b.Z * bWeight) / (aWeight + bWeight);
        }

        internal Vector3d Mult(double v)
        {
            return new Vector3d(X * v, Y * v, Z * v);
        }

        internal Vector3d Add(Vector3d v)
        {
            return new Vector3d(X + v.X, Y + v.Y, Z + v.Z);
        }

        public Vector3d Mult(Vector3d v)
        {
            return new Vector3d(X * v.X, Y * v.Y, Z * v.Z);
        }

        internal double Length()
        {
            return Math.Sqrt(X * X + Y * Y + Z * Z);
        }
    }
}
