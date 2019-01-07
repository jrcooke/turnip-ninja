using System;

namespace MountainView.Mesh
{
    public struct GeoPolar3d
    {
        public double Lat;
        public double Lon;
        public double Height;

        public GeoPolar3d(double lat, double lon, double height)
        {
            Lat = lat;
            Lon = lon;
            Height = height;
        }
    }
}
