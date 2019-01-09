using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Mesh;
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

        //   private TextHolder th = new TextHolder();
        public MainWindow()
        {
            //        th = new TextHolder();
            //         DataContext = th;
            InitializeComponent();

            Watcher = new GeoCoordinateWatcher();
            Watcher.StatusChanged += Watcher_StatusChanged;
            Watcher.Start();

            NewMethod(47.683923371494558, -122.29201376263447, 4);
        }


        private void S_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (s1 != null && s3 != null && uc?.myCamera != null)
            {
                var theta = s1.Value * 2.0 * Math.PI;
                var x = (s3.Value) * Math.Sin(theta);
                var y = (s3.Value) * Math.Cos(theta);
                uc.myCamera.Position = new Point3D(0, -2 * x, -2 * y);
                uc.myCamera.LookDirection = new Vector3D(0, x, y);
                //uc.myDirectionalLight.Direction = new Vector3D(x, y, 0);
            }
        }

        private void ButtClick2(object sender, RoutedEventArgs e)
        {
            if (Watcher.Status == GeoPositionStatus.Ready && !Watcher.Position.Location.IsUnknown)
            {
                double lat = Watcher.Position.Location.Latitude;
                double lon = Watcher.Position.Location.Longitude;
                //  th.ButtClick(traceListener =>
                NewMethod(lat, lon, 4);
            }
        }

        private void NewMethod(double lat, double lon, int zoomLevel)
        {
            var template = StandardChunkMetadata.GetRangeContaingPoint(
                Angle.FromDecimalDegrees(lat),
                Angle.FromDecimalDegrees(lon),
                zoomLevel);

            Task.Run(async () =>
            {
                var av = await DoChunk(lat, lon, zoomLevel, new Vector3d(), 0.0);
                await DoChunk(lat + (-1) * template.LatDelta.DecimalDegree, lon + (-1) * template.LonDelta.DecimalDegree, zoomLevel, av.Item1, av.Item2);
                await DoChunk(lat + (-1) * template.LatDelta.DecimalDegree, lon + (+0) * template.LonDelta.DecimalDegree, zoomLevel, av.Item1, av.Item2);
                await DoChunk(lat + (-1) * template.LatDelta.DecimalDegree, lon + (+1) * template.LonDelta.DecimalDegree, zoomLevel, av.Item1, av.Item2);
                await DoChunk(lat + (+0) * template.LatDelta.DecimalDegree, lon + (-1) * template.LonDelta.DecimalDegree, zoomLevel, av.Item1, av.Item2);
                await DoChunk(lat + (+0) * template.LatDelta.DecimalDegree, lon + (+1) * template.LonDelta.DecimalDegree, zoomLevel, av.Item1, av.Item2);
                await DoChunk(lat + (+1) * template.LatDelta.DecimalDegree, lon + (-1) * template.LonDelta.DecimalDegree, zoomLevel, av.Item1, av.Item2);
                await DoChunk(lat + (+1) * template.LatDelta.DecimalDegree, lon + (+0) * template.LonDelta.DecimalDegree, zoomLevel, av.Item1, av.Item2);
                await DoChunk(lat + (+1) * template.LatDelta.DecimalDegree, lon + (+1) * template.LonDelta.DecimalDegree, zoomLevel, av.Item1, av.Item2);
            });
        }

        private async Task<Tuple<Vector3d, double>> DoChunk(double lat, double lon, int zoomLevel, Vector3d avgV, double deltaV)
        {
            Tuple<Vector3d, double> ret = new Tuple<Vector3d, double>(new Vector3d(), 0.0);
            var mesh = await MountainView.Program.GetMesh(null, lat, lon, zoomLevel);
            if (mesh == null)
            {
                return null;
            }

            // TODO: remove
            mesh.ExagerateHeight(3.0);
            if (deltaV <= 0.0)
            {
                mesh.GetCenterAndScale(out deltaV, out avgV);
                deltaV *= 4;
                ret = new Tuple<Vector3d, double>(avgV, deltaV);
            }

            mesh.Match(avgV, deltaV);

            var imageData = await MountainView.Program.GetImage(null, lat, lon, zoomLevel, false);
            if ((imageData?.Length ?? 0) > 0)
            {
                Dispatcher.Invoke(() =>
                {
                    BitmapImage image = null;
                    MemoryStream ms = new MemoryStream();
                    ms.Write(imageData, 0, imageData.Length);
                    ms.Seek(0, SeekOrigin.Begin);
                    image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = ms;
                    image.EndInit();

                    mainImage.Source = image;
                    uc.Blarg(image, mesh);
                });
            }

            return ret;
        }
        private void Watcher_StatusChanged(object sender, GeoPositionStatusChangedEventArgs e)
        {
            //     getCurrLocButt.IsEnabled = e.Status == GeoPositionStatus.Ready && !Watcher.Position.Location.IsUnknown;
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
