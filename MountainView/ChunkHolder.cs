using System;
using System.Collections.Generic;

namespace MountainView
{
    public class ChunkMetadata
    {
        public int LatSteps { get; private set; }
        public int LonSteps { get; private set; }
        public Angle LatLo { get; private set; }
        public Angle LonLo { get; private set; }
        public Angle LatHi { get; private set; }
        public Angle LonHi { get; private set; }
        public Angle LonDelta { get; private set; }
        public Angle LatDelta { get; private set; }
        public Angle PixelSizeLat { get; private set; }
        public Angle PixelSizeLon { get; private set; }

        public ChunkMetadata(int latSteps, int lonSteps, Angle latLo, Angle lonLo, Angle latHi, Angle lonHi)
        {
            this.LatSteps = latSteps;
            this.LonSteps = lonSteps;
            this.LatLo = latLo;
            this.LonLo = lonLo;
            this.LatHi = latHi;
            this.LonHi = lonHi;
            this.LatDelta = Angle.Subtract(LatHi, LatLo);
            this.LonDelta = Angle.Subtract(LonHi, LonLo);
            this.PixelSizeLat = Angle.Divide(LatDelta, LatSteps);
            this.PixelSizeLon = Angle.Divide(LonDelta, LonSteps);
        }

        public Angle GetLat(int i)
        {
            return Angle.Add(LatLo, Angle.Divide(Angle.Multiply(LatDelta, i), LatSteps));
        }

        public Angle GetLon(int j)
        {
            return Angle.Add(LonLo, Angle.Divide(Angle.Multiply(LonDelta, j), LonSteps));
        }

        public int GetLatIndex(Angle lat)
        {
            var curLatDelta = Angle.Subtract(lat, LatLo);
            return Angle.Divide(curLatDelta, PixelSizeLat);
        }

        public int GetLonIndex(Angle lon)
        {
            var curLonDelta = Angle.Subtract(lon, LonLo);
            return Angle.Divide(curLonDelta, PixelSizeLon);
        }
    }
    public class ChunkHolder<T> : ChunkMetadata
    {
        public T[][] Data { get; private set; }

        public ChunkHolder(long latSteps, long lonSteps, Angle latLo, Angle lonLo, Angle latHi, Angle lonHi, Func<int, int, T> pixelGetter = null) : base(
            (int)latSteps, (int)lonSteps, latLo, lonLo, latHi, lonHi)
        {
        }

        public ChunkHolder(int latSteps, int lonSteps, Angle latLo, Angle lonLo, Angle latHi, Angle lonHi, Func<int, int, T> pixelGetter = null) : base(latSteps, lonSteps, latLo, lonLo, latHi, lonHi)
        {
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

        internal ChunkHolder<T> RenderSubChunk(
            Angle lat, Angle lon,
            Angle deltaLat, Angle deltaLon,
            Angle pixelSizeLat, Angle pixelSizeLon,
            Func<int, T, T, T> aggregate = null)
        {
            if (aggregate == null)
            {
                aggregate = (i, a, b) => b;
            }

            ChunkHolder<T> subChunk = new ChunkHolder<T>(
                Angle.Divide(deltaLat, pixelSizeLat),
                Angle.Divide(deltaLon, pixelSizeLon),
                Angle.Add(lat, Angle.Divide(deltaLat, -2)), Angle.Add(lon, Angle.Divide(deltaLon, -2)),
                Angle.Add(lat, Angle.Divide(deltaLat, +2)), Angle.Add(lon, Angle.Divide(deltaLon, +2)));

            Console.WriteLine("Source pixel size:" + this.PixelSizeLat.ToLatString() + ", " + this.PixelSizeLon.ToLonString());
            Console.WriteLine("Dest   pixel size:" + subChunk.PixelSizeLat.ToLatString() + ", " + subChunk.PixelSizeLon.ToLonString());

            if (this.PixelSizeLat.DecimalDegree < subChunk.PixelSizeLat.DecimalDegree &&
                this.PixelSizeLon.DecimalDegree < subChunk.PixelSizeLon.DecimalDegree)
            {
                Console.WriteLine("Will need to aggregate");
            }
            else if (this.PixelSizeLat.DecimalDegree > subChunk.PixelSizeLat.DecimalDegree &&
                 this.PixelSizeLon.DecimalDegree > subChunk.PixelSizeLon.DecimalDegree)
            {
                Console.WriteLine("Will need to interpolate");
            }

            int[][] subChunk2 = new int[subChunk.LatSteps][];
            for (int i = 0; i < subChunk.LatSteps; i++)
            {
                subChunk2[i] = new int[subChunk.LonSteps];
                int iPrime = this.GetLatIndex(subChunk.GetLat(i));
                if (iPrime >= 0 && iPrime < this.LatSteps)
                {
                    for (int j = 0; j < subChunk.LonSteps; j++)
                    {
                        int jPrime = this.LonSteps - 1 - this.GetLonIndex(subChunk.GetLon(subChunk.LonSteps - 1 - j));
                        if (jPrime >= 0 && jPrime < this.LonSteps)
                        {
                            if (subChunk2[i][j] > 0)
                            {
                                subChunk.Data[i][j] = aggregate(
                                    subChunk2[i][j],
                                    subChunk.Data[i][j],
                                    this.Data[iPrime][jPrime]);
                            }
                            else
                            {
                                subChunk.Data[i][j] = this.Data[iPrime][jPrime];
                            }

                            subChunk2[i][j]++;
                        }
                    }
                }
            }

            return subChunk;
        }


        internal static void RenderChunksInto(
            IEnumerable<ChunkHolder<T>> chunks,
            ChunkHolder<T> target,
            Func<int, T, T, T> aggregate = null)
        {
            if (aggregate == null)
            {
                aggregate = (i, a, b) => b;
            }

            ChunkHolder<T> subChunk = target;

            int[][] subChunk2 = new int[subChunk.LatSteps][];
            for (int i = 0; i < subChunk.LatSteps; i++)
            {
                subChunk2[i] = new int[subChunk.LonSteps];
            }

            foreach(var chunk in chunks)
            {
                for (int i = 0; i < subChunk.LatSteps; i++)
                {
                    int iPrime = chunk.GetLatIndex(subChunk.GetLat(i));
                    if (iPrime >= 0 && iPrime < chunk.LatSteps)
                    {
                        for (int j = 0; j < subChunk.LonSteps; j++)
                        {
                            int jPrime = chunk.LonSteps - 1 - chunk.GetLonIndex(subChunk.GetLon(subChunk.LonSteps - 1 - j));
                            if (jPrime >= 0 && jPrime < chunk.LonSteps)
                            {
                                if (subChunk2[i][j] > 0)
                                {
                                    subChunk.Data[i][j] = aggregate(
                                        subChunk2[i][j],
                                        subChunk.Data[i][j],
                                        chunk.Data[iPrime][jPrime]);
                                }
                                else
                                {
                                    subChunk.Data[i][j] = chunk.Data[iPrime][jPrime];
                                }

                                subChunk2[i][j]++;
                            }
                        }
                    }
                }
            }
        }

    }
}
