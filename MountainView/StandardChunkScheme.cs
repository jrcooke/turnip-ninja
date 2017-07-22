
using System;
using System.Collections.Generic;
using System.Text;

namespace MountainView
{
    class StandardChunkScheme
    {
        protected const int smallBatch = 540; // Number of 1/3 arc seconds per 3 minutes.
        private const int maxZoom = 14;

        private static Angle GetChunkSize(int zoomLevel)
        {
            var thirds = (int)(3 * 60 * 15 * Math.Pow(2, 14 - zoomLevel));
            return Angle.FromThirds(thirds);
        }

        public static int GetZoomLevel(Angle chunkSize)
        {
            return (int)(14.0 - Math.Log(chunkSize.DecimalDegree * 80, 2));
        }

        //public static int GetZoomLevel(double lat, double lon, double metersPerElement)
        //{
        //    // Size of a degree of lon here
        //    var len = Utils.LengthOfLatDegree * Math.Cos(Math.PI * lat / 180.0);

        //    // Chunks are Size minutes across, with SmallBatch elements.
        //    // So elements are Size / SmallBatch minutes large.
        //    // The length of smallest size of an element in meters is
        //    //     Size / SmallBatch / 60 degrees * LenOfDegree * cosLat.
        //    // Setting this equal to metersPerElement, and using
        //    //     size = (int)(3 * Math.Pow(2, 12 - zoomLevel));

        //    int zoomLevel = (int)(12 - Math.Log(metersPerElement * smallBatch * 20 / len, 2));
        //    return zoomLevel > maxZoom ? maxZoom : zoomLevel;
        //}

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

            long key = Utils.GetKey(zoomLevel, latLo, lonLo);

            ChunkMetadata ret = new ChunkMetadata(smallBatch + 1, smallBatch + 1, latLo, lonLo, latHi, lonHi);

            return ret;
        }
    }
}
