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
        public Angle MinAngle { get; set; }
        public Angle MaxAngle { get; set; }
        public Angle ElevationViewMin { get; set; }
        public Angle ElevationViewMax { get; set; }
        public Angle AngularResolution { get; set; }

        private bool hasNumTheta;
        private int numTheta;
        public int NumTheta
        {
            get
            {
                if (!hasNumTheta)
                {
                    numTheta = Angle.FloorDivide(MaxAngle, AngularResolution) - Angle.FloorDivide(MinAngle, AngularResolution);
                    hasNumTheta = true;
                }

                return numTheta;
            }
        }

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

        public static Config Juaneta()
        {
            return new Config()
            {
                Lat = Angle.FromDecimalDegrees(47.695736),
                Lon = Angle.FromDecimalDegrees(-122.232330),
                MinAngle = Angle.FromDecimalDegrees(85.0),
                MaxAngle = Angle.FromDecimalDegrees(95.0),
                ElevationViewMin = Angle.FromDecimalDegrees(-1.0),
                ElevationViewMax = Angle.FromDecimalDegrees(2.0),
                AngularResolution = Angle.FromDecimalDegrees(0.01),
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
                ElevationViewMin = Angle.FromDecimalDegrees(-5.0),
                ElevationViewMax = Angle.FromDecimalDegrees(5.0),
                AngularResolution = Angle.FromDecimalDegrees(0.1),
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
                ElevationViewMin = Angle.FromDecimalDegrees(-1.0),
                ElevationViewMax = Angle.FromDecimalDegrees(2.5),
                AngularResolution = Angle.FromDecimalDegrees(0.01),
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
                MinAngle = Angle.FromDecimalDegrees(180),
                MaxAngle = Angle.FromDecimalDegrees(270),
                ElevationViewMin = Angle.FromDecimalDegrees(-25.0),
                ElevationViewMax = Angle.FromDecimalDegrees(5.0),
                AngularResolution = Angle.FromDecimalDegrees(0.05),
            };
        }
    }
}
