using MeshDecimator;
using MountainView.Base;
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
                chunkIs = new List<int>() { 0, 1, 2, 3, 4 };
                chunkJs = new List<int>() { 0, 1, 2, 3, 4 };
                verbose = true;
            }

            var chunkInfos = new ChunkInfo[chunkIs.Count][];
            foreach (int chunkI in chunkIs)
            {
                chunkInfos[chunkI - chunkIs.Min()] = new ChunkInfo[chunkJs.Count];

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

                    chunkInfos[chunkI - chunkIs.Min()][chunkJ - chunkJs.Min()] = chunkInfo;
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

            Vertices = psFinal;
            TriangleIndices = tisFinal;
            VertexNormals = vertexNormals;
            if (VertexNormals == null)
            {
                VertexNormals = psFinal.Select(p => new Vector3d(0, 0, 1)).ToArray();
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

        private static void GlueChunks(Vector3d[] reducedPositions, int[] reducedTriangleIndices, ChunkInfo[][] chunkInfos, double fudgeSq)
        {
            Dictionary<int, int> equiv = new Dictionary<int, int>();
            for (int i1 = 0; i1 < chunkInfos.Length; i1++)
            {
                for (int j1 = 0; j1 < chunkInfos[i1].Length; j1++)
                {
                    ChunkInfo chunkInfoI = chunkInfos[i1][j1];

                    if (j1 < chunkInfos[i1].Length - 1)
                    {
                        Debug.WriteLine("Gluing chunks (" + i1 + "," + j1 + ") and (" + i1 + "," + (j1 + 1) + ")");
                        AlignEdges(reducedPositions, chunkInfoI, chunkInfos[i1][j1 + 1], fudgeSq, equiv);
                    }

                    if (i1 < chunkInfos.Length - 1)
                    {
                        Debug.WriteLine("Gluing chunks (" + i1 + "," + j1 + ") and (" + (i1 + 1) + "," + j1 + ")");
                        AlignEdges(reducedPositions, chunkInfoI, chunkInfos[i1 + 1][j1], fudgeSq, equiv);
                    }

                    if (j1 < chunkInfos[i1].Length - 1 && i1 < chunkInfos.Length - 1)
                    {
                        Debug.WriteLine("Gluing chunks (" + i1 + "," + j1 + ") and (" + (i1 + 1) + "," + (j1 + 1) + ")");
                        AlignEdges(reducedPositions, chunkInfoI, chunkInfos[i1 + 1][j1 + 1], fudgeSq, equiv, true);
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

        private static void AlignEdges(
            Vector3d[] reducedPositions,
            ChunkInfo chunkInfoI,
            ChunkInfo chunkInfoJ,
            double fudgeSq,
            Dictionary<int, int> equiv,
            bool singleMatch = false)
        {
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

                        if (singleMatch)
                        {
                            return;
                        }

                        break;
                    }
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
