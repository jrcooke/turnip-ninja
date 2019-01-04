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

        public Mesh(Vector3d[][] grid)
        {
            var reducedPositions = new List<Vector3d>();
            var reducedTriangleIndices = new List<int>();
            var reducedExternalIndices = new List<int>();

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
            double fudge = cornerDists.Min()/ 100.0;

            int numChunks = 9;
            int minChunk = 0;
            int numChunkPrime = numChunks;
            //if (true)
            //{
            //    numChunks = 9;
            //    minChunk = 3;
            //    numChunkPrime = 3;
            //}
            int chunkMax = max / numChunks;

            var chunkInfos = new ChunkInfo[numChunkPrime * numChunkPrime];
            for (int chunkI = minChunk; chunkI < minChunk + numChunkPrime; chunkI++)
            {
                int iMin = chunkI * chunkMax;
                int iMax = (chunkI < numChunks - 1 ? chunkMax * (chunkI + 1) + 1 : max);
                int iCount = iMax - iMin;
                for (int chunkJ = minChunk; chunkJ < minChunk + numChunkPrime; chunkJ++)
                {
                    Debug.WriteLine(DateTime.Now + "\tWorking on chunk (" + (chunkI - minChunk) + "," + (chunkJ - minChunk) + ") " +
                        "(" + (((chunkI - minChunk) * numChunkPrime) + (chunkJ - minChunk)) + "/" + (numChunkPrime * numChunkPrime) + ")");
                    int jMin = chunkJ * chunkMax;
                    int jMax = (chunkJ < numChunks - 1 ? chunkMax * (chunkJ + 1) + 1 : max);
                    int jCount = jMax - jMin;

                    int vid = 0;
                    Vector3d[] positions = new Vector3d[iCount * jCount];
                    List<int> edgeIndices = new List<int>();
                    List<Vector3d> edges = new List<Vector3d>();
                    List<Vector3d> exteriors = new List<Vector3d>();
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

                            if ((chunkI == minChunk && i == iMin) ||
                                (chunkI == minChunk + numChunkPrime - 1 && i == iMax - 1) ||
                                (chunkJ == minChunk && j == jMin) ||
                                (chunkJ == minChunk + numChunkPrime - 1 && j == jMax - 1))
                            {
                                exteriors.Add(v);
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
                            triangleIncides[tid++] = (i + 1) * jCount + (j + 0);
                            triangleIncides[tid++] = (i + 0) * jCount + (j + 1);
                            triangleIncides[tid++] = (i + 1) * jCount + (j + 1);
                            triangleIncides[tid++] = (i + 0) * jCount + (j + 1);
                            triangleIncides[tid++] = (i + 1) * jCount + (j + 0);
                        }
                    }

                    var md = new SimplifyMesh(positions.ToArray(), triangleIncides.ToArray(), edgeIndices.ToArray(), true);
                    positions = null;
                    triangleIncides = null;

                    ChunkInfo chunkInfo = new ChunkInfo();
                    md.SimplifyMeshByThreshold(1.0E-3);
                    var startIndex = reducedPositions.Count;
                    var vertices = md.GetVertices();
                    reducedPositions.AddRange(vertices);
                    chunkInfo.EdgeIndices.AddRange(GetVertexIndices(vertices, edges, fudge).Select(ei => ei + startIndex));
                    reducedExternalIndices.AddRange(GetVertexIndices(vertices, exteriors, fudge).Select(exti => exti + startIndex));
                    reducedTriangleIndices.AddRange(md.GetIndices().Select(ti => ti + startIndex));
                    vertices = null;
                    edges = null;
                    exteriors = null;
                    md = null;

                    List<int> chunkNeighbors = new List<int>();
                    if (chunkI > minChunk) chunkNeighbors.Add((chunkI - 1 - minChunk) * numChunkPrime + (chunkJ - minChunk));
                    if (chunkI < minChunk + numChunkPrime - 1) chunkNeighbors.Add((chunkI + 1 - minChunk) * numChunkPrime + (chunkJ - minChunk));
                    if (chunkJ > minChunk) chunkNeighbors.Add((chunkI - minChunk) * numChunkPrime + (chunkJ - 1 - minChunk));
                    if (chunkJ < minChunk + numChunkPrime - 1) chunkNeighbors.Add((chunkI - minChunk) * numChunkPrime + (chunkJ + 1 - minChunk));
                    chunkInfo.Neighbors = chunkNeighbors.ToArray();

                    chunkInfos[(chunkI - minChunk) * numChunkPrime + (chunkJ - minChunk)] = chunkInfo;
                }
            }

            var reducedPositionsArray = reducedPositions.ToArray();
            reducedPositions = null;

            var reducedTriangleIndicesArray = reducedTriangleIndices.ToArray();
            reducedTriangleIndices = null;

            GlueChunks(reducedPositionsArray, reducedTriangleIndicesArray, chunkInfos, fudge);

            Vector3d[] psFinal = reducedPositionsArray;
            int[] tisFinal = reducedTriangleIndicesArray;
            if (true)
            {
                var mdFinal = new SimplifyMesh(reducedPositionsArray, reducedTriangleIndicesArray, reducedExternalIndices.ToArray(), true);
                reducedPositionsArray = null;
                reducedTriangleIndicesArray = null;

                mdFinal.SimplifyMeshByThreshold(1.0E-3);
                psFinal = mdFinal.GetVertices();
                tisFinal = mdFinal.GetIndices();
                mdFinal = null;
            }

            this.Vertices = psFinal;
            this.TriangleIndices = tisFinal;
        }

        private static IEnumerable<int> GetVertexIndices(Vector3d[] vertices, IEnumerable<Vector3d> edges, double fudge)
        {
            foreach (var e in edges)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (e.DeltaSq(ref vertices[i]) < fudge * fudge)
                    {
                        yield return i;
                        break;
                    }
                }
            }
        }

        private static void GlueChunks(Vector3d[] reducedPositions, int[] reducedTriangleIndices, ChunkInfo[] chunkInfos, double fudge)
        {
            double fudgeSq = fudge * fudge;
            Dictionary<int, int> equiv = new Dictionary<int, int>();
            for (int i = 0; i < chunkInfos.Length; i++)
            {
                ChunkInfo chunkInfoI = chunkInfos[i];
                for (int j = i + 1; j < chunkInfos.Length; j++)
                {
                    ChunkInfo chunkInfoJ = chunkInfos[j];
                    Debug.WriteLine(i + "\t" + j);

                    if (chunkInfoI.Neighbors.Contains(j))
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
