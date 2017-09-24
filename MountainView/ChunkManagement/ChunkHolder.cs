using MountainView.Base;
using MountainViewDesktop.Interpolation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MountainView.ChunkManagement
{
    public class ChunkHolder<T> : ChunkMetadata
    {
        public T[][] Data { get; private set; }
        private Func<T, double>[] toDouble;
        private Func<double[], T> fromDouble;

        public ChunkHolder(int latSteps, int lonSteps,
            Angle latLo, Angle lonLo,
            Angle latHi, Angle lonHi,
            Func<int, int, T> pixelGetter,
            Func<T, double>[] toDouble,
            Func<double[], T> fromDouble)
            : base(latSteps, lonSteps, latLo, lonLo, latHi, lonHi)
        {
            this.toDouble = toDouble;
            this.fromDouble = fromDouble;
            this.Data = new T[this.LatSteps][];
            for (int i = 0; i < this.LatSteps; i++)
            {
                this.Data[i] = new T[this.LonSteps];
                if (pixelGetter != null)
                {
                    for (int j = 0; j < this.LonSteps; j++)
                    {
                        this.Data[i][j] = pixelGetter(i, j);
                    }
                }
            }
        }

        internal void RenderChunksInto(IEnumerable<ChunkHolder<T>> chunks, Func<int, T, T, T> aggregate)
        {
            double[] buffer = new double[toDouble.Length];
            int[][] counter = new int[this.LatSteps][];
            for (int i = 0; i < this.LatSteps; i++)
            {
                counter[i] = new int[this.LonSteps];
            }

            foreach (var loopChunk in chunks.Where(p => p != null))
            {
                InterpolatingChunk<T> chunk2 = null;
                if (loopChunk.PixelSizeLatDeg > this.PixelSizeLatDeg ||
                    loopChunk.PixelSizeLonDeg > this.PixelSizeLonDeg)
                {
                    // Need to interpolate.
                    chunk2 = loopChunk.ComputeInterpolation(this.LatLo, this.LonLo, this.LatHi, this.LonHi, this.toDouble, this.fromDouble, InterpolatonType.Cubic);
                }

                for (int i = 0; i < this.LatSteps; i++)
                {
                    Angle loopLat = this.GetLat(i);
                    if (chunk2 == null)
                    {
                        if (!loopChunk.HasDataAtLat(loopLat))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (!chunk2.HasDataAtLat(loopLat.DecimalDegree))
                        {
                            continue;
                        }
                    }

                    for (int j = 0; j < this.LonSteps; j++)
                    {
                        Angle loopLon = this.GetLon(j);
                        if (chunk2 == null)
                        {
                            if (loopChunk.TryGetDataAtPoint(loopLat, loopLon, out T data))
                            {
                                this.Data[i][j] = aggregate(counter[i][j], this.Data[i][j], data);
                                counter[i][j]++;
                            }
                            else
                            {

                            }
                        }
                        else
                        {
                            if (chunk2.TryGetDataAtPoint(loopLat.DecimalDegree, loopLon.DecimalDegree, buffer, out T data))
                            {
                                this.Data[i][j] = aggregate(counter[i][j], this.Data[i][j], data);
                                counter[i][j]++;
                            }
                            else
                            {

                            }
                        }
                    }
                }
            }

            for (int i = 0; i < counter.Length; i++)
            {
                for (int j = 0; j < counter[i].Length; j++)
                {
                    if (counter[i][j] == 0)
                    {
                        throw new InvalidOperationException("The chunks do not cover the area of this chunk: " + this.ToString());
                    }
                }
            }
        }

        private InterpolatingChunk<T> ComputeInterpolation(
            Angle latLo, Angle lonLo,
            Angle latHi, Angle lonHi,
            Func<T, double>[] toDouble,
            Func<double[], T> fromDouble,
            InterpolatonType interpolatonType)
        {
            int iLo = GetLatIndex(latLo) - 2;
            int iHi = GetLatIndex(latHi) + 2;
            iLo = iLo < 0 ? 0 : iLo >= LatSteps ? LatSteps - 1 : iLo;
            iHi = iHi < 0 ? 0 : iHi >= LatSteps ? LatSteps - 1 : iHi;
            double areaLatLo = GetLat(iLo).DecimalDegree;
            double areaLatHi = GetLat(iHi).DecimalDegree;

            int jLo = GetLonIndex(lonHi) - 2;
            int jHi = GetLonIndex(lonLo) + 2;
            jLo = jLo < 0 ? 0 : jLo >= LonSteps ? LonSteps - 1 : jLo;
            jHi = jHi < 0 ? 0 : jHi >= LonSteps ? LonSteps - 1 : jHi;
            double areaLonLo = GetLon(jHi).DecimalDegree;
            double areaLonHi = GetLon(jLo).DecimalDegree;

            double[] lats = new double[iHi - iLo + 1];
            for (int i = 0; i < lats.Length; i++)
            {
                lats[i] = areaLatLo + i * (areaLatHi - areaLatLo) / (iHi - iLo);
            }

            double[] lons = new double[jHi - jLo + 1];
            for (int i = 0; i < lons.Length; i++)
            {
                lons[i] = areaLonHi + i * (areaLonLo - areaLonHi) / (jHi - jLo);
            }

            double[][][] values = new double[toDouble.Length][][];
            for (int k = 0; k < toDouble.Length; k++)
            {
                values[k] = new double[lats.Length][];
                for (int i = 0; i < lats.Length; i++)
                {
                    values[k][i] = new double[lons.Length];
                    for (int j = 0; j < lons.Length; j++)
                    {
                        values[k][i][j] = toDouble[k](Data[iLo + i][jLo + j]);
                    }
                }
            }

            return new InterpolatingChunk<T>(lats, lons, values, fromDouble, interpolatonType);
        }

        internal InterpolatingChunk<T> GetInterpolator(InterpolatonType interpolatonType)
        {
            return ComputeInterpolation(LatLo, LonLo, LatHi, LonHi, toDouble, fromDouble, interpolatonType);
        }

        public bool TryGetDataAtPoint(Angle lat, Angle lon, out T data)
        {
            if (HasDataAtLat(lat) && HasDataAtLon(lon))
            {
                data = Data[GetLatIndex(lat)][GetLonIndex(lon)];
                return true;
            }

            data = default(T);
            return false;
        }

        public bool HasDataAtLat(Angle lat)
        {
            int i = GetLatIndex(lat);
            return i >= 0 && i < LatSteps;
        }

        public bool HasDataAtLon(Angle lon)
        {
            int j = GetLonIndex(lon);
            return j >= 0 && j < LonSteps;
        }
    }
}
