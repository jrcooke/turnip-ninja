using MountainView;
using MountainView.Base;
using MountainView.Render;
using MountainViewCore.Landmarks;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private FeatureInfo[][] features;

        public MainWindow()
        {
            InitializeComponent();

            DebugTraceListener log = new DebugTraceListener();
            Config config = new Config()
            {
                Height = 300,
                Width = 1200,
                MaxZoom = 3,
                MinZoom = 3,
                R = 150000,
                UseHaze = true,
                Lat = Angle.FromDecimalDegrees(47.637546),
                Lon = Angle.FromDecimalDegrees(-122.132786),
                MinAngleDec = 15,
                MaxAngleDec = 40,
                //Lat = Angle.FromDecimalDegrees(47.683923371494558),
                //Lon = Angle.FromDecimalDegrees(-122.29201376263447),
                //MinAngleDec = 135,
                //MaxAngleDec = 170,
                HeightOffset = 100,
                LocalTime = DateTimeOffset.Now,
            };

            Task.Run(async () => await Program.Doit(config, log, DrawToScreen));
        }

        private void DrawToScreen(Stream ms, FeatureInfo[][] features)
        {
            this.features = features;
            Dispatcher.Invoke(() =>
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.EndInit();
                image1.Source = image;
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

        private void Image1_MouseMove(object sender, MouseEventArgs e)
        {
            var tmpFeat = features;
            if (tmpFeat != null)
            {
                var mousePos = e.GetPosition((IInputElement)sender);
                int x = (int)(mousePos.X * tmpFeat.Length / ((Image)sender).ActualWidth);
                int y = tmpFeat[0].Length - 1 - (int)(mousePos.Y * tmpFeat[0].Length / ((Image)sender).ActualHeight);

                var feat = tmpFeat[x][y];
                if (feat != null)
                {
                    tttb.Text = feat.MapName + " (" + feat.Name + ") [" + feat.FeatureClass + "]";
                    tt.IsOpen = true;
                }
                else
                {
                    tt.IsOpen = false;
                }

                tt.HorizontalOffset = mousePos.X + 10;
                tt.VerticalOffset = mousePos.Y + 10;
            }
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
