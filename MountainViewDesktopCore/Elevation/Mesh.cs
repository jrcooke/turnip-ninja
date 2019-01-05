using MeshDecimator;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MountainViewDesktopCore.Elevation
{
    public class Mesh
    {
        public Vector3d[] Vertices { get; private set; }
        public int[] TriangleIndices { get; private set; }
        public int[] CornerIndices { get; private set; }
        public Vector3d[] VertexNormals { get; private set; }

        public Mesh(Vector3d[][] grid, double threshold = 0.001)
        {
            var reducedPositions = new List<Vector3d>();
            var reducedTriangleIndices = new List<int>();
            var reducedExternalIndices = new List<int>();
            var reducedCornerIndices = new List<int>();

            var max = grid.Length;

            // The min distance between vertices is at the corners
            var cornerDists = new double[]
            {
                grid[0][0].DeltaSq(ref grid[0][1]),
                grid[0][0].DeltaSq(ref grid[1][0]),
                grid[max-1][0].DeltaSq(ref grid[max-1][1]),
                grid[max-1][0].DeltaSq(ref grid[max-2][0]),
                grid[0][max-1].DeltaSq(ref grid[0][max-2]),
                grid[0][max-1].DeltaSq(ref grid[1][max-1]),
                grid[max-1][max-1].DeltaSq(ref grid[max-1][max-2]),
                grid[max-1][max-1].DeltaSq(ref grid[max-2][max-1]),
            };
            double fudgeSq = cornerDists.Min() / 100.0;

            int numChunks = 9;
            int chunkMax = max / numChunks;
            bool verbose = false;

            List<int> chunkIs = new List<int>();
            List<int> chunkJs = new List<int>();
            for (int i = 0; i < numChunks; i++)
            {
                chunkIs.Add(i);
                chunkJs.Add(i);
            }

            if (false)
            {
                numChunks = 9;
                chunkIs = new List<int>() { 0 };
                chunkJs = new List<int>() { 7 };
                verbose = true;
            }

            var chunkInfos = new ChunkInfo[chunkIs.Count * chunkJs.Count];
            foreach (int chunkI in chunkIs)
            {
                int iMin = chunkI * chunkMax;
                int iMax = (chunkI < numChunks - 1 ? chunkMax * (chunkI + 1) + 1 : max);
                int iCount = iMax - iMin;
                foreach (int chunkJ in chunkJs)
                {
                    Debug.WriteLine(DateTime.Now + "\tWorking on chunk (" + (chunkI - chunkIs.Min()) + "," + (chunkJ - chunkJs.Min()) + ") " +
                        "(" + (((chunkI - chunkIs.Min()) * chunkJs.Count) + (chunkJ - chunkJs.Min())) + "/" + (chunkIs.Count * chunkJs.Count) + ")");
                    int jMin = chunkJ * chunkMax;
                    int jMax = (chunkJ < numChunks - 1 ? chunkMax * (chunkJ + 1) + 1 : max);
                    int jCount = jMax - jMin;

                    int vid = 0;
                    Vector3d[] positions = new Vector3d[iCount * jCount];
                    List<int> edgeIndices = new List<int>();
                    List<Vector3d> edges = new List<Vector3d>();
                    List<Vector3d> exteriors = new List<Vector3d>();
                    List<Vector3d> corners = new List<Vector3d>();
                    for (int i = iMin; i < iMax; i++)
                    {
                        for (int j = jMin; j < jMax; j++)
                        {
                            int iPrime = (max - 1 - i) * grid.Length / max;
                            int jPrime = (j) * grid[0].Length / max;
                            var v = grid[jPrime][iPrime];

                            if (i == iMin || i == (iMax - 1) || j == jMin || j == (jMax - 1))
                            {
                                edgeIndices.Add(vid);
                                edges.Add(v);
                            }

                            if ((chunkI == chunkIs.Min() && i == iMin) ||
                                (chunkI == chunkIs.Max() && i == iMax - 1) ||
                                (chunkJ == chunkJs.Min() && j == jMin) ||
                                (chunkJ == chunkJs.Max() && j == jMax - 1))
                            {
                                exteriors.Add(v);
                            }

                            if ((chunkI == chunkIs.Min() && i == iMin + 0 && chunkJ == chunkJs.Min() && j == jMin + 0) ||
                                (chunkI == chunkIs.Max() && i == iMax - 1 && chunkJ == chunkJs.Max() && j == jMax - 1) ||
                                (chunkI == chunkIs.Min() && i == iMin + 0 && chunkJ == chunkJs.Max() && j == jMax - 1) ||
                                (chunkI == chunkIs.Max() && i == iMax - 1 && chunkJ == chunkJs.Min() && j == jMin + 0))
                            {
                                corners.Add(v);
                            }

                            positions[vid++] = v;
                        }
                    }

                    // Create a collection of triangle indices
                    int tid = 0;
                    int[] triangleIncides = new int[(iCount - 1) * (jCount - 1) * 6];
                    for (int i = 0; i < iCount - 1; i++)
                    {
                        for (int j = 0; j < jCount - 1; j++)
                        {
                            triangleIncides[tid++] = (i + 0) * jCount + (j + 0);
                            triangleIncides[tid++] = (i + 0) * jCount + (j + 1);
                            triangleIncides[tid++] = (i + 1) * jCount + (j + 0);
                            triangleIncides[tid++] = (i + 1) * jCount + (j + 1);
                            triangleIncides[tid++] = (i + 1) * jCount + (j + 0);
                            triangleIncides[tid++] = (i + 0) * jCount + (j + 1);
                        }
                    }

                    var md = new SimplifyMesh(positions.ToArray(), triangleIncides.ToArray(), edgeIndices.ToArray(), verbose);
                    positions = null;
                    triangleIncides = null;

                    ChunkInfo chunkInfo = new ChunkInfo();
                    md.SimplifyMeshByThreshold(threshold);
                    var startIndex = reducedPositions.Count;
                    var vertices = md.GetVertices();
                    reducedPositions.AddRange(vertices);
                    chunkInfo.EdgeIndices.AddRange(GetVertexIndices(vertices, edges, fudgeSq).Select(ei => ei + startIndex));
                    reducedExternalIndices.AddRange(GetVertexIndices(vertices, exteriors, fudgeSq).Select(exti => exti + startIndex));
                    reducedCornerIndices.AddRange(GetVertexIndices(vertices, corners, fudgeSq).Select(exti => exti + startIndex));
                    reducedTriangleIndices.AddRange(md.GetIndices().Select(ti => ti + startIndex));
                    vertices = null;
                    edges = null;
                    exteriors = null;
                    corners = null;
                    md = null;

                    List<int> chunkNeighbors = new List<int>();
                    if (chunkI > chunkIs.Min()) chunkNeighbors.Add((chunkI - chunkIs.Min() - 1) * chunkJs.Count + (chunkJ - chunkJs.Min() + 0));
                    if (chunkI < chunkIs.Max()) chunkNeighbors.Add((chunkI - chunkIs.Min() + 1) * chunkJs.Count + (chunkJ - chunkJs.Min() + 0));
                    if (chunkJ > chunkJs.Min()) chunkNeighbors.Add((chunkI - chunkIs.Min() + 0) * chunkJs.Count + (chunkJ - chunkJs.Min() - 1));
                    if (chunkJ < chunkJs.Max()) chunkNeighbors.Add((chunkI - chunkIs.Min() + 0) * chunkJs.Count + (chunkJ - chunkJs.Min() + 1));
                    chunkInfo.Neighbors = chunkNeighbors.ToArray();

                    chunkInfos[(chunkI - chunkIs.Min()) * chunkJs.Count + (chunkJ - chunkJs.Min())] = chunkInfo;
                }
            }

            var reducedPositionsArray = reducedPositions.ToArray();
            reducedPositions = null;

            var reducedTriangleIndicesArray = reducedTriangleIndices.ToArray();
            reducedTriangleIndices = null;

            Vector3d[] cornerArray = reducedCornerIndices.Select(p => reducedPositionsArray[p]).ToArray();
            reducedCornerIndices = null;

            GlueChunks(reducedPositionsArray, reducedTriangleIndicesArray, chunkInfos, fudgeSq);

            Vector3d[] psFinal = reducedPositionsArray;
            int[] tisFinal = reducedTriangleIndicesArray;
            Vector3d[] vertexNormals = null;
            if (true)
            {
                var mdFinal = new SimplifyMesh(reducedPositionsArray, reducedTriangleIndicesArray, reducedExternalIndices.ToArray(), verbose);
                reducedPositionsArray = null;
                reducedTriangleIndicesArray = null;

                mdFinal.SimplifyMeshByThreshold(threshold);
                psFinal = mdFinal.GetVertices();
                tisFinal = mdFinal.GetIndices();
                vertexNormals = mdFinal.GetVertexNormals();
                mdFinal = null;
            }

            var cornersFinal = GetVertexIndices(psFinal, cornerArray, fudgeSq).ToArray();

            if (true)
            {
                Center(psFinal);
            }

            this.Vertices = psFinal;
            this.TriangleIndices = tisFinal;
            this.CornerIndices = cornersFinal;
            this.VertexNormals = vertexNormals;
            if (this.VertexNormals == null)
            {
                this.VertexNormals = psFinal.Select(p => new Vector3d(0, 0, 1)).ToArray();
            }
        }

        public static void CenterAndScale(Vector3d[][] positions)
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
        }

        public static void Center(Vector3d[] positions)
        {
            var avgV = new Vector3d(
                positions.Average(p => p.X),
                positions.Average(p => p.Y),
                positions.Average(p => p.Z));

            for (int i = 0; i < positions.Length; i++)
            {
                positions[i].X = positions[i].X - avgV.X;
                positions[i].Y = positions[i].Y - avgV.Y;
                positions[i].Z = positions[i].Z - avgV.Z;
            }
        }

        private static IEnumerable<int> GetVertexIndices(Vector3d[] vertices, IEnumerable<Vector3d> edges, double fudgeSq)
        {
            foreach (var e in edges)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (e.DeltaSq(ref vertices[i]) < fudgeSq)
                    {
                        yield return i;
                        break;
                    }
                }
            }
        }

        private static void GlueChunks(Vector3d[] reducedPositions, int[] reducedTriangleIndices, ChunkInfo[] chunkInfos, double fudgeSq)
        {
            Dictionary<int, int> equiv = new Dictionary<int, int>();
            for (int i = 0; i < chunkInfos.Length; i++)
            {
                ChunkInfo chunkInfoI = chunkInfos[i];
                for (int j = i + 1; j < chunkInfos.Length; j++)
                {
                    ChunkInfo chunkInfoJ = chunkInfos[j];
                    if (chunkInfoI.Neighbors.Contains(j))
                    {
                        Debug.WriteLine("Gluing chunks " + i + " and " + j);
                        foreach (int i2 in chunkInfoI.EdgeIndices)
                        {
                            foreach (int j2 in chunkInfoJ.EdgeIndices)
                            {
                                if (reducedPositions[i2].DeltaSq(ref reducedPositions[j2]) < fudgeSq)
                                {
                                    if (!equiv.ContainsKey(j2))
                                    {
                                        equiv.Add(j2, i2);
                                    }

                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // Glue seams. Don't worry about leftover indices
            for (int i = 0; i < reducedTriangleIndices.Length; i++)
            {
                if (equiv.TryGetValue(reducedTriangleIndices[i], out int newVertex))
                {
                    reducedTriangleIndices[i] = newVertex;
                }
            }
        }

        private class ChunkInfo
        {
            public List<int> EdgeIndices { get; set; } = new List<int>();
            public int[] Neighbors { get; set; }
        }
    }
}
