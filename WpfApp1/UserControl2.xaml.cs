using MeshDecimator;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace WpfApp1
{
    public partial class UserControl2 : UserControl
    {
        public PerspectiveCamera myCamera;
        public DirectionalLight myDirectionalLight;
        private GeometryModel3D myGeometryModel;

        public UserControl2()
        {
            myCamera = new PerspectiveCamera()
            {
                Position = new Point3D(0, -06.66, 01.35),
                LookDirection = new Vector3D(0, 06.66, -01.35),
                FieldOfView = 60,
            };

            Model3DGroup myModel3DGroup = new Model3DGroup();
            myGeometryModel = new GeometryModel3D();
            myModel3DGroup.Children.Add(myGeometryModel);

            if (true)
            {
                myDirectionalLight = new DirectionalLight()
                {
                    Color = Colors.White,
                    Direction = new Vector3D(0, 0, -500),
                };

                myModel3DGroup.Children.Add(myDirectionalLight);
            }
            else
            {
                myModel3DGroup.Children.Add(new AmbientLight(Colors.White));
            }

            ModelVisual3D myModelVisual3D = new ModelVisual3D()
            {
                Content = myModel3DGroup,
            };

            // Asign the camera to the viewport
            Viewport3D myViewport3D = new Viewport3D() { Camera = myCamera };
            myViewport3D.Children.Add(myModelVisual3D);

            this.Content = myViewport3D;
        }

        public void Blarg(BitmapImage bi, float[][] heights, double imageWidth, double imageHeight)
        {
            ImageBrush ib = new ImageBrush() { ImageSource = bi };
            Material myMaterial = new DiffuseMaterial(ib);
            myGeometryModel.Material = myMaterial;

            var reducedPositions = new List<Vector3d>();
            var reducedTriangleIndices = new List<int>();
            var reducedExternalIndices = new List<int>();

            var max = heights.Length;
            double xSpacing = 10.0 * imageWidth / (max * imageHeight);
            double ySpacing = 10.0 / max;

            double fudge = Math.Min(xSpacing, ySpacing) / 100.0;

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
                            int iPrime = (max - 1 - i) * heights.Length / max;
                            int jPrime = (j) * heights[0].Length / max;
                            double height = 10000 * heights[jPrime][iPrime];
                            var v = new Vector3d(
                                (i - max / 2.0) * xSpacing,
                                (j - max / 2.0) * ySpacing,
                                10.0 * height / (max * imageHeight));

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

                    var md = new SimplifyMesh(positions.ToArray(), triangleIncides.ToArray(), edgeIndices.ToArray());
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

            GlueChunks(reducedPositionsArray, reducedTriangleIndices, chunkInfos, fudge);

            IEnumerable<Vector3d> psFinal = reducedPositionsArray;
            IEnumerable<int> tisFinal = reducedTriangleIndices;
            if (true)
            {
                var mdFinal = new SimplifyMesh(reducedPositionsArray, reducedTriangleIndices.ToArray(), reducedExternalIndices.ToArray());

                reducedPositionsArray = null;
                reducedTriangleIndices = null;

                mdFinal.SimplifyMeshByThreshold(1.0E-3);
                psFinal = mdFinal.GetVertices();
                tisFinal = mdFinal.GetIndices();
                mdFinal = null;
            }

            MeshGeometry3D myMeshGeometry3D = new MeshGeometry3D();
            myMeshGeometry3D.Positions = new Point3DCollection(psFinal.Select(p => new Point3D(p.X, p.Y, p.Z)));
            myMeshGeometry3D.TextureCoordinates = new PointCollection(psFinal.Select(p => new Point(p.X, -p.Y)));
            myMeshGeometry3D.TriangleIndices = new Int32Collection(tisFinal);
            psFinal = null;
            tisFinal = null;

            myGeometryModel.Geometry = myMeshGeometry3D;
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

        private static void GlueChunks(Vector3d[] reducedPositions, List<int> reducedTriangleIndices, ChunkInfo[] chunkInfos, double fudge)
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
            for (int i = 0; i < reducedTriangleIndices.Count; i++)
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