using MountainView.Base;
using MountainViewDesktop.Interpolation;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

    public class NearestInterpolatingChunk<T> : IDisposable
    {
        private double latLo;
        private double lonLo;
        private double latHi;
        private double lonHi;
        private int numLat;
        private int numLon;
        private double scaleLat;
        private double scaleLon;
        private string container;
        private string fullFileName;
        private Func<MemoryStream, int, int, T> readPixel;
        private MemoryStream ms;
        private bool triedToGetMS;

        public NearestInterpolatingChunk(
            double latLo, double lonLo,
            double latHi, double lonHi,
            int numLat, int numLon,
            string container, string fullFileName,
            Func<MemoryStream, int, int, T> readPixel)
        {
            this.latLo = latLo;
            this.lonLo = lonLo;
            this.latHi = latHi;
            this.lonHi = lonHi;
            this.numLat = numLat;
            this.numLon = numLon;
            scaleLat = (numLat - 1.0) / (latHi - latLo);
            scaleLon = (numLon - 1.0) / (lonHi - lonLo);
            this.container = container;
            this.fullFileName = fullFileName;
            this.readPixel = readPixel;
        }

        public bool HasDataAtLat(double latDegree)
        {
            return latLo <= latDegree && latDegree <= latHi;
        }

        public bool HasDataAtLon(double lonDegree)
        {
            return lonLo <= lonDegree && lonDegree <= lonHi;
        }

        public bool TryGetDataAtPoint(double latDegree, double lonDegree, out T data)
        {
            if (!triedToGetMS)
            {
                var task = BlobHelper.TryGetStream(container, fullFileName);
                Task.WaitAll(task);
                ms = task.Result;
                triedToGetMS = true;
            }

            if (ms == null || !HasDataAtLat(latDegree) || !HasDataAtLon(lonDegree))
            {
                data = default(T);
                return false;
            }

            int i = (int)Math.Round(scaleLat * (latDegree - latLo));
            int j = numLon - 1 - (int)Math.Round(scaleLon * (lonDegree - lonLo));
            data = readPixel(ms, i, j);
            return true;
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing && ms != null) ms.Dispose();
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
