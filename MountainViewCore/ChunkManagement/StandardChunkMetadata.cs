using MountainView.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MountainView.ChunkManagement
{
    public class StandardChunkMetadata : ChunkMetadata
    {
        public int ZoomLevel { get; private set; }
        public long Key { get; private set; }
        public int Version { get; private set; }

        private StandardChunkMetadata(int latSteps, int lonSteps,
            Angle latLo, Angle lonLo,
            Angle latHi, Angle lonHi,
            int zoomLevel, long key,
            int version) : base(latSteps, lonSteps, latLo, lonLo, latHi, lonHi)
        {
            this.ZoomLevel = zoomLevel;
            this.Key = key;
            this.Version = version;
        }

        public const int MaxZoomLevel = 8;
        private static readonly Angle[] pixelSizeForZoom = new Angle[] {
            Angle.FromMinutes(1), Angle.FromSeconds(20), Angle.FromSeconds(4),
            Angle.FromSeconds(1), Angle.FromThirds( 20), Angle.FromThirds( 4),
            Angle.FromThirds( 1), Angle.FromFourths(20), Angle.FromFourths(4),
        };

        private static readonly Dictionary<int, Angle[]> frameSizeForZoom = new Dictionary<int, Angle[]>
        {
            {
                1,
                new Angle[] {
                    Angle.FromDecimalDegrees(20), Angle.FromDecimalDegrees(4), Angle.FromDecimalDegrees(1),
                    Angle.FromMinutes(       20), Angle.FromMinutes(       4), Angle.FromMinutes(       1),
                    Angle.FromSeconds(       20), Angle.FromSeconds(       4), Angle.FromSeconds(       1),
                }
            },
            {
                2,
                new Angle[] {
                    Angle.FromDecimalDegrees(60), Angle.FromDecimalDegrees(20), Angle.FromDecimalDegrees(4),
                    Angle.FromMinutes(       60), Angle.FromMinutes(       20), Angle.FromMinutes(       4),
                    Angle.FromSeconds(       60), Angle.FromSeconds(       20), Angle.FromSeconds(       4),
                }
            },
        };

        private static readonly Dictionary<int, int[]> numPixelsForZoom = new Dictionary<int, int[]>
        {
            {
                1,
                new int[] {
                    1200, 720, 900,
                    1200, 720, 900,
                    1200, 720, 900,
                }
            },
            {
                2,
                new int[] {
                    3600, 3600, 3600,
                    3600, 3600, 3600,
                    3600, 3600, 3600,
                }
            },
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

        // Continental United States bounded by: 24N125W by 50N66W
        // So 26 degrees latitude, 59 degrees longitude
        private static readonly Angle usMinLat = Angle.FromDecimalDegrees(24.0);
        private static readonly Angle usMinLon = Angle.FromDecimalDegrees(-125.0);
        private static readonly Angle usDeltaLat = Angle.FromDecimalDegrees(26.0);
        private static readonly Angle usDeltaLon = Angle.FromDecimalDegrees(59.0);

        public static long GetKey(long latTotalIn, long lonTotalIn, int zoomLevel, int version)
        {
            Angle pixelSize = pixelSizeForZoom[zoomLevel];
            Angle frameSize = frameSizeForZoom[version][zoomLevel];
            int numPixels = numPixelsForZoom[version][zoomLevel];

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

        public static StandardChunkMetadata GetRangeFromKey(long key, int version)
        {
            int zoomLevel = (int)(key % (MaxZoomLevel + 1));

            Angle pixelSize = pixelSizeForZoom[zoomLevel];
            Angle frameSize = frameSizeForZoom[version][zoomLevel];
            int numPixels = numPixelsForZoom[version][zoomLevel];

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
                zoomLevel, key,
                version);

            return ret;
        }

        public static StandardChunkMetadata GetRangeContaingPoint(Angle lat, Angle lon, int zoomLevel, int version)
        {
            return GetRangeFromKey(GetKey(lat.Fourths, lon.Fourths, zoomLevel, version), version);
        }

        internal IEnumerable<StandardChunkMetadata> GetV1ChildChunks()
        {
            var ratio = Angle.FloorDivide(frameSizeForZoom[this.Version][this.ZoomLevel], frameSizeForZoom[this.Version][this.ZoomLevel + 1]);
            var latLoopLo = Angle.Add(this.LatLo, Angle.Divide(frameSizeForZoom[this.Version][this.ZoomLevel + 1], 2));
            var lonLoopLo = Angle.Add(this.LonLo, Angle.Divide(frameSizeForZoom[this.Version][this.ZoomLevel + 1], 2));

            List<StandardChunkMetadata> ret = new List<StandardChunkMetadata>();
            for (int i = 0; i < ratio; i++)
            {
                var li = Angle.Add(latLoopLo, Angle.Multiply(frameSizeForZoom[this.Version][this.ZoomLevel + 1], i));
                for (int j = 0; j < ratio; j++)
                {
                    var lj = Angle.Add(lonLoopLo, Angle.Multiply(frameSizeForZoom[this.Version][this.ZoomLevel + 1], j));
                    ret.Add(GetRangeContaingPoint(li, lj, this.ZoomLevel + 1, this.Version));
                }
            }

            return ret;

        }

        internal IEnumerable<StandardChunkMetadata> GetChildChunks()
        {
            var ratio = Angle.FloorDivide(frameSizeForZoom[this.Version][this.ZoomLevel], frameSizeForZoom[this.Version][this.ZoomLevel + 1]);
            var latLoopLo = Angle.Add(this.LatLo, Angle.Divide(frameSizeForZoom[this.Version][this.ZoomLevel + 1], 2));
            var lonLoopLo = Angle.Add(this.LonLo, Angle.Divide(frameSizeForZoom[this.Version][this.ZoomLevel + 1], 2));

            List<StandardChunkMetadata> ret = new List<StandardChunkMetadata>();
            for (int i = 0; i < ratio; i++)
            {
                var li = Angle.Add(latLoopLo, Angle.Multiply(frameSizeForZoom[this.Version][this.ZoomLevel + 1], i));
                for (int j = 0; j < ratio; j++)
                {
                    var lj = Angle.Add(lonLoopLo, Angle.Multiply(frameSizeForZoom[this.Version][this.ZoomLevel + 1], j));
                    ret.Add(GetRangeContaingPoint(li, lj, this.ZoomLevel + 1, this.Version));
                }
            }

            return ret;
        }

        internal StandardChunkMetadata GetParentChunk()
        {
            var latLoopCenter = Angle.Add(this.LatLo, Angle.Divide(this.LatDelta, 2));
            var lonLoopCenter = Angle.Add(this.LonLo, Angle.Divide(this.LonDelta, 2));
            return GetRangeContaingPoint(latLoopCenter, lonLoopCenter, this.ZoomLevel - 1, this.Version);
        }

        public override string ToString()
        {
            return ZoomLevel + "Z_" + LatLo.ToLatString() + "," + LonLo.ToLonString() + "_" + LatHi.ToLatString() + "," + LonHi.ToLonString();
        }

        public static Tuple<StandardChunkMetadata, bool> Parse(string x)
        {
            var parts = x.Split('.');
            var split1 = Math.Max(x.IndexOf('n'), x.IndexOf('s'));
            var split2 = Math.Max(x.IndexOf('w'), x.IndexOf('e'));

            var lat = Angle.Parse(x.Substring(0, split1 + 1));
            var lon = Angle.Parse(x.Substring(split1 + 1, split2 - split1));
            var zoom = int.Parse(parts[0].Substring(split2 + 1));

            var version = int.Parse(parts[1].Replace("v", "")) - 6;
            bool isImage = (parts[2][0] == 'i');

            var template = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, zoom, version);

            var ggg = isImage ?
                Imaging.Images.Current.GetFileName(template) :
                Elevation.Heights.Current.GetFileName(template);
            if (ggg != x)
            {
                throw new InvalidOperationException();
            }

            return new Tuple<StandardChunkMetadata, bool>(template, isImage);
        }
    }
}
