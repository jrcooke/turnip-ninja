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

        protected Angle GetLat(int i)
        {
            return Angle.Add(LatLo, Angle.Divide(Angle.Multiply(LatDelta, i), LatSteps));
        }

        protected Angle GetLon(int j)
        {
            return Angle.Add(LonLo, Angle.Divide(Angle.Multiply(LonDelta, j), LonSteps));
        }

        protected int GetLatIndex(Angle lat)
        {
            var curLatDelta = Angle.Subtract(lat, LatLo);
            return Angle.Divide(curLatDelta, PixelSizeLat);
        }

        protected int GetLonIndex(Angle lon)
        {
            var curLonDelta = Angle.Subtract(lon, LonLo);
            return Angle.Divide(curLonDelta, PixelSizeLon);
        }
    }

    public class StandardChunkMetadata : ChunkMetadata
    {
        private const int smallBatch = 540;
        private const int maxZoom = 14;

        public int ZoomLevel { get; private set; }

        private StandardChunkMetadata(int latSteps, int lonSteps,
            Angle latLo, Angle lonLo,
            Angle latHi, Angle lonHi,
            int zoomLevel) : base(latSteps, lonSteps, latLo, lonLo, latHi, lonHi)
        {
            this.ZoomLevel = zoomLevel;
        }

        public static StandardChunkMetadata GetRangeContaingPoint(Angle lat, Angle lon, int zoomLevel)
        {
            if (zoomLevel > maxZoom || zoomLevel < 4)
            {
                throw new ArgumentOutOfRangeException("zoomLevel");
            }

            var thirds = (int)(60 * 45 * Math.Pow(2, 14 - zoomLevel));
            Angle size = Angle.FromThirds(thirds);
            Angle latLo = Angle.Subtract(Angle.Multiply(size, Angle.Divide(Angle.Add(lat, Angle.Whole), size)), Angle.Whole);
            Angle lonLo = Angle.Subtract(Angle.Multiply(size, Angle.Divide(Angle.Add(lon, Angle.Whole), size)), Angle.Whole);
            Angle latHi = Angle.Add(latLo, size);
            Angle lonHi = Angle.Add(lonLo, size);
            StandardChunkMetadata ret = new StandardChunkMetadata(smallBatch + 1, smallBatch + 1, latLo, lonLo, latHi, lonHi, zoomLevel);
            return ret;
        }
    }

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

        internal void RenderSubChunkInto(
            ChunkHolder<T> chunk,
            //Angle lat, Angle lon,
            //Angle deltaLat, Angle deltaLon,
            //Angle pixelSizeLat, Angle pixelSizeLon,
            Func<int, T, T, T> aggregate)
        {
            if (aggregate == null)
            {
                aggregate = (i, a, b) => b;
            }

            //ChunkHolder<T> subChunk = new ChunkHolder<T>(
            //    Angle.Divide(deltaLat, pixelSizeLat),
            //    Angle.Divide(deltaLon, pixelSizeLon),
            //    Angle.Add(lat, Angle.Divide(deltaLat, -2)), Angle.Add(lon, Angle.Divide(deltaLon, -2)),
            //    Angle.Add(lat, Angle.Divide(deltaLat, +2)), Angle.Add(lon, Angle.Divide(deltaLon, +2)));

            Console.WriteLine("Source pixel size:" + chunk.PixelSizeLat.ToLatString() + ", " + chunk.PixelSizeLon.ToLonString());
            Console.WriteLine("Dest   pixel size:" + this.PixelSizeLat.ToLatString() + ", " + this.PixelSizeLon.ToLonString());

            if (chunk.PixelSizeLat.DecimalDegree < this.PixelSizeLat.DecimalDegree &&
                chunk.PixelSizeLon.DecimalDegree < this.PixelSizeLon.DecimalDegree)
            {
                Console.WriteLine("Will need to aggregate");
            }
            else if (chunk.PixelSizeLat.DecimalDegree > this.PixelSizeLat.DecimalDegree &&
                 chunk.PixelSizeLon.DecimalDegree > this.PixelSizeLon.DecimalDegree)
            {
                Console.WriteLine("Will need to interpolate");
            }

            int[][] countAccumulatedPoints = new int[this.LatSteps][];
            for (int i = 0; i < this.LatSteps; i++)
            {
                countAccumulatedPoints[i] = new int[this.LonSteps];
                int iPrime = chunk.GetLatIndex(this.GetLat(i));
                if (iPrime >= 0 && iPrime < chunk.LatSteps)
                {
                    for (int j = 0; j < this.LonSteps; j++)
                    {
                        int jPrime = chunk.LonSteps - 1 - chunk.GetLonIndex(this.GetLon(this.LonSteps - 1 - j));
                        if (jPrime >= 0 && jPrime < chunk.LonSteps)
                        {
                            if (countAccumulatedPoints[i][j] > 0)
                            {
                                this.Data[i][j] = aggregate(
                                    countAccumulatedPoints[i][j],
                                    this.Data[i][j],
                                    chunk.Data[iPrime][jPrime]);
                            }
                            else
                            {
                                this.Data[i][j] = chunk.Data[iPrime][jPrime];
                            }

                            countAccumulatedPoints[i][j]++;
                        }
                    }
                }
            }
        }

        internal void RenderChunksInto(
            IEnumerable<ChunkHolder<T>> chunks,
            Func<int, T, T, T> aggregate = null)
        {
            if (aggregate == null)
            {
                aggregate = (i, a, b) => b;
            }

            int[][] countAccumulatedPoints = new int[this.LatSteps][];
            for (int i = 0; i < this.LatSteps; i++)
            {
                countAccumulatedPoints[i] = new int[this.LonSteps];
            }

            foreach (var chunk in chunks)
            {
                if (chunk != null)
                {
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
                                    if (countAccumulatedPoints[i][j] > 0)
                                    {
                                        this.Data[i][j] = aggregate(
                                            countAccumulatedPoints[i][j],
                                            this.Data[i][j],
                                            chunk.Data[iPrime][jPrime]);
                                    }
                                    else
                                    {
                                        this.Data[i][j] = chunk.Data[iPrime][jPrime];
                                    }

                                    countAccumulatedPoints[i][j]++;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
