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
    }
}
