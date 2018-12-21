using System;
using System.ComponentModel;
using System.Device.Location;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
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

        private TextHolder th = new TextHolder();
        public MainWindow()
        {
            th = new TextHolder();
            DataContext = th;
            InitializeComponent();

            Watcher = new GeoCoordinateWatcher();
            Watcher.StatusChanged += Watcher_StatusChanged;
            Watcher.Start();
        }

        private void ButtClick2(object sender, RoutedEventArgs e)
        {
            if (Watcher.Status == GeoPositionStatus.Ready && !Watcher.Position.Location.IsUnknown)
            {
                double lat = Watcher.Position.Location.Latitude;
                double lon = Watcher.Position.Location.Longitude;
                th.ButtClick(traceListener =>
                    MountainView.Program.Foo2(traceListener, lat, lon, ms => this.Dispatcher.Invoke(() =>
                    {
                        // Tell the WPF image to use this stream.
                        BitmapImage bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = ms;
                        bi.EndInit();
                        this.mainImage.Source = bi;
                    })));
            }
        }

        private void Watcher_StatusChanged(object sender, GeoPositionStatusChangedEventArgs e)
        {
            getCurrLocButt.IsEnabled = e.Status == GeoPositionStatus.Ready && !Watcher.Position.Location.IsUnknown;
        }
    }

    class TextHolder : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly object locker = new object();

        private Timer t;
        private StringBuilder sb;
        private bool changed;

        public void ButtClick(Func<TraceListener, Task> action)
        {
            BBB bbb = new BBB(() => sb, () => changed = true, locker);
            Task.Run(() => action(bbb));
        }

        public TextHolder()
        {
            t = new Timer(100);
            t.Elapsed += T_Elapsed;
            t.Start();
            sb = new StringBuilder();
        }

        private void T_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (changed)
            {
                changed = false;
                lock (locker)
                {
                    var oldSB = sb;
                    sb = new StringBuilder();
                    Text += oldSB.ToString();
                    if (Text.Length > 10000)
                    {
                        Text = Text.Substring(Text.Length - 10000);
                    }
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Text"));
            }
        }

        public string Text { get; set; }

        private class BBB : TraceListener
        {
            readonly Action callback;
            readonly Func<StringBuilder> sb;
            readonly object locker;

            public BBB(Func<StringBuilder> sb, Action callback, object locker)
            {
                this.sb = sb;
                this.callback = callback;
                this.locker = locker;
            }

            public override void Write(string message)
            {
                Debug.Write(message);
                lock (locker)
                {
                    sb().Append(message);
                }
                callback?.Invoke();
            }

            public override void WriteLine(string message)
            {
                Debug.WriteLine(message);
                lock (locker)
                {
                    sb().AppendLine(message);
                }
                callback?.Invoke();
            }
        }
    }
}
