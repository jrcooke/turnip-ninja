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
        public Angle PixelLatAngle { get; private set; }
        public Angle PixelLonAngle { get; private set; }

        public ChunkHolder(int width, int height, Angle latLo, Angle lonLo, Angle latHi, Angle lonHi)
        {
            this.Width = width;
            this.Height = height;
            this.LatLo = latLo;
            this.LonLo = lonLo;
            this.LatHi = latHi;
            this.LonHi = lonHi;
            this.LonDelta = Angle.Subtract(LonHi, LonLo);
            this.LatDelta = Angle.Subtract(LatHi, LatLo);
            this.PixelLatAngle = Angle.Divide(LatDelta, Width);
            this.PixelLonAngle = Angle.Divide(LonDelta, Height);
            Data = new T[width][];
            for (int i = 0; i < width; i++)
            {
                Data[i] = new T[height];
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
            this.LonDelta = Angle.Subtract(LonHi, LonLo);
            this.LatDelta = Angle.Subtract(LatHi, LatLo);
            this.PixelLatAngle = Angle.Divide(LatDelta, Width);
            this.PixelLonAngle = Angle.Divide(LonDelta, Height);
            this.Data = data;
        }


        internal ChunkHolder<T> GetSubChunk(Angle lat, Angle lon, Angle deltaLat, Angle deltaLon)
        {
            // Get a minute on all sides
            var lowerLatIndex = GetLatIndex(Angle.Add(lat, Angle.Multiply(deltaLat, -1)));
            var lowerLonIndex = GetLonIndex(Angle.Add(lon, Angle.Multiply(deltaLon, -1)));
            var upperLatIndex = GetLatIndex(Angle.Add(lat, deltaLat));
            var upperLonIndex = GetLonIndex(Angle.Add(lon, deltaLon));
            var lowerLatAngle = GetLat(lowerLatIndex);
            var lowerLonAngle = GetLon(lowerLonIndex);
            var upperLatAngle = GetLat(upperLatIndex);
            var upperLonAngle = GetLon(upperLonIndex);

            ChunkHolder<T> subChunk = new ChunkHolder<T>(
                upperLatIndex - lowerLatIndex + 1,
                upperLonIndex - lowerLonIndex + 1,
                lowerLatAngle, lowerLatAngle,
                upperLatAngle, upperLatAngle);
            for (int i = lowerLatIndex; i <= upperLatIndex; i++)
            {
                for (int j = lowerLonIndex; j <= upperLonIndex; j++)
                {
                    if (i >= Width || i < 0 || j < 0 || j >= Height)
                    {
                        subChunk.Data[i- lowerLatIndex][upperLonIndex - j] = default(T);
                    }
                    else
                    {
                        subChunk.Data[i - lowerLatIndex][upperLonIndex - j] = Data[i][Height - 1 - j];
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
            return Angle.Divide(curLatDelta, PixelLatAngle);
        }

        public int GetLonIndex(Angle lon)
        {
            var curLonDelta = Angle.Subtract(lon, LonLo);
            return Angle.Divide(curLonDelta, PixelLonAngle);
        }
    }
}
