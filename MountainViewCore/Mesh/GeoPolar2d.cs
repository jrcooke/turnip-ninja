using MountainView.Base;

namespace MountainView.Mesh
{
    public struct GeoPolar2d
    {
        public Angle Lat;
        public Angle Lon;
        public GeoPolar2d(double latDec, double lonDec)
        {
            Lat = Angle.FromDecimalDegrees(latDec);
            Lon = Angle.FromDecimalDegrees(lonDec);
        }
    }
}
