using MountainView.Base;
using System;

namespace MountainView.ChunkManagement
{
    public class StandardChunkMetadata : ChunkMetadata
    {
        private const int smallBatch = 540;
        private const int maxZoom = 16;

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

            var thirds = (int)(15  * 45 * Math.Pow(2, 16 - zoomLevel));
            Angle size = Angle.FromThirds(thirds);
            Angle latLo = Angle.Subtract(Angle.Multiply(size, Angle.Divide(Angle.Add(lat, Angle.Whole), size)), Angle.Whole);
            Angle lonLo = Angle.Subtract(Angle.Multiply(size, Angle.Divide(Angle.Add(lon, Angle.Whole), size)), Angle.Whole);
            Angle latHi = Angle.Add(latLo, size);
            Angle lonHi = Angle.Add(lonLo, size);
            StandardChunkMetadata ret = new StandardChunkMetadata(smallBatch + 1, smallBatch + 1, latLo, lonLo, latHi, lonHi, zoomLevel);
            return ret;
        }
    }
}
