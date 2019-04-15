using MountainView.Base;
using System;

namespace MountainViewFn.Core
{
    public class DecConfig
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
        public double MinAngleDec { get; set; }
        public double MaxAngleDec { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Config GetConfig()
        {
            return new Config
            {
                Lat = Angle.FromDecimalDegrees(Lat),
                Lon = Angle.FromDecimalDegrees(Lon),
                MinAngleDec = MinAngleDec,
                MaxAngleDec = MaxAngleDec,
                Width = Width,
                Height = Height,
                MaxZoom = 3,
                MinZoom = 3,
                R = 150000,
                HeightOffset = 100,
                LocalTime = DateTimeOffset.Now,
            };
        }
    }
}
