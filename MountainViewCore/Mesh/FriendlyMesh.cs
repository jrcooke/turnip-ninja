using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MountainView.Mesh
{
    public class FriendlyMesh : ChunkMetadata
    {
        private const double RadToDeg = 180.0 / Math.PI;

        private Vector3d avgV;
        private double deltaV;
        private Vector3d[] origCorners;

        public Vector3d[] Vertices { get; private set; }
        public int[] TriangleIndices { get; private set; }
        public Vector3d[] VertexNormals { get; private set; }
        public Vector2d[] VertexToImage { get; private set; }
        public Vector3d[] Corners { get; private set; }

        public FriendlyMesh(
            Angle latLo, Angle lonLo,
            Angle latHi, Angle lonHi,
            int vertexCount, int triangleIndexCount)
            : base(-1, -1, latLo, lonLo, latHi, lonHi)
        {
            Vertices = new Vector3d[vertexCount];
            TriangleIndices = new int[triangleIndexCount];
            VertexNormals = new Vector3d[vertexCount];
            VertexToImage = new Vector2d[vertexCount];
            Corners = new Vector3d[4];
        }

        public FriendlyMesh(int latSteps, int lonSteps,
            Angle latLo, Angle lonLo,
            Angle latHi, Angle lonHi,
            float[][] heights)
            : base(latSteps, lonSteps, latLo, lonLo, latHi, lonHi)
        {
            GeoPolar3d buffGeoPolar = new GeoPolar3d();
            Vector3d[][] positions = Compute3dPositions(heights);
            origCorners = new Vector3d[]
            {
                positions[0][0],
                positions[0][positions.Length-1],
                positions[positions.Length-1][0],
                positions[positions.Length-1][positions.Length-1],
            };

            CenterAndScale(positions);
            ComplexMesh m = new ComplexMesh(positions);
            positions = null;
            Vertices = m.Vertices;
            TriangleIndices = m.TriangleIndices;
            VertexNormals = m.VertexNormals;
            RevertCenterAndScale();
            m = null;

            VertexToImage = new Vector2d[Vertices.Length];
            for (int i = 0; i < VertexToImage.Length; i++)
            {
                InvertTo(ref Vertices[i], ref VertexToImage[i], ref buffGeoPolar);
            }

            Corners = new Vector3d[origCorners.Length];
            for (int i = 0; i < Corners.Length; i++)
            {
                Corners[i] = origCorners[i];
            }
        }

        //------------------------------------------------

        private Vector3d[][] Compute3dPositions(float[][] heights, int reduction = 1)
        {
            Dictionary<int, Tuple<double, double>> latSinCoses = new Dictionary<int, Tuple<double, double>>();
            Dictionary<int, Tuple<double, double>> lonSinCoses = new Dictionary<int, Tuple<double, double>>();
            for (int i = 0; i < heights.Length; i++)
            {
                var latRad = Math.PI / 180 * (LatLo.DecimalDegree + i * LatDelta.DecimalDegree / (heights.Length - 1));
                latSinCoses.Add(i, new Tuple<double, double>(Math.Sin(latRad), Math.Cos(latRad)));

                var lonRad = Math.PI / 180 * (LonHi.DecimalDegree - i * LonDelta.DecimalDegree / (heights.Length - 1));
                lonSinCoses.Add(i, new Tuple<double, double>(Math.Sin(lonRad), Math.Cos(lonRad)));
            }

            int max = heights.Length / reduction;
            Vector3d[][] positions = new Vector3d[max][];
            for (int i = 0; i < max; i++)
            {
                positions[i] = new Vector3d[max];
                var latSinCos = latSinCoses[i * reduction];

                for (int j = 0; j < max; j++)
                {
                    var lonSinCos = lonSinCoses[j * reduction];
                    double height = heights[reduction * i][reduction * j] + Utils.AlphaMeters;
                    positions[i][j].X = height * latSinCos.Item2 * lonSinCos.Item2;
                    positions[i][j].Y = height * latSinCos.Item2 * lonSinCos.Item1;
                    positions[i][j].Z = height * latSinCos.Item1;
                }
            }

            return positions;
        }

        private void ForwardTo(ref GeoPolar3d polar, ref Vector3d cart)
        {
            double height = polar.Height + Utils.AlphaMeters;
            double cosLat = Math.Cos(polar.Lat / RadToDeg);
            double sinLat = Math.Sin(polar.Lat / RadToDeg);
            cart.X = height * cosLat * Math.Cos(polar.Lon / RadToDeg);
            cart.Y = height * cosLat * Math.Sin(polar.Lon / RadToDeg);
            cart.Z = height * sinLat;
        }

        private void InvertTo(ref Vector3d cart, ref Vector2d ret, ref GeoPolar3d polar)
        {
            InvertToFull(ref cart, ref polar);
            ret.X = (polar.Lon - LonLo.DecimalDegree) / LonDelta.DecimalDegree;
            ret.Y = 1.0 - (polar.Lat - LatLo.DecimalDegree) / LatDelta.DecimalDegree;
        }

        private void InvertToFull(ref Vector3d cart, ref GeoPolar3d polar)
        {
            var h = Math.Sqrt(cart.X * cart.X + cart.Y * cart.Y + cart.Z * cart.Z);
            var latSin = cart.Z / h;
            var hLatCos = h * Math.Sqrt(1.0 - latSin * latSin);
            var lon = Math.Asin(cart.Y / hLatCos) * RadToDeg;
            if (cart.X < 0.0)
            {
                lon = (lon > 0 ? 180 : -180) - lon;
            }

            var height = h - Utils.AlphaMeters;
            var LatDegrees = Math.Asin(latSin) * RadToDeg;
            var LonDegrees = lon;

            polar.Lat = LatDegrees;
            polar.Lon = LonDegrees;
            polar.Height = height;
        }

        private void CenterAndScale(Vector3d[][] positions)
        {
            avgV = new Vector3d(
                positions.SelectMany(p => p).Average(p => p.X),
                positions.SelectMany(p => p).Average(p => p.Y),
                positions.SelectMany(p => p).Average(p => p.Z));

            // Find the max dist between adjacent corners. This will the the characteristic length.
            var cornerDistsSq = new double[]
            {
                positions[0][0].DeltaSq(ref positions[0][positions.Length-1]),
                positions[0][0].DeltaSq(ref positions[positions.Length-1][0]),
                positions[positions.Length-1][positions.Length-1].DeltaSq(ref positions[0][positions.Length-1]),
                positions[positions.Length-1][positions.Length-1].DeltaSq(ref positions[positions.Length-1][0]),
            };

            deltaV = 10.0 / Math.Sqrt(cornerDistsSq.Max());

            for (int i = 0; i < positions.Length; i++)
            {
                for (int j = 0; j < positions.Length; j++)
                {
                    positions[i][j].X = (positions[i][j].X - avgV.X) * deltaV;
                    positions[i][j].Y = (positions[i][j].Y - avgV.Y) * deltaV;
                    positions[i][j].Z = (positions[i][j].Z - avgV.Z) * deltaV;
                }
            }
        }

        public void ExagerateHeight(double scale)
        {
            GeoPolar3d polar = new GeoPolar3d();
            for (int i = 0; i < Vertices.Length; i++)
            {
                InvertToFull(ref Vertices[i], ref polar);
                polar.Height *= scale;
                ForwardTo(ref polar, ref Vertices[i]);
            }
        }

        private void RevertCenterAndScale()
        {
            for (int i = 0; i < Vertices.Length; i++)
            {
                Vertices[i].X = Vertices[i].X / deltaV + avgV.X;
                Vertices[i].Y = Vertices[i].Y / deltaV + avgV.Y;
                Vertices[i].Z = Vertices[i].Z / deltaV + avgV.Z;
            }
        }
        public void CenterAndScale(double maxSideLength = 10.0, Vector3d center = new Vector3d())
        {
            avgV = new Vector3d(
                Vertices.Average(p => p.X),
                Vertices.Average(p => p.Y),
                Vertices.Average(p => p.Z));

            // Find the max dist between adjacent corners. This will the the characteristic length.
            var cornerDistsSq = new double[]
            {
                Corners[0].DeltaSq(ref Corners[1]),
                Corners[0].DeltaSq(ref Corners[2]),
                Corners[3].DeltaSq(ref Corners[1]),
                Corners[3].DeltaSq(ref Corners[2]),
            };

            deltaV = maxSideLength / Math.Sqrt(cornerDistsSq.Max());

            for (int i = 0; i < Vertices.Length; i++)
            {
                Vertices[i].X = (Vertices[i].X - avgV.X + center.X) * deltaV;
                Vertices[i].Y = (Vertices[i].Y - avgV.Y + center.Y) * deltaV;
                Vertices[i].Z = (Vertices[i].Z - avgV.Z + center.Z) * deltaV;
            }

            for (int i = 0; i < Corners.Length; i++)
            {
                Corners[i].X = (Corners[i].X - avgV.X + center.X) * deltaV;
                Corners[i].Y = (Corners[i].Y - avgV.Y + center.Y) * deltaV;
                Corners[i].Z = (Corners[i].Z - avgV.Z + center.Z) * deltaV;
            }
        }

        public void Match(FriendlyMesh mesh)
        {
            avgV = mesh.avgV;
            deltaV = mesh.deltaV;

            for (int i = 0; i < Vertices.Length; i++)
            {
                Vertices[i].X = (Vertices[i].X - avgV.X) * deltaV;
                Vertices[i].Y = (Vertices[i].Y - avgV.Y) * deltaV;
                Vertices[i].Z = (Vertices[i].Z - avgV.Z) * deltaV;
            }

            for (int i = 0; i < Corners.Length; i++)
            {
                Corners[i].X = (Corners[i].X - avgV.X) * deltaV;
                Corners[i].Y = (Corners[i].Y - avgV.Y) * deltaV;
                Corners[i].Z = (Corners[i].Z - avgV.Z) * deltaV;
            }
        }
    }
}
