using System;

namespace MountainViewDesktopCore.Mesh
{
    /// <summary>
    /// A double precision 3D vector.
    /// </summary>
    public struct Vector3d
    {
        public double X;
        public double Y;
        public double Z;

        /// <summary>
        /// Creates a new vector.
        /// </summary>
        public Vector3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Gets a normalized vector from this vector.
        /// </summary>
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
    }
}
