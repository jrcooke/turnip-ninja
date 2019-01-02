using MeshDecimator;
using System;
using System.Collections.Generic;
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

            MeshGeometry3D myMeshGeometry3D = new MeshGeometry3D();
            myMeshGeometry3D.Positions = new Point3DCollection();
            myMeshGeometry3D.TextureCoordinates = new PointCollection();
            myMeshGeometry3D.TriangleIndices = new Int32Collection();

            int numChunks = 5;
            var max = heights.Length;
            var chunkMax = max / numChunks;
            for (int chunkI = 0; chunkI < numChunks; chunkI++)
            {
                int iMin = chunkI * chunkMax;
                int iMax = (chunkI < numChunks - 1 ? chunkMax * (chunkI + 1) + 1 : max);
                int iCount = iMax - iMin;
                for (int chunkJ = 0; chunkJ < numChunks; chunkJ++)
                {
                    int jMin = chunkJ * chunkMax;
                    int jMax = (chunkJ < numChunks - 1 ? chunkMax * (chunkJ + 1) + 1 : max);
                    int jCount = jMax - jMin;

                    int vid = 0;
                    Vector3d[] positions = new Vector3d[iCount*jCount];
                    for (int i = iMin; i < iMax; i++)
                    {
                        for (int j = jMin; j < jMax; j++)
                        {
                            int iPrime = (max - 1 - i) * heights.Length / max;
                            int jPrime = (j) * heights[0].Length / max;
                            double height = 10000 * heights[jPrime][iPrime];
                            positions[vid++] = new Vector3d(
                                10.0 * (i - max / 2.0) * imageWidth / (max * imageHeight),
                                10.0 * (j - max / 2.0) / max,
                                10.0 * height / (max * imageHeight));
                        }
                    }

                    // Create a collection of triangle indices for the MeshGeometry3D.
                    int tid = 0;
                    int[] TriangleIncides = new int[(iCount-1) * (jCount-1) * 6];
                    for (int i = 0; i < iCount - 1; i++)
                    {
                        for (int j = 0; j < jCount - 1; j++)
                        {
                            TriangleIncides[tid++] = (i + 0) * jCount + (j + 0);
                            TriangleIncides[tid++] = (i + 1) * jCount + (j + 0);
                            TriangleIncides[tid++] = (i + 0) * jCount + (j + 1);
                            TriangleIncides[tid++] = (i + 1) * jCount + (j + 1);
                            TriangleIncides[tid++] = (i + 0) * jCount + (j + 1);
                            TriangleIncides[tid++] = (i + 1) * jCount + (j + 0);
                        }
                    }

                    var md = new SimplifyMesh(positions.ToArray(), TriangleIncides.ToArray());
                    positions = null;
                    TriangleIncides = null;

                    md.SimplifyMeshByThreshold(1.0E-3);
                    var ps = md.GetVertices();
                    var tis = md.GetIndices();
                    md = null;

                    int positionOffset = myMeshGeometry3D.Positions.Count;

                    foreach (var p in ps)
                    {
                        myMeshGeometry3D.Positions.Add(new Point3D(p.X, p.Y, p.Z));
                        myMeshGeometry3D.TextureCoordinates.Add(new Point(p.X, -p.Y));
                    }
                    ps = null;

                    foreach (var ti in tis)
                    {
                        myMeshGeometry3D.TriangleIndices.Add(ti + positionOffset);
                    }
                    tis = null;
                }
            }

            myGeometryModel.Geometry = myMeshGeometry3D;
        }
    }
}