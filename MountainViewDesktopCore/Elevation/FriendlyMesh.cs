using MeshDecimator;
using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MountainViewDesktopCore.Elevation
{
    public struct Vector2d
    {
        public double X;
        public double Y;
    }

    public class FriendlyMesh
    {
        private const double RadToDeg = 180.0 / Math.PI;

        private StandardChunkMetadata template;
        private Vector3d avgV;
        private double deltaV;
        private Vector3d[] origCorners;

        public Vector3d[] Vertices { get; private set; }
        public int[] TriangleIndices { get; private set; }
        public Vector3d[] VertexNormals { get; private set; }
        public Vector2d[] VertexToImage { get; private set; }
        public Vector3d[] Corners { get; private set; }

        public FriendlyMesh(StandardChunkMetadata template, float[][] heights)
        {
            this.template = template;

            Vector3d[][] positions = Compute3dPositions(heights);
            origCorners = new Vector3d[]
            {
                positions[0][0],
                positions[0][positions.Length-1],
                positions[positions.Length-1][0],
                positions[positions.Length-1][positions.Length-1],
            };

            CenterAndScale(positions);
            Mesh m = new Mesh(positions);
            positions = null;
            Vertices = m.Vertices;
            TriangleIndices = m.TriangleIndices;
            VertexNormals = m.VertexNormals;
            RevertCenterAndScale();
            m = null;

            VertexToImage = new Vector2d[Vertices.Length];
            for (int i = 0; i < VertexToImage.Length; i++)
            {
                InvertTo(ref Vertices[i], ref VertexToImage[i]);
            }

            Corners = new Vector3d[origCorners.Length];
            for (int i = 0; i < Corners.Length; i++)
            {
                Corners[i] = origCorners[i];
            }
        }

        private Vector3d[][] Compute3dPositions(float[][] heights)
        {
            int max = heights.Length;

            Dictionary<int, Tuple<double, double>> latSinCoses = new Dictionary<int, Tuple<double, double>>();
            Dictionary<int, Tuple<double, double>> lonSinCoses = new Dictionary<int, Tuple<double, double>>();
            for (int i = 0; i < max; i++)
            {
                var latRad = Math.PI / 180 * (template.LatLo.DecimalDegree + i * template.LatDelta.DecimalDegree / max);
                latSinCoses.Add(i, new Tuple<double, double>(Math.Sin(latRad), Math.Cos(latRad)));

                var lonRad = Math.PI / 180 * (template.LonLo.DecimalDegree + i * template.LonDelta.DecimalDegree / max);
                lonSinCoses.Add(i, new Tuple<double, double>(Math.Sin(lonRad), Math.Cos(lonRad)));
            }

            Vector3d[][] positions = new Vector3d[max][];
            for (int i = 0; i < max; i++)
            {
                positions[i] = new Vector3d[max];
                var latSinCos = latSinCoses[i];

                for (int j = 0; j < max; j++)
                {
                    var lonSinCos = lonSinCoses[j];
                    double height = heights[j][max - 1 - i] + Utils.AlphaMeters;
                    positions[i][j].X = height * latSinCos.Item2 * lonSinCos.Item2;
                    positions[i][j].Y = height * latSinCos.Item2 * lonSinCos.Item1;
                    positions[i][j].Z = height * latSinCos.Item1;
                }
            }

            return positions;
        }

        private void InvertTo(ref Vector3d pRel, ref Vector2d ret)
        {
            var h = Math.Sqrt(pRel.X * pRel.X + pRel.Y * pRel.Y + pRel.Z * pRel.Z);
            var latSin = pRel.Z / h;
            var hLatCos = h * Math.Sqrt(1.0 - latSin * latSin);
            var lon = Math.Asin(pRel.Y / hLatCos) * RadToDeg;
            if (pRel.X < 0.0)
            {
                lon = (lon > 0 ? 180 : -180) - lon;
            }

            // var height = h - Utils.AlphaMeters;
            var LatDegrees = Math.Asin(latSin) * RadToDeg;
            var LonDegrees = lon;

            ret.X = (LonDegrees - template.LonLo.DecimalDegree) / template.LonDelta.DecimalDegree;
            ret.Y = 1.0 - (LatDegrees - template.LatLo.DecimalDegree) / template.LatDelta.DecimalDegree;
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

            for(int i = 0; i < Corners.Length; i++)
            {
                Corners[i].X = (Corners[i].X - avgV.X + center.X) * deltaV;
                Corners[i].Y = (Corners[i].Y - avgV.Y + center.Y) * deltaV;
                Corners[i].Z = (Corners[i].Z - avgV.Z + center.Z) * deltaV;
            }
        }
    }
}
