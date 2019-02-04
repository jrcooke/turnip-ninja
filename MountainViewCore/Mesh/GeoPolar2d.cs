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

        public GeoPolar2d(Angle lat, Angle lon)
        {
            Lat = lat;
            Lon = lon;
        }

        public override string ToString()
        {
            return "(" + Lat + "," + Lon + ")";
        }
    }
}
