using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MountainView.Mesh
{
    public class FriendlyMesh : ChunkMetadata
    {
        private const double RadToDeg = 180.0 / Math.PI;

        private Vector3d[] origCorners;

        public Vector3d[] Vertices { get; private set; }
        public int[] TriangleIndices { get; private set; }
        public int[] EdgeIndices { get; private set; }
        public Vector3d[] VertexNormals { get; private set; }
        public Vector2d[] VertexToImage { get; private set; }
        public Vector3d[] Corners { get; private set; }
        public byte[] ImageData { get; set; }

        public FriendlyMesh(
            Angle latLo, Angle lonLo,
            Angle latHi, Angle lonHi,
            int vertexCount, int triangleIndexCount, int edgeIndicesCount)
            : base(-1, -1, latLo, lonLo, latHi, lonHi)
        {
            Vertices = new Vector3d[vertexCount];
            TriangleIndices = new int[triangleIndexCount];
            EdgeIndices = new int[edgeIndicesCount];
            VertexNormals = new Vector3d[vertexCount];
            VertexToImage = new Vector2d[vertexCount];
            Corners = new Vector3d[4];
        }

        public FriendlyMesh(int latSteps, int lonSteps,
            Angle latLo, Angle lonLo,
            Angle latHi, Angle lonHi,
            float[][] heights,
            TraceListener log)
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

            var norm = CenterAndScale(positions);
            ComplexMesh m = new ComplexMesh(positions, log);
            positions = null;
            Vertices = m.Vertices;
            TriangleIndices = m.TriangleIndices;
            EdgeIndices = m.EdgeIndices;
            VertexNormals = m.VertexNormals;
            RevertCenterAndScale(norm);
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

        public void SimplifyMesh(double threshold, TraceListener log, bool verbose)
        {
            var edgePoints = EdgeIndices.Select(p => Vertices[p]).ToArray();
            double fudgeSq = double.MaxValue;
            for (int i = 1; i < edgePoints.Length; i++)
            {
                var d = edgePoints[i].DeltaSq(ref edgePoints[i - 1]);
                if (d > 1.0E-10)
                {
                    fudgeSq = Math.Min(fudgeSq, d);
                }
            }

            fudgeSq /= 100.0;
            var mdFinal = new SimplifyMesh(log, Vertices, TriangleIndices, EdgeIndices, verbose);
            mdFinal.SimplifyMeshByThreshold(threshold);
            Vertices = mdFinal.GetVertices();
            TriangleIndices = mdFinal.GetIndices();
            VertexNormals = mdFinal.GetVertexNormals();
            EdgeIndices = mdFinal.GetEdgeIndices();
            mdFinal = null;

            VertexToImage = new Vector2d[Vertices.Length];
            GeoPolar3d buffGeoPolar = new GeoPolar3d();
            for (int i = 0; i < VertexToImage.Length; i++)
            {
                InvertTo(ref Vertices[i], ref VertexToImage[i], ref buffGeoPolar);
            }
        }

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


        private void RotatePointToNormal(
            double cosLat, double sinLat,
            double cosLon, double sinLon, ref Vector3d cart)
        {
            // First, rotate back lon. The cylinder, so z doesn't change
            double x1 = +cosLon * cart.X + sinLon * cart.Y;
            double y1 = -sinLon * cart.X + cosLon * cart.Y;
            double z1 = cart.Z;

            // Then rotate back lat. Rotate along y axis
            cart.X = +cosLat * x1 + sinLat * z1;
            cart.Y = y1;
            cart.Z = -sinLat * x1 + cosLat * z1;
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

        private NormalizeSettingsBasic CenterAndScale(Vector3d[][] positions)
        {
            var avgV = new Vector3d(
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

            var deltaV = 10.0 / Math.Sqrt(cornerDistsSq.Max());

            for (int i = 0; i < positions.Length; i++)
            {
                for (int j = 0; j < positions.Length; j++)
                {
                    positions[i][j].X = (positions[i][j].X - avgV.X) * deltaV;
                    positions[i][j].Y = (positions[i][j].Y - avgV.Y) * deltaV;
                    positions[i][j].Z = (positions[i][j].Z - avgV.Z) * deltaV;
                }
            }

            return new NormalizeSettingsBasic(deltaV, avgV);
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

        private void RevertCenterAndScale(NormalizeSettingsBasic norm)
        {
            for (int i = 0; i < Vertices.Length; i++)
            {
                Vertices[i].X = Vertices[i].X / norm.DeltaV + norm.AvgV.X;
                Vertices[i].Y = Vertices[i].Y / norm.DeltaV + norm.AvgV.Y;
                Vertices[i].Z = Vertices[i].Z / norm.DeltaV + norm.AvgV.Z;
            }
        }

        public NormalizeSettings GetCenterAndScale(double lat, double lon, double scale, double latDelta, double elevation, TraceListener log)
        {
            double cosLat = Math.Cos(lat / RadToDeg);
            double sinLat = Math.Sin(lat / RadToDeg);
            double cosLon = Math.Cos(lon / RadToDeg);
            double sinLon = Math.Sin(lon / RadToDeg);
            Vector3d avgV = new Vector3d();

            // Find the triangle that contains lat lon
            // First transform all points to ones centered on lat lon
            Vector3d[] xformed = new Vector3d[Vertices.Length];
            for (int i = 0; i < Vertices.Length; i++)
            {
                xformed[i] = Vertices[i];
                RotatePointToNormal(cosLat, sinLat, cosLon, sinLon, ref xformed[i]);
            }

            // Look find the triangles that contain that contain the origin
            for (int i = 0; i < TriangleIndices.Length; i += 3)
            {
                var pointH = TriangleContainsOrigin(xformed, i);
                if (pointH.HasValue)
                {
                    GeoPolar3d polar = new GeoPolar3d(lat, lon, pointH.Value);
                    ForwardTo(ref polar, ref avgV);
                    // Interpolate the height at the origin.
                    log?.WriteLine(avgV);
                }
            }

            RotatePointToNormal(cosLat, sinLat, cosLon, sinLon, ref avgV);

            var norm = new NormalizeSettings(cosLat, sinLat, cosLon, sinLon, avgV, elevation);
            return norm;
        }

        // https://stackoverflow.com/questions/2049582/how-to-determine-if-a-point-is-in-a-2d-triangle
        private double Sign(Vector3d p2, Vector3d p3)
        {
            return (-p3.Y) * (p2.Z - p3.Z) - (p2.Y - p3.Y) * (-p3.Z);
        }

        private double? TriangleContainsOrigin(Vector3d[] xformed, int i)
        {
            Vector3d v1 = xformed[TriangleIndices[i + 0]];
            Vector3d v2 = xformed[TriangleIndices[i + 1]];
            Vector3d v3 = xformed[TriangleIndices[i + 2]];

            double d1 = Sign(v1, v2);
            double d2 = Sign(v2, v3);
            double d3 = Sign(v3, v1);

            bool has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            if (!(has_neg && has_pos))
            {
                // Contains origin!
                List<double> hs = new List<double>();
                List<double> zs = new List<double>();
                double grad = 0.0;
                if (v1.Y * v2.Y < 0.0)
                {
                    grad = -v2.Y / (v1.Y - v2.Y);
                    hs.Add(v1.X * grad + v2.X * (1 - grad));
                    zs.Add(v1.Z * grad + v2.Z * (1 - grad));
                }

                if (v1.Y * v3.Y < 0.0)
                {
                    grad = -v3.Y / (v1.Y - v3.Y);
                    hs.Add(v1.X * grad + v3.X * (1 - grad));
                    zs.Add(v1.Z * grad + v3.Z * (1 - grad));
                }

                if (v2.Y * v3.Y < 0.0)
                {
                    grad = -v3.Y / (v2.Y - v3.Y);
                    hs.Add(v2.X * grad + v3.X * (1 - grad));
                    zs.Add(v2.Z * grad + v3.Z * (1 - grad));
                }

                grad = zs[1] / (zs[0] - zs[1]);
                var h = hs[0] * grad + hs[1] * (1 - grad);

                return h - Utils.AlphaMeters;
            }

            return null;
        }

        public void Match(NormalizeSettings norm)
        {
            // MatchWorker
            for (int i = 0; i < Vertices.Length; i++)
            {
                RotatePointToNormal(norm.CosLat, norm.SinLat, norm.CosLon, norm.SinLon, ref Vertices[i]);
                Vertices[i].X = (Vertices[i].X - norm.AvgV.X);
                Vertices[i].Y = (Vertices[i].Y - norm.AvgV.Y);
                Vertices[i].Z = (Vertices[i].Z - norm.AvgV.Z);
            }

            for (int i = 0; i < Corners.Length; i++)
            {
                RotatePointToNormal(norm.CosLat, norm.SinLat, norm.CosLon, norm.SinLon, ref Corners[i]);
                Corners[i].X = (Corners[i].X - norm.AvgV.X);
                Corners[i].Y = (Corners[i].Y - norm.AvgV.Y);
                Corners[i].Z = (Corners[i].Z - norm.AvgV.Z);
            }

            for (int i = 0; i < VertexNormals.Length; i++)
            {
                RotatePointToNormal(norm.CosLat, norm.SinLat, norm.CosLon, norm.SinLon, ref VertexNormals[i]);
            }

            // Rotate()
            for (int i = 0; i < Vertices.Length; i++)
            {
                var tmp = Vertices[i].X;
                Vertices[i].X = Vertices[i].Y;
                Vertices[i].Y = Vertices[i].Z;
                Vertices[i].Z = tmp;

                tmp = VertexNormals[i].X;
                VertexNormals[i].X = VertexNormals[i].Y;
                VertexNormals[i].Y = VertexNormals[i].Z;
                VertexNormals[i].Z = tmp;
            }

            for (int i = 0; i < Corners.Length; i++)
            {
                var tmp = Corners[i].X;
                Corners[i].X = Corners[i].Y;
                Corners[i].Y = Corners[i].Z;
                Corners[i].Z = tmp;
            }

            // MatchHeight
            for (int i = 0; i < Vertices.Length; i++)
            {
                Vertices[i].Z += norm.Height;
            }

            for (int i = 0; i < Corners.Length; i++)
            {
                Corners[i].Z += norm.Height;
            }
        }

        public class NormalizeSettingsBasic
        {
            public double DeltaV { get; private set; }
            public Vector3d AvgV { get; private set; }

            public NormalizeSettingsBasic(double deltaV, Vector3d avgV)
            {
                DeltaV = deltaV;
                AvgV = avgV;
            }
        }

        public class NormalizeSettings
        {
            //            public double DeltaV { get; private set; }
            public Vector3d AvgV { get; private set; }
            public double CosLon { get; private set; }
            public double SinLon { get; private set; }
            public double CosLat { get; private set; }
            public double SinLat { get; private set; }
            public double Height { get; set; }
            //            public double BackToMeters { get; private set; }

            public NormalizeSettings(
                double cosLat, double sinLat,
                double cosLon, double sinLon,
                Vector3d avgV, double height)
            {
                AvgV = avgV;
                CosLon = cosLon;
                SinLon = sinLon;
                CosLat = cosLat;
                SinLat = sinLat;
                Height = height;
            }
        }
    }
}
