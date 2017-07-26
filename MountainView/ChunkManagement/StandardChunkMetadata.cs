using MountainView.Base;
using System;

namespace MountainView.ChunkManagement
{
    public class StandardChunkMetadata : ChunkMetadata
    {
        private const int smallBatch = 540;
        private const int maxZoom = 16;

        public int ZoomLevel { get; private set; }
        public long Key { get; private set; }

        private StandardChunkMetadata(int latSteps, int lonSteps,
            Angle latLo, Angle lonLo,
            Angle latHi, Angle lonHi,
            int zoomLevel, long key) : base(latSteps, lonSteps, latLo, lonLo, latHi, lonHi)
        {
            this.ZoomLevel = zoomLevel;
            this.Key = key;
        }

        public static StandardChunkMetadata GetRangeContaingPoint(Angle lat, Angle lon, int zoomLevel)
        {
            if (zoomLevel > maxZoom)
            {
                throw new ArgumentOutOfRangeException("zoomLevel");
            }

            var thirds = (int)(15 * 45 * Math.Pow(2, 16 - zoomLevel));
            Angle size = Angle.FromThirds(thirds);

            int numPossible = (int)Math.Pow(2, 1 + zoomLevel);
            int sizeMultiplierLat = Angle.FloorDivide(lat, size);
            int sizeMultiplierLon = Angle.FloorDivide(lon, size);

            int encodedLat = 3 * numPossible / 2 + sizeMultiplierLat;
            int encodedLon = 3 * numPossible / 2 + sizeMultiplierLon;
            long key = encodedLat + encodedLon * 0x100000000;
            StandardChunkMetadata ret = GetRangeFromKey(key);

            return ret;
        }

        public static StandardChunkMetadata GetRangeFromKey(long key)
        {
            int encodedLat = (int)(key % 0x100000000);
            int encodedLon = (int)(key / 0x100000000);

            int zoomLevel = (int)Math.Log(encodedLat, 2) - 1;
            int numPossible = (int)Math.Pow(2, 1 + zoomLevel);

            int sizeMultiplierLat = encodedLat - numPossible * 3 / 2;
            int sizeMultiplierLon = encodedLon - numPossible * 3 / 2;

            var thirds = (int)(15 * 45 * Math.Pow(2, 16 - zoomLevel));
            Angle size = Angle.FromThirds(thirds);

            Angle latLo = Angle.Multiply(size, sizeMultiplierLat);
            Angle lonLo = Angle.Multiply(size, sizeMultiplierLon);
            Angle latHi = Angle.Multiply(size, sizeMultiplierLat + 1);
            Angle lonHi = Angle.Multiply(size, sizeMultiplierLon + 1);
            StandardChunkMetadata ret = new StandardChunkMetadata(
                smallBatch + 1, smallBatch + 1,
                latLo, lonLo,
                latHi, lonHi,
                zoomLevel, key);

            return ret;
        }
    }
}
