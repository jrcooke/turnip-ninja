using MeshDecimator;
using System;
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

            Point3DCollection myPositionCollection = new Point3DCollection();
            var max = heights.Length;
            for (int i = 0; i < max; i++)
            {
                for (int j = 0; j < max; j++)
                {
                    int iPrime = (max - 1 - i) * heights.Length / max;
                    int jPrime = (j) * heights[0].Length / max;
                    double height = 10000 * heights[jPrime][iPrime];
                    myPositionCollection.Add(new Point3D(
                        10.0 * (i - max / 2.0) * imageWidth / (max * imageHeight),
                        10.0 * (j - max / 2.0) / max,
                        10.0 * height / (max * imageHeight)));
                }
            }

            // Create a collection of triangle indices for the MeshGeometry3D.
            Int32Collection myTriangleIndicesCollection = new Int32Collection();
            for (int i = 0; i < (max - 1); i++)
            {
                for (int j = 0; j < (max - 1); j++)
                {
                    myTriangleIndicesCollection.Add((i + 0) * max + (j + 0));
                    myTriangleIndicesCollection.Add((i + 1) * max + (j + 0));
                    myTriangleIndicesCollection.Add((i + 0) * max + (j + 1));
                    myTriangleIndicesCollection.Add((i + 1) * max + (j + 1));
                    myTriangleIndicesCollection.Add((i + 0) * max + (j + 1));
                    myTriangleIndicesCollection.Add((i + 1) * max + (j + 0));
                }
            }

            SimpleMesh sm = new SimpleMesh(myPositionCollection, myTriangleIndicesCollection);
            myPositionCollection = null;
            myTriangleIndicesCollection = null;
            sm.Simplify();

            MeshGeometry3D myMeshGeometry3D = new MeshGeometry3D();
            myMeshGeometry3D.Positions = sm.Positions;
            myMeshGeometry3D.TextureCoordinates = new PointCollection(sm.Positions.Select(p => new Point(p.X, -p.Y)));
            myMeshGeometry3D.TriangleIndices = sm.TriangleIncides;
            myGeometryModel.Geometry = myMeshGeometry3D;
        }

        private class SimpleMesh
        {
            public Point3DCollection Positions { get; set; }
            public Int32Collection TriangleIncides { get; set; }

            public SimpleMesh(Point3DCollection positions, Int32Collection triangleIncides)
            {
                Positions = positions;
                TriangleIncides = triangleIncides;
            }

            public void Simplify()
            {

                var sourceVertices = Positions.Select(p => new Vector3d(p.X, p.Y, p.Z)).ToArray();
                var md = new SimplifyMesh(sourceVertices, TriangleIncides.ToArray());
                Positions = null;
                TriangleIncides = null;

                md.SimplifyMeshLossless(1.0E-3);
                var newVertices = md.GetVertices();
                var newIndices = md.GetIndices();

                Positions = new Point3DCollection(newVertices.Select(p => new Point3D(p.X, p.Y, p.Z)));
                TriangleIncides = new Int32Collection(newIndices);
            }
        }
    }
}