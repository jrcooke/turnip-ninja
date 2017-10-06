using MountainViewDesktop.Interpolation;
using System;
using System.Linq;

namespace MountainView.ChunkManagement
{
    public class InterpolatingChunk<T>
    {
        private double latLo;
        private double lonLo;
        private double latHi;
        private double lonHi;
        private Func<double[], T> fromDouble;
        private TwoDInterpolator[] interp;

        public InterpolatingChunk(
            double[] lats,
            double[] lons,
            double[][][] values,
            Func<double[], T> fromDouble,
            InterpolatonType interpolatonType)
        {
            this.latLo = lats.Min();
            this.lonLo = lons.Min();
            this.latHi = lats.Max();
            this.lonHi = lons.Max();
            this.fromDouble = fromDouble;
            this.interp = new TwoDInterpolator[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                this.interp[i] = new TwoDInterpolator(lats, lons, values[i], interpolatonType);
            }
        }

        public bool HasDataAtLat(double latDegree)
        {
            return this.latLo <= latDegree && latDegree <= this.latHi;
        }

        public bool HasDataAtLon(double lonDegree)
        {
            return this.lonLo <= lonDegree && lonDegree <= this.lonHi;
        }

        public bool TryGetDataAtPoint(double latDegree, double lonDegree, double[] buffer, out T data)
        {
            if (HasDataAtLat(latDegree) && HasDataAtLon(lonDegree))
            {
                for (int i = 0; i < interp.Length; i++)
                {
                    if (!interp[i].TryGetValue(latDegree, lonDegree, out double z))
                    {
                        data = default(T);
                        return false;
                    }

                    buffer[i] = z;
                }

                data = fromDouble(buffer);
                return true;
            }

            data = default(T);
            return false;
        }
    }

    public class NearestInterpolatingChunk<T>
    {
        private double latLo;
        private double lonLo;
        private double latHi;
        private double lonHi;
        private T[][] values;
        private int numLat;
        private int numLon;
        private double scaleLat;
        private double scaleLon;

        public NearestInterpolatingChunk(
            double latLo, double lonLo,
            double latHi, double lonHi,
            T[][] values)
        {
            this.latLo = latLo;
            this.lonLo = lonLo;
            this.latHi = latHi;
            this.lonHi = lonHi;
            this.values = values;
            this.numLat = values.Length;
            this.numLon = values[0].Length;
            this.scaleLat = (numLat - 1.0) / (latHi - latLo);
            this.scaleLon = (numLon - 1.0) / (lonHi - lonLo);
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
                int i = (int)Math.Round(scaleLat * (latDegree - latLo));
                int j = numLon - 1 - (int)Math.Round(scaleLon * (lonDegree - lonLo));
                data = values[i][j];
                return true;
            }

            data = default(T);
            return false;
        }

    }
}
