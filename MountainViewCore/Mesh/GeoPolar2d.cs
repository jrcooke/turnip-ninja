using System;
using MountainView.Base;
using MountainView.Render;

namespace MountainView.Mesh
{
    public struct GeoPolar2d
    {
        public Angle Lat;
        public Angle Lon;

        public GeoPolar2d(Angle lat, Angle lon)
        {
            Lat = lat;
            Lon = lon;
        }

        public override string ToString()
        {
            return "(" + Lat + "," + Lon + ")";
        }

        internal Vector3f GetUnitVector()
        {
            var ret = new Vector3f()
            {
                X = (float)(Math.Cos(Lon.Radians) * Math.Sin(Lat.Radians)),
                Y = (float)(Math.Cos(Lon.Radians) * Math.Cos(Lat.Radians)),
                Z = (float)(Math.Sin(Lon.Radians)),
            };
            return ret;
        }
    }
}
