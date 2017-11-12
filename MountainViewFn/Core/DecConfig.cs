using MountainView.Base;

namespace MountainViewFn.Core
{
    public class DecConfig
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
        public double MinAngle { get; set; }
        public double MaxAngle { get; set; }
        public double ElevationViewMin { get; set; }
        public double ElevationViewMax { get; set; }
        public double AngularResolution { get; set; }

        public Config GetConfig()
        {
            return new Config
            {
                Lat = Angle.FromDecimalDegrees(Lat),
                Lon = Angle.FromDecimalDegrees(Lon),
                MinAngle = Angle.FromDecimalDegrees(MinAngle),
                MaxAngle = Angle.FromDecimalDegrees(MaxAngle),
                ElevationViewMin = Angle.FromDecimalDegrees(ElevationViewMin),
                ElevationViewMax = Angle.FromDecimalDegrees(ElevationViewMax),
                AngularResolution = Angle.FromDecimalDegrees(AngularResolution),
            };
        }
    }
}
