using MountainView.Base;
using System;

namespace MountainView.ChunkManagement
{
    public class StandardChunkMetadata : ChunkMetadata
    {
        private const int smallBatch = 540;
        public const int MaxZoomLevel = 16;

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

        public static long GetKey(long latTotal, long lonTotal, int zoomLevel)
        {
            if (zoomLevel > MaxZoomLevel)
            {
                throw new ArgumentOutOfRangeException("zoomLevel");
            }

            var sizeInFourths = 60L * 15 * 45 * (1 << (16 - zoomLevel)); //Math.Pow(2, 16 - zoomLevel));

            int numPossible = 2 << zoomLevel; //Math.Pow(2, 1 + zoomLevel);
            int sizeMultiplierLat = (latTotal >= 0 ? (int)(latTotal / sizeInFourths) : -1 - (int)((-latTotal) / sizeInFourths)); // Angle.FloorDivide(lat, sizeInFourths);
            int sizeMultiplierLon = (lonTotal >= 0 ? (int)(lonTotal / sizeInFourths) : -1 - (int)((-lonTotal) / sizeInFourths));

            int encodedLat = 3 * numPossible / 2 + sizeMultiplierLat;
            int encodedLon = 3 * numPossible / 2 + sizeMultiplierLon;
            long key = encodedLat + encodedLon * 0x100000000;
            return key;
        }

        public static StandardChunkMetadata GetRangeContaingPoint(Angle lat, Angle lon, int zoomLevel)
        {
            return GetRangeFromKey(GetKey(lat.Fourths, lon.Fourths, zoomLevel));
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

            var twoPixelSize = Angle.Divide(size, smallBatch / 2);
            Angle latLo = Angle.Subtract(Angle.Multiply(size, sizeMultiplierLat), twoPixelSize);
            Angle lonLo = Angle.Subtract(Angle.Multiply(size, sizeMultiplierLon), twoPixelSize);
            Angle latHi = Angle.Add(Angle.Multiply(size, sizeMultiplierLat + 1), twoPixelSize);
            Angle lonHi = Angle.Add(Angle.Multiply(size, sizeMultiplierLon + 1), twoPixelSize);
            StandardChunkMetadata ret = new StandardChunkMetadata(
                smallBatch + 5, smallBatch + 5,
                latLo, lonLo,
                latHi, lonHi,
                zoomLevel, key);

            return ret;
        }

        public static StandardChunkMetadata GetEmpty()
        {
            return new StandardChunkMetadata(0, 0, Angle.FromThirds(0), Angle.FromThirds(0), Angle.FromThirds(0), Angle.FromThirds(0), 0, 0);
        }

        public override string ToString()
        {
            return ZoomLevel + "Z_" + LatLo.ToLatString() + "," + LonLo.ToLonString() + "_" + LatHi.ToLatString() + "," + LonHi.ToLonString();
        }
    }
}
