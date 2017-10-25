using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.IO;

namespace MountainView.Elevation
{
    public class Heights : CachingHelper<float>
    {
        private Heights() : base("hdata", "Heights", 4, 4,
            new Func<float, double>[] { p => p },
            p => (float)p[0],
            Utils.WeightedFloatAverage)
        {
        }

        private static Lazy<Heights> current = new Lazy<Heights>(() => new Heights());
        public static Heights Current
        {
            get
            {
                return current.Value;
            }
        }

        protected override ChunkHolder<float> GenerateData(StandardChunkMetadata template)
        {
            var ret = new ChunkHolder<float>(
                template.LatSteps, template.LonSteps,
                template.LatLo, template.LonLo,
                template.LatHi, template.LonHi,
                null,
                toDouble,
                fromDouble);

            int latMin = (int)(Math.Min(template.LatLo.DecimalDegree, template.LatHi.DecimalDegree) + 1.0e-5);
            int latMax = (int)(Math.Max(template.LatLo.DecimalDegree, template.LatHi.DecimalDegree) - 1.0e-5);
            int lonMin = (int)(Math.Min(template.LonLo.DecimalDegree, template.LonHi.DecimalDegree) + 1.0e-5);
            int lonMax = (int)(Math.Max(template.LonLo.DecimalDegree, template.LonHi.DecimalDegree) - 1.0e-5);
            var chunks = new List<ChunkHolder<float>>();
            for (int latInt = latMin; latInt <= latMax; latInt++)
            {
                for (int lonInt = lonMin; lonInt <= lonMax; lonInt++)
                {
                    chunks.Add(UsgsRawChunks.GetRawHeightsInMeters(latInt, lonInt));
                }
            }

            ret.RenderChunksInto(chunks, aggregate);
            return ret;
        }

        protected override void WritePixel(MemoryStream stream, float pixel)
        {
            stream.Write(BitConverter.GetBytes(pixel), 0, 4);
        }

        protected override float ReadPixel(MemoryStream stream, byte[] buffer)
        {
            stream.Read(buffer, 0, 4);
            return BitConverter.ToSingle(buffer, 0);
        }
    }
}
