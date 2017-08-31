using MountainView.Base;
using MountainViewDesktop.Interpolation;
using System;
using System.Linq;

namespace MountainView.ChunkManagement
{
    class InterpolatingChunk<T> : IChunkPointAccessor<T>
    {
        private double latLo;
        private double lonLo;
        private double latHi;
        private double lonHi;
        private Func<double, T> fromDouble;
        private TwoDInterpolator interp;

        public InterpolatingChunk(
            double[] lats,
            double[] lons,
            double[][] values,
            Func<double, T> fromDouble,
            InterpolatonType interpolatonType)
        {
            this.latLo = lats.Min();
            this.lonLo = lons.Min();
            this.latHi = lats.Max();
            this.lonHi = lons.Max();
            this.fromDouble = fromDouble;
            this.interp = new TwoDInterpolator(lats, lons, values, interpolatonType);
        }

        public bool HasDataAtLat(Angle lat)
        {
            return this.latLo <= lat.DecimalDegree && lat.DecimalDegree <= this.latHi;
        }

        public bool HasDataAtLon(Angle lon)
        {
            return this.lonLo <= lon.DecimalDegree && lon.DecimalDegree <= this.lonHi;
        }

        public bool TryGetDataAtPoint(Angle lat, Angle lon, out T data)
        {
            if (HasDataAtLat(lat) && HasDataAtLon(lon))
            {
                if (interp.TryGetValue(lat.DecimalDegree, lon.DecimalDegree, out double z))
                {
                    data = fromDouble(z);
                    return true;
                }
            }

            data = default(T);
            return false;
        }
    }
}
