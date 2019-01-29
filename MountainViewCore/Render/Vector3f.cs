using System;

namespace MountainView.Render
{
    public struct Vector3f
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3f(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public void Normalize()
        {
            float norm = (float)Math.Sqrt(X * X + Y * Y + Z * Z);
            if (norm < 1.0E-10)
            {
                X = 1.0f;
                Y = 0.0f;
                Z = 0.0f;
            }
            else
            {
                X /= norm;
                Y /= norm;
                Z /= norm;
            }
        }

        public override string ToString()
        {
            return "(" + X + "," + Y + "," + Z + ")";
        }

        public static void SubAndNorm(ref Vector3f a, ref Vector3f b, ref Vector3f result)
        {
            result.X = a.X - b.X;
            result.Y = a.Y - b.Y;
            result.Z = a.Z - b.Z;
            result.Normalize();
        }

        public float SqDistBetween(ref Vector3f b)
        {
            var dX = X - b.X;
            var dY = Y - b.Y;
            var dZ = Z - b.Z;
            return dX * dX + dY * dY + dZ * dZ;
        }

        public static void AvgAndNorm(ref Vector3f v1, ref Vector3f v2, ref Vector3f v3, ref Vector3f ret)
        {
            ret.X = (v1.X + v2.X + v3.X);
            ret.Y = (v1.Y + v2.Y + v3.Y);
            ret.Z = (v1.Z + v2.Z + v3.Z);
            ret.Normalize();
        }

        public static void AvgAndNorm(Vector3f[] vs, ref Vector3f ret)
        {
            ret.X = vs[0].X;
            ret.Y = vs[0].Y;
            ret.Z = vs[0].Z;

            for (int i = 1; i < vs.Length; i++)
            {
                ret.X += vs[i].X;
                ret.Y += vs[i].Y;
                ret.Z += vs[i].Z;
            }

            ret.Normalize();
        }

        /// <summary>
        /// Dot Product of two vectors.
        /// </summary>
        public static float Dot(ref Vector3f a, ref Vector3f b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        /// <summary>
        /// Cross Product of two vectors.
        /// </summary>
        public static void Cross(ref Vector3f a, ref Vector3f b, ref Vector3f result)
        {
            result.X = a.Y * b.Z - a.Z * b.Y;
            result.Y = a.Z * b.X - a.X * b.Z;
            result.Z = a.X * b.Y - a.Y * b.X;
        }
    }
}