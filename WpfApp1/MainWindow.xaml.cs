using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Mesh;
using MountainView.Render;
using SoftEngine;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // The coordinate watcher.
        private GeoCoordinateWatcher Watcher = null;

        private Device device;

        //   private TextHolder th = new TextHolder();
        public MainWindow()
        {
            //        th = new TextHolder();
            //         DataContext = th;
            InitializeComponent();

            Watcher = new GeoCoordinateWatcher();
            Watcher.StatusChanged += Watcher_StatusChanged;
            Watcher.Start();

            s1.Value = UserControl2.InitAng;
            s3.Value = UserControl2.InitM;

            // Choose the back buffer resolution here
            var bmp = new DirectBitmap(640, 480);

            var camera = new SoftEngine.Camera
            {
                Position = new Vector3f(0, 0, 10.0f),
                Target = new Vector3f()
            };

            device = new Device(bmp)
            {
                Camera = camera,
                AmbientLight = 0.5f,
                DirectLight = 1.0f,
                Light = new Vector3f(0, 0, 20),
            };

            S_ValueChanged(null, null);
        }

        private void S_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (s1 != null && s3 != null)
            {
                var m = s3.Value;
                var theta = (s1.Value) * 2.0 * Math.PI;
                var phi = s2.Value * 2.0 * Math.PI;
                var x = m * Math.Sin(phi) * Math.Sin(theta);
                var y = m * Math.Sin(phi) * Math.Cos(theta);
                var z = m * Math.Cos(phi);

                if (uc?.myCamera != null)
                {
                    uc.myCamera.Position = new Point3D(x, y, z);
                    uc.myCamera.LookDirection = new Vector3D(-x, -y, -z);
                    Debug.WriteLine(uc.myCamera.Position);
                }

                if (device != null)
                {
                    device.Camera = new SoftEngine.Camera()
                    {
                        Position = new Vector3f((float)x, (float)y, (float)z),
                        Target = device.Camera.Target,
                    };

                    using (var ms = device.Render())
                    {
                        BitmapImage image;
                        image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = ms;
                        image.EndInit();
                        image1.Source = image;
                    };
                }
            }
        }

        private void ButtClick2(object sender, RoutedEventArgs e)
        {
            if (Watcher.Status == GeoPositionStatus.Ready && !Watcher.Position.Location.IsUnknown)
            {
                double lat = Watcher.Position.Location.Latitude;
                double lon = Watcher.Position.Location.Longitude;

                // Mt. Ranier
                //NewMethod(46.853100, -121.759100, 4);

                // Home
                // NewMethod(log, 47.683923371494558, -122.29201376263447, 4);

                //  th.ButtClick(traceListener =>
                DebugTraceListener log = new DebugTraceListener();
                NewMethod(log, lat, lon, 4);
            }
        }

        private void NewMethod(TraceListener log, double lat, double lon, int zoomLevel)
        {
            var template = StandardChunkMetadata.GetRangeContaingPoint(
                Angle.FromDecimalDegrees(lat),
                Angle.FromDecimalDegrees(lon),
                zoomLevel);
            int n = 0;

            Task.Run(async () =>
            {
                var norm = await DoChunk(log, lat, lon, zoomLevel);
                for (int i = -n; i <= n; i++)
                    for (int j = -n; j <= n; j++)
                        if (i != 0 || j != 0)
                        {
                            await DoChunk(
                                log,
                                lat + i * template.LatDelta.DecimalDegree,
                                lon + j * template.LonDelta.DecimalDegree,
                                zoomLevel,
                                norm);
                        }
            });
        }

        private class DebugTraceListener : TraceListener
        {
            public override void Write(string message)
            {
                Debug.Write(message);
            }

            public override void WriteLine(string message)
            {
                Debug.WriteLine(message);
            }
        }

        private async Task<FriendlyMesh.NormalizeSettings> DoChunk(TraceListener log, double lat, double lon, int zoomLevel, FriendlyMesh.NormalizeSettings norm = null, double threshold = 1.0E-3)
        {
            var mesh = await MountainView.Program.GetMesh(log, lat, lon, zoomLevel);
            if (mesh == null)
            {
                return null;
            }

            // TODO: remove
            mesh.SimplifyMesh(threshold, true);

            mesh.ExagerateHeight(3.0);
            if (norm == null)
            {
                norm = mesh.GetCenterAndScale(lat, lon, 4);
            }

            mesh.Match(norm);

            mesh.Rotate();

            MemoryStream ms = new MemoryStream();
            ms.Write(mesh.ImageData, 0, mesh.ImageData.Length);
            ms.Seek(0, SeekOrigin.Begin);

            using (MemoryStream ms2 = new MemoryStream())
            {
                ms2.Write(mesh.ImageData, 0, mesh.ImageData.Length);
                ms2.Seek(0, SeekOrigin.Begin);
                using (DirectBitmap bm = new DirectBitmap(ms2))
                {
                    var renderMesh = Mesh.GetMesh(bm, mesh);
                    device.Meshes.Add(renderMesh);
                }
            }

            Dispatcher.Invoke(() =>
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.EndInit();

                mainImage.Source = image;
                uc.Blarg(image, mesh);


                S_ValueChanged(null, null);

            });

            return norm;
        }
        private void Watcher_StatusChanged(object sender, GeoPositionStatusChangedEventArgs e)
        {
            getCurrLocButt.IsEnabled = e.Status == GeoPositionStatus.Ready && !Watcher.Position.Location.IsUnknown;
        }
    }

    //class TextHolder : INotifyPropertyChanged
    //{
    //    public event PropertyChangedEventHandler PropertyChanged;

    //    private readonly object locker = new object();

    //    private Timer t;
    //    private StringBuilder sb;
    //    private bool changed;

    //    public void ButtClick(Func<TraceListener, Task> action)
    //    {
    //        BBB bbb = new BBB(() => sb, () => changed = true, locker);
    //        Task.Run(() => action(bbb));
    //    }

    //    public TextHolder()
    //    {
    //        t = new Timer(100);
    //        t.Elapsed += T_Elapsed;
    //        t.Start();
    //        sb = new StringBuilder();
    //    }

    //    private void T_Elapsed(object sender, ElapsedEventArgs e)
    //    {
    //        if (changed)
    //        {
    //            changed = false;
    //            lock (locker)
    //            {
    //                var oldSB = sb;
    //                sb = new StringBuilder();
    //                Text += oldSB.ToString();
    //                if (Text.Length > 10000)
    //                {
    //                    Text = Text.Substring(Text.Length - 10000);
    //                }
    //            }
    //            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Text"));
    //        }
    //    }

    //    public string Text { get; set; }

    //    private class BBB : TraceListener
    //    {
    //        readonly Action callback;
    //        readonly Func<StringBuilder> sb;
    //        readonly object locker;

    //        public BBB(Func<StringBuilder> sb, Action callback, object locker)
    //        {
    //            this.sb = sb;
    //            this.callback = callback;
    //            this.locker = locker;
    //        }

    //        public override void Write(string message)
    //        {
    //            Debug.Write(message);
    //            lock (locker)
    //            {
    //                sb().Append(message);
    //            }
    //            callback?.Invoke();
    //        }

    //        public override void WriteLine(string message)
    //        {
    //            Debug.WriteLine(message);
    //            lock (locker)
    //            {
    //                sb().AppendLine(message);
    //            }
    //            callback?.Invoke();
    //        }
    //    }
}
