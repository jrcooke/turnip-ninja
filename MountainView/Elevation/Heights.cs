using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MountainView.Elevation
{
    public class Heights : CachingHelper<float>
    {
        private const string fileExt = "hdata";
        private const string description = "Heights";

        private Heights() : base(fileExt, description, 4)
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

        protected override Task<ChunkHolder<float>> GenerateData(StandardChunkMetadata template)
        {
            int latMin = (int)Math.Min(template.LatLo.DecimalDegree, template.LatHi.DecimalDegree);
            int latMax = (int)Math.Max(template.LatLo.DecimalDegree, template.LatHi.DecimalDegree);
            int lonMin = (int)Math.Min(template.LonLo.DecimalDegree, template.LonHi.DecimalDegree);
            int lonMax = (int)Math.Max(template.LonLo.DecimalDegree, template.LonHi.DecimalDegree);
            List<ChunkHolder<float>> chunks = new List<ChunkHolder<float>>();
            for (int latInt = latMin; latInt <= latMax; latInt++)
            {
                for (int lonInt = lonMin; lonInt <= lonMax; lonInt++)
                {
                    chunks.Add(UsgsRawChunks.GetRawHeightsInMeters(latInt, lonInt));
                }
            }

            var ret = new ChunkHolder<float>(
                  template.LatSteps, template.LonSteps,
                  template.LatLo, template.LonLo,
                  template.LatHi, template.LonHi,
                  null,
                  new Func<float, double>[] { p => p },
                  p => (float)p[0]);
            ret.RenderChunksInto(chunks, Utils.WeightedFloatAverage);
            return Task.FromResult(ret);
        }

        protected override void WritePixel(FileStream stream, float pixel)
        {
            stream.Write(BitConverter.GetBytes(pixel), 0, 4);
        }

        protected override float ReadPixel(FileStream stream, byte[] buffer)
        {
            stream.Read(buffer, 0, 4);
            return BitConverter.ToSingle(buffer, 0);
        }
    }
}
