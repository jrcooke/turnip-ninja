using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MountainView.Elevation
{
    public static class Heights
    {
        // private const int smallBatch = 540; // Number of 1/3 arc seconds per 3 minutes.
        private const string cachedFileTemplate = "{0}.hdata";
        private const string description = "Heights";
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        //private static CachingHelper<float> ch = new CachingHelper<float>(
        //    smallBatch,
        //    Path.Combine(rootMapFolder, cachedFileTemplate),
        //    description,
        //    WriteElement,
        //    ReadElement,
        //    GenerateData);

        public static async Task<ChunkHolder<float>> GenerateData(ChunkMetadata template)
        {
            int latMin = (int)Math.Min(template.LatLo.SignedDegrees, template.LatHi.SignedDegrees);
            int latMax = (int)Math.Max(template.LatLo.SignedDegrees, template.LatHi.SignedDegrees);
            int lonMin = (int)Math.Min(template.LonLo.SignedDegrees, template.LonHi.SignedDegrees);
            int lonMax = (int)Math.Max(template.LonLo.SignedDegrees, template.LonHi.SignedDegrees);
            List<ChunkHolder<float>> chunks = new List<ChunkHolder<float>>();
            for (int latInt = latMin; latInt <= latMax; latInt++)
            {
                for (int lonInt = lonMin; lonInt <= lonMax; lonInt++)
                {
                    chunks.Add(await UsgsRawChunks.GetRawHeightsInMeters(latInt, lonInt));
                }
            }

            ChunkHolder<float> ret = new ChunkHolder<float>(
                template.LatSteps, template.LonSteps,
                template.LatLo, template.LonLo,
                template.LatHi, template.LonHi,
                null,
                p => p,
                p => (float)p);
            ret.RenderChunksInto(chunks, Utils.WeightedFloatAverage);
            return ret;
        }

        //public static float GetHeight(Angle lat, Angle lon, double cosLat, double metersPerElement)
        //{
        //    return ch.GetValue(lat, lon, cosLat, metersPerElement);
        //}

        //public static ChunkHolder<float> GetChunk(Angle lat, Angle lon, int zoomLevel)
        //{
        //    ChunkHolder<float> raw = null; // ch.GetValuesFromCache(lat, lon, zoomLevel);
        //    return raw;
        //}

        private static void WriteElement(float item, FileStream stream)
        {
            stream.Write(BitConverter.GetBytes(item), 0, 4);
        }

        private static float ReadElement(FileStream stream, byte[] buffer)
        {
            stream.Read(buffer, 0, 4);
            float i3 = BitConverter.ToSingle(buffer, 0);
            return i3;
        }

        //private static ChunkHolder<float> GenerateData(
        //    int zoomLevel,
        //    int size,
        //    Angle lat1,
        //    Angle lon1,
        //    Angle lat2,
        //    Angle lon2,
        //    Angle minLat,
        //    Angle minLon)
        //{
        //    var maxLat = Angle.Add(minLat, 1.0);
        //    var maxLon = Angle.Add(minLon, 1.0);
        //    ChunkHolder<float> ret = new ChunkHolder<float>(smallBatch + 1, smallBatch + 1, lat1, lon1, lat2, lon2);

        //    var chunks = RawChunks.GetRawHeightsInMeters(lat1, lon1, lat2, lon2);
        //    foreach (var chunk in chunks)
        //    {
        //        LoadRawChunksIntoProcessedChunk(size, minLat, minLon, ret, chunk);
        //    }

        //    return ret;
        //}

        //    private static void LoadRawChunksIntoProcessedChunk(
        //        int size,
        //        Angle minLat,
        //        Angle minLon,
        //        ChunkHolder<float> ret,
        //        ChunkHolder<float> chunk)
        //    {
        //        double minLatDecimal = minLat.DecimalDegree;
        //        double minLonDecimal = minLon.DecimalDegree;
        //        double minLatRoot = Angle.Min(chunk.LatLo, chunk.LatHi).DecimalDegree;
        //        double minLonRoot = Angle.Min(chunk.LonLo, chunk.LonHi).DecimalDegree;

        //        var lat2Min = (float)(minLatRoot + 0 * 1.0 / RawChunks.trueElements);
        //        var lat2Max = (float)(minLatRoot + RawChunks.trueElements * 1.0 / RawChunks.trueElements);
        //        var lon2Min = (float)(minLonRoot + 0 * 1.0 / RawChunks.trueElements);
        //        var lon2Max = (float)(minLonRoot + RawChunks.trueElements * 1.0 / RawChunks.trueElements);

        //        int targetDeltaLatMin = (int)Math.Round((lat2Min - minLatDecimal) * 60 * 60 * smallBatch / size);
        //        int targetDeltaLatMax = (int)Math.Round((lat2Max - minLatDecimal) * 60 * 60 * smallBatch / size);

        //        int targetDeltaLonMin = (int)Math.Round((lon2Min - minLonDecimal) * 60 * 60 * smallBatch / size);
        //        int targetDeltaLonMax = (int)Math.Round((lon2Max - minLonDecimal) * 60 * 60 * smallBatch / size);

        //        for (int j = 0; j <= RawChunks.trueElements; j++)
        //        {
        //            // The chunk has smallBatch+1 elements, so each element is
        //            // TargetElementCoord = angle * smallBatch / Size

        //            var lat2 = (float)(minLatRoot + j * 1.0 / RawChunks.trueElements);
        //            int targetDeltaLat = (int)Math.Round((lat2 - minLatDecimal) * 60 * 60 * smallBatch / size);
        //            if (targetDeltaLat >= 0 && targetDeltaLat <= smallBatch)
        //            {
        //                for (int i = 0; i <= RawChunks.trueElements; i++)
        //                {
        //                    var lon2 = (float)(minLonRoot + i * 1.0 / RawChunks.trueElements);
        //                    int targetDeltaLon = (int)Math.Round((lon2 - minLonDecimal) * 60 * 60 * smallBatch / size);
        //                    if (targetDeltaLon >= 0 && targetDeltaLon <= smallBatch)
        //                    {
        //                        var val = chunk.Data[i + RawChunks.boundaryElements][RawChunks.trueElements - 1 - j + RawChunks.boundaryElements];
        //                        var cur = ret.Data[targetDeltaLat][targetDeltaLon];
        //                        if (cur < val)
        //                        {
        //                            ret.Data[targetDeltaLat][targetDeltaLon] = val;
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
    }
}
