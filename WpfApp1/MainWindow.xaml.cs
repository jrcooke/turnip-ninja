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
                AmbientLight = 0.5f,
                DirectLight = 3.5f,
                Light = new Vector3f(1, -1, 0.1f),
            };

            var ddd = new DateTimeOffset(2019, 2, 1, 0, 0, 0, TimeSpan.Zero);
            while (ddd < new DateTimeOffset(2019, 2, 10, 0, 0, 0, TimeSpan.Zero))
            {
                Sdgsfdgsfdg(config.Lat, config.Lon, ddd);
                ddd = ddd.AddHours(1);
            }

            Task.Run(async () => await Program.Doit(config, log, DrawToScreen));
        }


        private static void Sdgsfdgsfdg(Angle lat, Angle lon, DateTimeOffset curTime)
        {
            // ts is standard time in decimal hours
            var J = curTime.ToUniversalTime().DayOfYear;
            var ts = curTime.ToUniversalTime().TimeOfDay.TotalHours;

            // t  is solar time in radians
            var omega = Math.PI / 12 * (ts - 12)  //- lon.Radians
                + 0.170 / 12 * Math.PI * Math.Sin(4.0 * Math.PI * (J - 80) / 373.0)
                - 0.129 / 12 * Math.PI * Math.Sin(2.0 * Math.PI * (J - 8) / 355.0);

            // The solar declination in radians is approximated by
            var delta = 0.4093 * Math.Sin(2.0 * Math.PI * (J - 81) / 368.0);

            /*
            http://www.powerfromthesun.net/Book/chapter03/chapter03.html
            Local coords
            alpha is solar angle above horizon
            A is solar azimuthal angle
            S_z = sin alpha       (upward)
            S_e = cos alpha sin A (east pointing)
            S_n = cos alpha cos A (north pointing)

            Earth-center coords
            S'_m = cos delta cos omega (from center to equator, hits where observer meridian hits equator)
            S'_e = cos delta sin omega (eastward on equator)
            S'_p = sin delta           (north polar)

            Rotate up from polar up to z up
            S_z = S'_m cos lat + S'_p sin lat
            S_e = S'_e
            S_n = S'_p cos lat - S'_m sin lat

            Substituting
            sin alpha       = cos delta cos omega cos lat + sin delta           sin lon
            cos alpha sin A = cos delta sin omega
            cos alpha cos A = sin delta           cos lat - cos delta cos omega sin lon

            So
            alpha = asin (cos delta cos omega cos lat + sin delta sin lon)
            A     = atan2(cos delta sin omega , (sin delta cos lat - cos delta cos omega sin lon))

            */
            var alpha = Math.Asin(
                (Math.Cos(delta) * Math.Cos(omega) * Math.Cos(lat.Radians) + Math.Sin(delta) * Math.Sin(lat.Radians))
                );

            // Switch to A=0 be south
            var A = Math.Atan2(
                Math.Cos(delta) * Math.Sin(omega),
                Math.Sin(delta) * Math.Cos(lat.Radians) - Math.Cos(delta) * Math.Cos(omega) * Math.Sin(lat.Radians)
                );

            if (Math.Sin(omega) > 0)
            {
                //     solarAzimuth = 2 * Math.PI - solarAzimuth;
            }

            //while (solarAzimuth > +Math.PI) solarAzimuth -= Math.PI;
            //while (solarAzimuth < 0) solarAzimuth += Math.PI;


            //while (A > +Math.PI) A -= 2 * Math.PI;
            if (A < 0) A += 2 * Math.PI;

            if (alpha > 0)
            {
                Debug.WriteLine(J + ts / 24 + " \t" + alpha + " \t" + A);
            }
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
