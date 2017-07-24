using System;
using System.Collections.Generic;
using System.Linq;

namespace MountainView.ChunkManagement
{
    public class ChunkHolder<T> : ChunkMetadata
    {
        public T[][] Data { get; private set; }

        public ChunkHolder(int latSteps, int lonSteps,
            Angle latLo, Angle lonLo,
            Angle latHi, Angle lonHi,
            Func<int, int, T> pixelGetter = null) : base(latSteps, lonSteps, latLo, lonLo, latHi, lonHi)
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

        internal void RenderChunksInto(IEnumerable<ChunkHolder<T>> chunks, Func<int, T, T, T> aggregate)
        {
            int[][] countAccumulatedPoints = new int[this.LatSteps][];
            for (int i = 0; i < this.LatSteps; i++)
            {
                countAccumulatedPoints[i] = new int[this.LonSteps];
            }

            foreach (var chunk in chunks.Where(p => p != null))
            {
                if (chunk.PixelSizeLat.DecimalDegree > this.PixelSizeLat.DecimalDegree ||
                    chunk.PixelSizeLon.DecimalDegree > this.PixelSizeLon.DecimalDegree)
                {
                    Console.WriteLine("Source pixel size:" + chunk.PixelSizeLat.ToLatString() + ", " + chunk.PixelSizeLon.ToLonString());
                    Console.WriteLine("Dest   pixel size:" + this.PixelSizeLat.ToLatString() + ", " + this.PixelSizeLon.ToLonString());
                    Console.WriteLine("Will need to interpolate");
                }

                for (int i = 0; i < this.LatSteps; i++)
                {
                    int iPrime = chunk.GetLatIndex(this.GetLat(i));
                    if (iPrime >= 0 && iPrime < chunk.LatSteps)
                    {
                        for (int j = 0; j < this.LonSteps; j++)
                        {
                            int jPrime = chunk.LonSteps - 1 - chunk.GetLonIndex(this.GetLon(this.LonSteps - 1 - j));
                            if (jPrime >= 0 && jPrime < chunk.LonSteps)
                            {
                                this.Data[i][j] = aggregate(
                                    countAccumulatedPoints[i][j],
                                    this.Data[i][j],
                                    chunk.Data[iPrime][jPrime]);
                                countAccumulatedPoints[i][j]++;
                            }
                        }
                    }
                }
            }
        }
    }
}
