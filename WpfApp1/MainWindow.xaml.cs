using System;
using System.Device.Location;
using System.Diagnostics;
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
        }


        private void S_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (s1 != null && s3 != null && uc?.myCamera != null)
            {
                var theta = s1.Value * 2.0 * Math.PI;
                var x = (s3.Value) * Math.Sin(theta);
                var y =   (s3.Value) * Math.Cos(theta);
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
                Task.Run(async () =>
                {
                    BitmapImage bi = null;
                    BitmapImage bi2 = null;
                    var mesh = await MountainView.Program.Foo2(null, lat, lon,
                        ms => this.Dispatcher.Invoke(() =>
                        {
                            // Tell the WPF image to use this stream.
                            bi = new BitmapImage();
                            bi.BeginInit();
                            bi.StreamSource = ms;
                            bi.EndInit();
//                            this.mainImage.Source = bi;
                        }),
                        ms => this.Dispatcher.Invoke(() =>
                        {
                            // Tell the WPF image to use this stream.
                            bi2 = new BitmapImage();
                            bi2.BeginInit();
                            bi2.StreamSource = ms;
                            bi2.EndInit();
  //                          this.mainImage2.Source = bi2;
                        }));

//                    Debug.WriteLine(data.Item2.Length);

                    this.Dispatcher.Invoke(() =>
                    {
                        uc.Blarg(bi2, mesh);
                    });

                });
            }
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
