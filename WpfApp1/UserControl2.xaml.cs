using MountainView.Mesh;
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
        private Model3DGroup myModel3DGroup;

        public UserControl2()
        {
            myCamera = new PerspectiveCamera()
            {
                Position = new Point3D(0, -06.66, 01.35),
                LookDirection = new Vector3D(0, 06.66, -01.35),
                FieldOfView = 60,
            };

            myModel3DGroup = new Model3DGroup();

            if (true)
            {
                myDirectionalLight = new DirectionalLight()
                {
                    Color = Colors.White,
                    Direction = new Vector3D(0, 0, 500),
                };

                myModel3DGroup.Children.Add(myDirectionalLight);

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

        public void Blarg(BitmapImage bi, FriendlyMesh m)
        {
            ImageBrush ib = new ImageBrush() { ImageSource = bi };
            ib.ViewportUnits = BrushMappingMode.Absolute;
            Material myMaterial = new DiffuseMaterial(ib);

            MeshGeometry3D myMeshGeometry3D = new MeshGeometry3D
            {
                Positions = new Point3DCollection(m.Vertices.Select(p => new Point3D(p.X, p.Y, p.Z))),
                TextureCoordinates = new PointCollection(m.VertexToImage.Select(p => new Point(p.X, p.Y))),
                Normals = new Vector3DCollection(m.VertexNormals.Select(p => new Vector3D(p.X, p.Y, p.Z))),
                TriangleIndices = new Int32Collection(m.TriangleIndices)
            };

            var myGeometryModel = new GeometryModel3D()
            {
                Material = myMaterial,
                Geometry = myMeshGeometry3D,
            };
            myModel3DGroup.Children.Add(myGeometryModel);
        }
    }
}