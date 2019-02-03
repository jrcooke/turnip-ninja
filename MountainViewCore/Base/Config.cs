namespace MountainView.Base
{
    public class Config
    {
        public double R = 100000;
        public double DeltaR = 5;

        public int Width { get; set; }
        public int Height { get; set; }
        public Angle Lat { get; set; }
        public Angle Lon { get; set; }

        /// <summary>
        /// 0 is facing north. 90 degrees is facing east.
        /// </summary>
        public double MinAngleDec { get; set; }

        /// <summary>
        /// 0 is facing north. 90 degrees is facing east.
        /// </summary>
        public double MaxAngleDec { get; set; }

        public Angle AngularResolution { get { return Angle.Divide(Angle.FromDecimalDegrees(MaxAngleDec-MinAngleDec), Width); } }

        private bool hasIThetaMin;
        private int iThetaMin;
        public int IThetaMin
        {
            get
            {
                if (!hasIThetaMin)
                {
                    iThetaMin = Angle.FloorDivide(Angle.FromDecimalDegrees(MinAngleDec), AngularResolution);
                    hasIThetaMin = true;
                }

                return iThetaMin;
            }
        }

        public int MaxZoom { get; set; } = 6;
        public int MinZoom { get; set; } = 3;
        public bool UseHaze { get; set; } = true;

        public static Config Juaneta()
        {
            return new Config()
            {
                Lat = Angle.FromDecimalDegrees(47.695736),
                Lon = Angle.FromDecimalDegrees(-122.232330),
                MinAngleDec = (85.0),
                MaxAngleDec = (95.0),
                //ElevationViewMin = Angle.FromDecimalDegrees(-1.0),
                //ElevationViewMax = Angle.FromDecimalDegrees(2.0),
                //AngularResolution = Angle.FromDecimalDegrees(0.01),
            };
        }

        public static Config JuanetaAll()
        {
            return new Config()
            {
                Lat = Angle.FromDecimalDegrees(47.695736),
                Lon = Angle.FromDecimalDegrees(-122.232330),
                MinAngleDec = (0.0),
                MaxAngleDec = (360.0),
                //ElevationViewMin = Angle.FromDecimalDegrees(-5.0),
                //ElevationViewMax = Angle.FromDecimalDegrees(5.0),
                //AngularResolution = Angle.FromDecimalDegrees(0.1),
            };
        }

        public static Config Rainer()
        {
            return new Config()
            {
                Lat = Angle.FromDecimalDegrees(47.695736),
                Lon = Angle.FromDecimalDegrees(-122.232330),
                MinAngleDec = (145.0),
                MaxAngleDec = (170.0),
                //ElevationViewMin = Angle.FromDecimalDegrees(-1.0),
                //ElevationViewMax = Angle.FromDecimalDegrees(2.5),
                //AngularResolution = Angle.FromDecimalDegrees(0.01),
            };
        }

        public static Config Home()
        {
            return new Config()
            {
                Lat = Angle.FromDecimalDegrees(47.6867797),
                Lon = Angle.FromDecimalDegrees(-122.2907541),
                MinAngleDec = (85.0),
                MaxAngleDec = (95.0),
                //ElevationViewMin = Angle.FromDecimalDegrees(-1.0),
                //ElevationViewMax = Angle.FromDecimalDegrees(2.0),
                //AngularResolution = Angle.FromDecimalDegrees(0.01),
            };
        }

        public static Config Owego()
        {
            return new Config()
            {
                Lat = Angle.FromDecimalDegrees(42.130303),
                Lon = Angle.FromDecimalDegrees(-76.243376),
                MinAngleDec = (180),
                MaxAngleDec = (270),
                //ElevationViewMin = Angle.FromDecimalDegrees(-25.0),
                //ElevationViewMax = Angle.FromDecimalDegrees(5.0),
                //AngularResolution = Angle.FromDecimalDegrees(0.05),
            };
        }
    }
}
