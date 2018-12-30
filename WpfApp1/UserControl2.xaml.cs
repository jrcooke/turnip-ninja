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
            Debug.WriteLine(DateTime.Now + "\tStart blarg");

            ImageBrush ib = new ImageBrush() { ImageSource = bi };
            Material myMaterial = new DiffuseMaterial(ib);
            myGeometryModel.Material = myMaterial;

            Debug.WriteLine(DateTime.Now + "\tStart Point3DCollection");
            Point3DCollection myPositionCollection = new Point3DCollection();
            var max = heights.Length;
            for (int i = 0; i < max; i++)
            {
                for (int j = 0; j < max; j++)
                {
                    int iPrime = (max - 1 - i) * heights.Length / max;
                    int jPrime = (j) * heights[0].Length / max;
                    double height = 1000 * heights[jPrime][iPrime];
                    myPositionCollection.Add(new Point3D(
                        10.0 * (i - max / 2.0) * imageWidth / (max * imageHeight),
                        10.0 * (j - max / 2.0) / max,
                        10.0 * height / (max * imageHeight)));
                }
            }
            Debug.WriteLine(DateTime.Now + "\tEnd Point3DCollection");

            Debug.WriteLine(DateTime.Now + "\tStart Int32Collection");
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
            Debug.WriteLine(DateTime.Now + "\tEnd Int32Collection");

            var sourceVertices = myPositionCollection.Select(p => new MeshDecimator.Algorithms2.Vector3d(p.X, p.Y, p.Z)).ToArray();
            var sourceIndices = myTriangleIndicesCollection.ToArray();

            int currentTriangleCount = sourceIndices.Length / 3;

            var md = new MeshDecimator.Algorithms2.FastQuadricMeshSimplification();
            //            Mesh destMesh = MeshDecimation.DecimateMesh(algorithm, sourceMesh, targetTriangleCount);

            Debug.WriteLine(DateTime.Now + "\tStart Initialize");
            md.Initialize(sourceVertices, sourceIndices);
            Debug.WriteLine(DateTime.Now + "\tEnd Initialize");

            Debug.WriteLine(DateTime.Now + "\tStart SimplifyMeshLossless");
            md.SimplifyMeshLossless(true);
            Debug.WriteLine(DateTime.Now + "\tEnd SimplifyMeshLossless");

            var newVertices = md.GetVertices();
            var newIndices = md.GetIndices();

            // Apply the mesh to the geometry model.
            MeshGeometry3D myMeshGeometry3D = new MeshGeometry3D();
            myMeshGeometry3D.Positions = new Point3DCollection(newVertices.Select(p => new Point3D(p.x, p.y, 10 * p.z)));
            myMeshGeometry3D.TextureCoordinates = new PointCollection(newVertices.Select(p => new Point(p.x, -p.y)));
            myMeshGeometry3D.TriangleIndices = new Int32Collection(newIndices);
            myGeometryModel.Geometry = myMeshGeometry3D;
        }
    }
}
