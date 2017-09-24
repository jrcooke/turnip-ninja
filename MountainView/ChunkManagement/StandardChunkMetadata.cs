using MountainView.Base;
using System.Collections.Generic;

namespace MountainView.ChunkManagement
{
    public class StandardChunkMetadata : ChunkMetadata
    {
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

        public const int MaxZoomLevel = 8;
        private static readonly Angle[] pixelSizeForZoom = new Angle[] {
            Angle.FromMinutes(1), Angle.FromSeconds(20), Angle.FromSeconds(4),
            Angle.FromSeconds(1), Angle.FromThirds( 20), Angle.FromThirds( 4),
            Angle.FromThirds( 1), Angle.FromFourths(20), Angle.FromFourths(4),
        };

        private static readonly Angle[] frameSizeForZoom = new Angle[] {
            Angle.FromDecimalDegrees(20), Angle.FromDecimalDegrees(4), Angle.FromDecimalDegrees(1),
            Angle.FromMinutes(       20), Angle.FromMinutes(       4), Angle.FromMinutes(       1),
            Angle.FromSeconds(       20), Angle.FromSeconds(       4), Angle.FromSeconds(       1),
        };

        private static readonly int[] numPixelsForZoom = new int[] {
            1200, 720, 900,
            1200, 720, 900,
            1200, 720, 900,
        };

        public static int GetZoomLevel(double decimalDegreesPerPixel)
        {
            for (int zoomLevel = 0; zoomLevel < MaxZoomLevel; zoomLevel++)
            {
                if (decimalDegreesPerPixel > pixelSizeForZoom[zoomLevel].DecimalDegree)
                {
                    return zoomLevel;
                }
            }

            return MaxZoomLevel;
        }

        // Continental unites states bounded by: 24N125W by 50N66W
        // So 26 degrees lat, 59 degrees lon
        private static readonly Angle usMinLat = Angle.FromDecimalDegrees(24.0);
        private static readonly Angle usMinLon = Angle.FromDecimalDegrees(-125.0);
        private static readonly Angle usDeltaLat = Angle.FromDecimalDegrees(26.0);
        private static readonly Angle usDeltaLon = Angle.FromDecimalDegrees(59.0);

        public static long GetKey(long latTotalIn, long lonTotalIn, int zoomLevel)
        {
            Angle pixelSize = pixelSizeForZoom[zoomLevel];
            Angle frameSize = frameSizeForZoom[zoomLevel];
            int numPixels = numPixelsForZoom[zoomLevel];

            var latTotal = latTotalIn - usMinLat.Fourths;
            var lonTotal = lonTotalIn - usMinLon.Fourths;

            int latNumPossible = (int)(usDeltaLat.Abs / frameSize.Abs);
            int lonNumPossible = (int)(usDeltaLon.Abs / frameSize.Abs);

            int sizeMultiplierLat = (latTotal >= 0 ? (int)(latTotal / frameSize.Fourths) : -1 - (int)((-latTotal) / frameSize.Fourths));
            int sizeMultiplierLon = (lonTotal >= 0 ? (int)(lonTotal / frameSize.Fourths) : -1 - (int)((-lonTotal) / frameSize.Fourths));

            int encodedLat = 3 * latNumPossible + sizeMultiplierLat;
            int encodedLon = 3 * lonNumPossible + sizeMultiplierLon;

            long key = (encodedLat + encodedLon * 0x100000000) * (MaxZoomLevel + 1) + zoomLevel;
            return key;
        }

        public static StandardChunkMetadata GetRangeFromKey(long key)
        {
            int zoomLevel = (int)(key % (MaxZoomLevel + 1));

            Angle pixelSize = pixelSizeForZoom[zoomLevel];
            Angle frameSize = frameSizeForZoom[zoomLevel];
            int numPixels = numPixelsForZoom[zoomLevel];

            int latNumPossible = (int)(usDeltaLat.Abs / frameSize.Abs);
            int lonNumPossible = (int)(usDeltaLon.Abs / frameSize.Abs);

            int encodedLat = (int)(key / (MaxZoomLevel + 1) % 0x100000000);
            int encodedLon = (int)(key / (MaxZoomLevel + 1) / 0x100000000);

            int sizeMultiplierLat = encodedLat - 3 * latNumPossible;
            int sizeMultiplierLon = encodedLon - 3 * lonNumPossible;

            Angle latLo = Angle.Add(Angle.Multiply(frameSize, sizeMultiplierLat), usMinLat);
            Angle lonLo = Angle.Add(Angle.Multiply(frameSize, sizeMultiplierLon), usMinLon);
            Angle latHi = Angle.Add(Angle.Multiply(frameSize, sizeMultiplierLat + 1), usMinLat);
            Angle lonHi = Angle.Add(Angle.Multiply(frameSize, sizeMultiplierLon + 1), usMinLon);
            StandardChunkMetadata ret = new StandardChunkMetadata(
                numPixels + 1, numPixels + 1,
                latLo, lonLo,
                latHi, lonHi,
                zoomLevel, key);

            return ret;
        }

        public static StandardChunkMetadata GetRangeContaingPoint(Angle lat, Angle lon, int zoomLevel)
        {
            return GetRangeFromKey(GetKey(lat.Fourths, lon.Fourths, zoomLevel));
        }

        internal IEnumerable<StandardChunkMetadata> GetChildChunks()
        {
            var ratio = Angle.FloorDivide(frameSizeForZoom[this.ZoomLevel], frameSizeForZoom[this.ZoomLevel + 1]);
            var latLoopLo = Angle.Add(this.LatLo, Angle.Divide(frameSizeForZoom[this.ZoomLevel + 1], 2));
            var lonLoopLo = Angle.Add(this.LonLo, Angle.Divide(frameSizeForZoom[this.ZoomLevel + 1], 2));

            List<StandardChunkMetadata> ret = new List<StandardChunkMetadata>();
            for (int i = 0; i < ratio; i++)
            {
                var li = Angle.Add(latLoopLo, Angle.Multiply(frameSizeForZoom[this.ZoomLevel + 1], i));
                for (int j = 0; j < ratio; j++)
                {
                    var lj = Angle.Add(lonLoopLo, Angle.Multiply(frameSizeForZoom[this.ZoomLevel + 1], j));
                    ret.Add(GetRangeContaingPoint(li, lj, this.ZoomLevel + 1));
                }
            }

            return ret;
        }

        internal StandardChunkMetadata GetParentChunk()
        {
            var latLoopCenter = Angle.Add(this.LatLo, Angle.Divide(this.LatDelta, 2));
            var lonLoopCenter = Angle.Add(this.LonLo, Angle.Divide(this.LonDelta, 2));
            return GetRangeContaingPoint(latLoopCenter, lonLoopCenter, this.ZoomLevel - 1);
        }

        public override string ToString()
        {
            return ZoomLevel + "Z_" + LatLo.ToLatString() + "," + LonLo.ToLonString() + "_" + LatHi.ToLatString() + "," + LonHi.ToLonString();
        }
    }
}
