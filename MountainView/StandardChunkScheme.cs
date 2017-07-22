using System;

namespace MountainView
{
    class StandardChunkScheme
    {
        protected const int smallBatch = 540;
        private const int maxZoom = 14;

        private static Angle GetChunkSize(int zoomLevel)
        {
            var thirds = (int)(60 * 45 * Math.Pow(2, 14 - zoomLevel));
            return Angle.FromThirds(thirds);
        }

        public static ChunkMetadata GetRangeContaingPoint(Angle lat, Angle lon, int zoomLevel)
        {
            if (zoomLevel > maxZoom || zoomLevel < 4)
            {
                throw new ArgumentOutOfRangeException("zoomLevel");
            }

            Angle size = GetChunkSize(zoomLevel);
            Angle latLo = Angle.Subtract(Angle.Multiply(size, Angle.Divide(Angle.Add(lat, Angle.Whole), size)), Angle.Whole);
            Angle lonLo = Angle.Subtract(Angle.Multiply(size, Angle.Divide(Angle.Add(lon, Angle.Whole), size)), Angle.Whole);
            Angle latHi = Angle.Add(latLo, size);
            Angle lonHi = Angle.Add(lonLo, size);
            ChunkMetadata ret = new ChunkMetadata(smallBatch + 1, smallBatch + 1, latLo, lonLo, latHi, lonHi);
            return ret;
        }
    }
}
