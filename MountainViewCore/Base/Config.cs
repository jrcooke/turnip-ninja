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
        public Angle MinAngle { get; set; }

        /// <summary>
        /// 0 is facing north. 90 degrees is facing east.
        /// </summary>
        public Angle MaxAngle { get; set; }

        public Angle FOV { get { return Angle.Subtract(MaxAngle, MinAngle); } }
        public Angle AngularResolution { get { return Angle.Divide(FOV, Width); } }

        private bool hasIThetaMin;
        private int iThetaMin;
        public int IThetaMin
        {
            get
            {
                if (!hasIThetaMin)
                {
                    iThetaMin = Angle.FloorDivide(MinAngle, AngularResolution);
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
                MinAngle = Angle.FromDecimalDegrees(85.0),
                MaxAngle = Angle.FromDecimalDegrees(95.0),
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
                MinAngle = Angle.FromDecimalDegrees(0.0),
                MaxAngle = Angle.FromDecimalDegrees(360.0),
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
                MinAngle = Angle.FromDecimalDegrees(145.0),
                MaxAngle = Angle.FromDecimalDegrees(170.0),
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
                MinAngle = Angle.FromDecimalDegrees(85.0),
                MaxAngle = Angle.FromDecimalDegrees(95.0),
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
                MinAngle = Angle.FromDecimalDegrees(180),
                MaxAngle = Angle.FromDecimalDegrees(270),
                //ElevationViewMin = Angle.FromDecimalDegrees(-25.0),
                //ElevationViewMax = Angle.FromDecimalDegrees(5.0),
                //AngularResolution = Angle.FromDecimalDegrees(0.05),
            };
        }
    }
}
