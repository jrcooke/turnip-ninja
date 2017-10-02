﻿namespace MountainView.Base
{
    public class Config
    {
        public Angle Lat { get; set; }
        public Angle Lon { get; set; }
        public double R { get; set; }
        public double DeltaR { get; set; }
        public Angle MinAngle { get; set; }
        public Angle MaxAngle { get; set; }
        public Angle ElevationViewMin { get; set; }
        public Angle ElevationViewMax { get; set; }
        public Angle AngularResolution { get; set; }


        public static Config Juaneta()
        {
            return new Config()
            {
                Lat = Angle.FromDecimalDegrees(47.695736),
                Lon = Angle.FromDecimalDegrees(-122.232330),
                R = 100000,
                DeltaR = 5,
                MinAngle = Angle.FromDecimalDegrees(85.0),
                MaxAngle = Angle.FromDecimalDegrees(95.0),
                ElevationViewMin = Angle.FromDecimalDegrees(-1.0),
                ElevationViewMax = Angle.FromDecimalDegrees(2.0),
                AngularResolution = Angle.FromDecimalDegrees(0.01),
            };
        }

        public static Config Home()
        {
            return new Config()
            {
                Lat = Angle.FromDecimalDegrees(47.6867797),
                Lon = Angle.FromDecimalDegrees(-122.2907541),
                R = 100000,
                DeltaR = 5,
                MinAngle = Angle.FromDecimalDegrees(85.0),
                MaxAngle = Angle.FromDecimalDegrees(95.0),
                ElevationViewMin = Angle.FromDecimalDegrees(-1.0),
                ElevationViewMax = Angle.FromDecimalDegrees(2.0),
                AngularResolution = Angle.FromDecimalDegrees(0.01),
            };
        }

        public static Config Owego()
        {
            return new Config()
            {
                Lat = Angle.FromDecimalDegrees(42.130303),
                Lon = Angle.FromDecimalDegrees(-76.243376),
                R = 5000,
                DeltaR = 1,
                MinAngle = Angle.FromDecimalDegrees(180),
                MaxAngle = Angle.FromDecimalDegrees(270),
                ElevationViewMin = Angle.FromDecimalDegrees(-25.0),
                ElevationViewMax = Angle.FromDecimalDegrees(5.0),
                AngularResolution = Angle.FromDecimalDegrees(0.05),
            };
        }
    }
}