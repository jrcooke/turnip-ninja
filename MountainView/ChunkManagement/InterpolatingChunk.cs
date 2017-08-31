using MountainView.Base;
using MountainViewDesktop.Interpolation;
using System;
using System.Linq;

namespace MountainView.ChunkManagement
{
    class InterpolatingChunk<T> // : IChunkPointAccessor<T>
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

        public bool HasDataAtLat(double latDegree)
        {
            return this.latLo <= latDegree && latDegree <= this.latHi;
        }

        public bool HasDataAtLon(double lonDegree)
        {
            return this.lonLo <= lonDegree && lonDegree <= this.lonHi;
        }

        public bool TryGetDataAtPoint(double latDegree, double lonDegree, out T data)
        {
            if (HasDataAtLat(latDegree) && HasDataAtLon(lonDegree))
            {
                if (interp.TryGetValue(latDegree, lonDegree, out double z))
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
