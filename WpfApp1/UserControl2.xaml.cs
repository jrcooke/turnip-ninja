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
        private Model3DGroup myModel3DGroup;
        public const double InitAng = 0.45;
        public const double InitM = 5;

        public UserControl2()
        {
            myCamera = new PerspectiveCamera() {
                FieldOfView = 15,
                UpDirection = new Vector3D(0, 0, 1),
                NearPlaneDistance = 0,
            };

            myModel3DGroup = new Model3DGroup();
            myModel3DGroup.Children.Add(new AmbientLight(Colors.White));

            ModelVisual3D myModelVisual3D = new ModelVisual3D()
            {
                Content = myModel3DGroup,
            };

            // Asign the camera to the viewport
            Viewport3D myViewport3D = new Viewport3D() { Camera = myCamera };
            myViewport3D.Children.Add(myModelVisual3D);
            Content = myViewport3D;
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