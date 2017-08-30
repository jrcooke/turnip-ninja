using MountainView.Base;
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

        public InterpolatingChunk(double[] lats, double[] lons, double[][] values,
            Func<double, T> fromDouble)
        {
            this.latLo = lats.Min();
            this.lonLo = lons.Min();
            this.latHi = lats.Max();
            this.lonHi = lons.Max();
            this.fromDouble = fromDouble;
            this.interp = new TwoDInterpolator(lats, lons, values);
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
                double z;
                if (interp.TryGetValue(lat.DecimalDegree, lon.DecimalDegree, out z))
                {
                    data = fromDouble(z);
                    return true;
                }
            }

            data = default(T);
            return false;
        }

        internal InterpolatingChunk<T> GetInterpolatorForLine(Angle latLo, Angle lonLo, Angle latHi, Angle lonHi)
        {
            if (latLo.DecimalDegree > latHi.DecimalDegree)
            {
                Angle.Swap(ref latLo, ref latHi);
            }

            if (lonLo.DecimalDegree > lonHi.DecimalDegree)
            {
                Angle.Swap(ref lonLo, ref lonHi);
            }

            var laL = Angle.FromDecimalDegrees(this.latLo);
            var loL = Angle.FromDecimalDegrees(this.lonLo);
            var laH = Angle.FromDecimalDegrees(this.latHi);
            var loH = Angle.FromDecimalDegrees(this.lonHi);

            if (this.latLo > latHi.DecimalDegree || this.latHi < latLo.DecimalDegree ||
                this.lonLo > lonHi.DecimalDegree || this.lonHi < lonLo.DecimalDegree)
            {
                return null;
            }
            else
            {
                var overlapLatLo = Math.Min(this.latLo, latLo.DecimalDegree);
                var overlapLonLo = Math.Min(this.lonLo, lonLo.DecimalDegree);
                var overlapLatHi = Math.Min(this.latHi, latHi.DecimalDegree);
                var overlapLonHi = Math.Min(this.lonHi, lonHi.DecimalDegree);
                return this;
                //return interp.GetInterpolatorForLine(
                //    Math.Min(this.latLo, latLo.DecimalDegree),
                //    Math.Min(this.lonLo, lonLo.DecimalDegree),
                //    Math.Min(this.latHi, latHi.DecimalDegree),
                //    Math.Min(this.lonHi, lonHi.DecimalDegree));
            }
        }
    }
}
