using System;

namespace MountainView
{
    public class ChunkHolder<T>
    {
        public T[][] Data { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Angle LatLo { get; private set; }
        public Angle LonLo { get; private set; }
        public Angle LatHi { get; private set; }
        public Angle LonHi { get; private set; }
        public Angle LonDelta { get; private set; }
        public Angle LatDelta { get; private set; }
        public Angle PixelSizeLat { get; private set; }
        public Angle PixelSizeLon { get; private set; }

        public ChunkHolder(int width, int height, Angle latLo, Angle lonLo, Angle latHi, Angle lonHi, Func<int, int, T> pixelGetter = null)
        {
            this.Width = width;
            this.Height = height;
            this.LatLo = latLo;
            this.LonLo = lonLo;
            this.LatHi = latHi;
            this.LonHi = lonHi;
            this.LatDelta = Angle.Subtract(LatHi, LatLo);
            this.LonDelta = Angle.Subtract(LonHi, LonLo);
            this.PixelSizeLat = Angle.Divide(LatDelta, Width);
            this.PixelSizeLon = Angle.Divide(LonDelta, Height);
            Data = new T[Width][];
            for (int i = 0; i < Width; i++)
            {
                Data[i] = new T[Height];
                if (pixelGetter != null)
                {
                    for (int j = 0; j < Height; j++)
                    {
                        Data[i][j] = pixelGetter(i, j);
                    }
                }
            }
        }

        public ChunkHolder(T[][] data, Angle latLo, Angle lonLo, Angle latHi, Angle lonHi)
        {
            this.Width = data.Length;
            this.Height = data[0].Length;
            this.LatLo = latLo;
            this.LonLo = lonLo;
            this.LatHi = latHi;
            this.LonHi = lonHi;
            this.LatDelta = Angle.Subtract(LatHi, LatLo);
            this.LonDelta = Angle.Subtract(LonHi, LonLo);
            this.PixelSizeLat = Angle.Divide(LatDelta, Width);
            this.PixelSizeLon = Angle.Divide(LonDelta, Height);
            this.Data = data;
        }

        //internal ChunkHolder<T> GetSubChunk(Angle lat, Angle lon, Angle deltaLat, Angle deltaLon)
        //{
        //    var lowerLatIndex = GetLatIndex(Angle.Add(lat, Angle.Divide(Angle.Multiply(deltaLat, -1), 2)));
        //    var lowerLonIndex = GetLonIndex(Angle.Add(lon, Angle.Divide(Angle.Multiply(deltaLon, -1), 2)));
        //    var upperLatIndex = GetLatIndex(Angle.Add(lat, Angle.Divide(deltaLat, 2)));
        //    var upperLonIndex = GetLonIndex(Angle.Add(lon, Angle.Divide(deltaLon, 2)));
        //    ChunkHolder<T> subChunk = new ChunkHolder<T>(
        //        upperLatIndex - lowerLatIndex,
        //        upperLonIndex - lowerLonIndex,
        //        GetLat(lowerLatIndex), GetLon(lowerLonIndex),
        //        GetLat(upperLatIndex), GetLon(upperLonIndex));
        //    for (int i = lowerLatIndex; i < upperLatIndex; i++)
        //    {
        //        for (int j = lowerLonIndex; j < upperLonIndex; j++)
        //        {
        //            if (i > Width || i < 0 || j < 0 || j > Height)
        //            {
        //                subChunk.Data[i - lowerLatIndex][upperLonIndex - 1 - j] = default(T);
        //            }
        //            else
        //            {
        //                subChunk.Data[i - lowerLatIndex][upperLonIndex - 1 - j] = Data[i][Height - 1 - j];
        //            }
        //        }
        //    }

        //    return subChunk;
        //}

        internal ChunkHolder<T> RenderSubChunk(
            Angle lat, Angle lon,
            Angle deltaLat, Angle deltaLon,
            Angle pixelSizeLat, Angle pixelSizeLon)
        {
            ChunkHolder<T> subChunk = new ChunkHolder<T>(
                Angle.Divide(deltaLat, pixelSizeLat),
                Angle.Divide(deltaLon, pixelSizeLon),
                Angle.Add(lat, Angle.Divide(Angle.Multiply(deltaLat, -1), 2)),
                Angle.Add(lon, Angle.Divide(Angle.Multiply(deltaLon, -1), 2)),
                Angle.Add(lat, Angle.Divide(deltaLat, 2)),
                Angle.Add(lon, Angle.Divide(deltaLon, 2)));

            //int[][] subChunk2 = new int[width][];
            //for (int i = 0; i < width; i++)
            //{
            //    subChunk2[i] = new int[height];
            //}

            for (int i = 0; i < subChunk.Width; i++)
            {
                int iPrime = this.GetLatIndex(subChunk.GetLat(i));
                if (iPrime >= 0 && iPrime < this.Width)
                {
                    for (int j = 0; j < subChunk.Height; j++)
                    {
                        int jPrime = this.GetLonIndex(subChunk.GetLon(j));
                        if (jPrime >= 0 && jPrime < this.Height)
                        {
                            subChunk.Data[i][subChunk.Height - 1 - j] = this.Data[iPrime][this.Height - 1 - jPrime];
                        }
                    }
                }
            }

            return subChunk;
        }

        public Angle GetLat(int i)
        {
            return Angle.Add(LatLo, Angle.Divide(Angle.Multiply(LatDelta, i), Width));
        }

        public Angle GetLon(int j)
        {
            return Angle.Add(LonLo, Angle.Divide(Angle.Multiply(LonDelta, j), Height));
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
}
