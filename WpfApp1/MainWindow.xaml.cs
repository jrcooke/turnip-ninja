using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Imaging;
using MountainView.Mesh;
using MountainView.Render;
using MountainViewCore.Base;
using SoftEngine;
using System;
using System.Device.Location;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // The coordinate watcher.
        private GeoCoordinateWatcher Watcher = null;

        public MainWindow()
        {
            InitializeComponent();

            Watcher = new GeoCoordinateWatcher();
            Watcher.StatusChanged += Watcher_StatusChanged;
            Watcher.Start();

            // Mt. Ranier
            //NewMethod(46.853100, -121.759100, 4);

            DebugTraceListener log = new DebugTraceListener();
            //            Home
            //            NewMethod(log, 47.683923371494558, -122.29201376263447, 4);

            //Config c = Config.Rainer();
            //Config config = Config.JuanetaAll();

            Config config = Config.Juaneta();
            config.Width = 600;
            config.Height = 300;

            config.Lat = Angle.FromDecimalDegrees(47.683923371494558);
            config.Lon = Angle.FromDecimalDegrees(-122.29201376263447);

            Task.Run(async () => await Doit(config, log));
        }


        public async Task Doit(Config config, TraceListener log)
        {
            DateTime start = DateTime.Now;
            BlobHelper.SetConnectionString(ConfigurationManager.AppSettings["ConnectionString"]);

            var theta = (config.MaxAngle.DecimalDegree + config.MinAngle.DecimalDegree) * Math.PI / 360;
            var z = 0.05f;
            int subpixel = 3;

            var chunkBmp = new DirectBitmap(subpixel * config.Width, subpixel * config.Height);
            var compositeBmp = new DirectBitmap(subpixel * config.Width, subpixel * config.Height);
            compositeBmp.SetAllPixels(View.skyColor);

            Device device = new Device()
            {
                Camera = new Camera()
                {
                    Position = new Vector3f(0, 0, z),
                    Target = new Vector3f((float)Math.Sin(theta), (float)Math.Cos(theta), z),
                    UpDirection = new Vector3f(0, 0, 1),
                    FovRad = (float)config.FOV.Radians,
                },
                AmbientLight = 0.5f,
                DirectLight = 1.0f,
                Light = new Vector3f(0, 0, 20),
            };

            var chunks = View.GetRelevantChunkKeys(config, log);

            StandardChunkMetadata mainChunk = StandardChunkMetadata.GetRangeFromKey(chunks.Last());
            var mainMesh = await Meshes.Current.GetData(mainChunk, log);
            var norm = mainMesh.GetCenterAndScale(config.Lat.DecimalDegree, config.Lon.DecimalDegree, mainChunk.ZoomLevel, 10);

            int counter = 0;
            foreach (var chunkKey in chunks)
            {
                StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);
                if (chunk == null) continue;

                var mesh = await Meshes.Current.GetData(chunk, log);
                if (mesh == null) continue;

                if (norm == null)
                {
                    norm = mesh.GetCenterAndScale(config.Lat.DecimalDegree, config.Lon.DecimalDegree, chunk.ZoomLevel, 10);
                }

                mesh.Match(norm);
                mesh.ImageData = await JpegImages.Current.GetData(chunk, log);
                var renderMesh = Mesh.GetMesh(mesh.ImageData, mesh);
                foreach (var oldMesh in device.Meshes)
                {
                    oldMesh.Dispose();
                }

                device.Meshes.Clear();
                device.Meshes.Add(renderMesh);

                device.RenderInto(chunkBmp);

                compositeBmp.DrawOn(chunkBmp);

                DrawToScreen(compositeBmp);

                using (var fs = File.OpenWrite(Path.Combine(".", counter + ".jpg")))
                {
                    chunkBmp.WriteFile(OutputType.JPEG, fs);
                }

                counter++;
                log.WriteLine(counter);
            }

            DateTime end = DateTime.Now;
            log.WriteLine(start);
            log.WriteLine(end);
            log.WriteLine(end - start);
        }

        private void DrawToScreen(DirectBitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                bmp.WriteFile(OutputType.PNG, ms);
                ms.Position = 0;
                Dispatcher.Invoke(() =>
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image1.Source = image;
                });
            }
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

        private void Watcher_StatusChanged(object sender, GeoPositionStatusChangedEventArgs e)
        {
            //            getCurrLocButt.IsEnabled = e.Status == GeoPositionStatus.Ready && !Watcher.Position.Location.IsUnknown;
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
