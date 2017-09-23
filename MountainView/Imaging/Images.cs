using MountainView.Base;
using MountainView.ChunkManagement;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using FreeImageAPI;

namespace MountainView.Imaging
{
    internal class Images : CachingHelper<MyColor>
    {
        //      private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        private Images() : base("idata", "Images", 4, 5,
            Utils.ColorToDoubleArray,
            Utils.ColorFromDoubleArray,
            Utils.WeightedColorAverage)
        {
        }

        private static Lazy<Images> current = new Lazy<Images>(() => new Images());
        public static Images Current
        {
            get
            {
                return current.Value;
            }
        }

        protected override async Task<ChunkHolder<MyColor>> GenerateData(StandardChunkMetadata template)
        {
            var ret = new ChunkHolder<MyColor>(
                template.LatSteps, template.LonSteps,
                template.LatLo, template.LonLo,
                template.LatHi, template.LonHi,
                null,
                toDouble,
                fromDouble);

            var targetChunks = (await UsgsRawImageChunks.GetChunkMetadata())
                .Select(p => new
                {
                    p = p,
                    Chunk = new ChunkMetadata(0, 0,
                        Angle.FromDecimalDegrees(p.Points.Min(q => q.Item1)),
                        Angle.FromDecimalDegrees(p.Points.Min(q => q.Item2)),
                        Angle.FromDecimalDegrees(p.Points.Max(q => q.Item1)),
                        Angle.FromDecimalDegrees(p.Points.Max(q => q.Item2)))
                })
                .Where(p => !ret.Disjoint(p.Chunk))
                .ToArray();

            var chunks = new List<ChunkHolder<MyColor>>();
            foreach (var tmp in targetChunks)
            {
                Console.WriteLine(tmp.Chunk);
                var col = await UsgsRawImageChunks.GetRawColors(
                    Angle.Add(tmp.Chunk.LatLo, Angle.Divide(tmp.Chunk.LatDelta, 2)),
                    Angle.Add(tmp.Chunk.LonLo, Angle.Divide(tmp.Chunk.LonDelta, 2)));

                if (col != null)
                {
                    chunks.Add(col);
                }
            }

            ret.RenderChunksInto(chunks, aggregate);
            return ret;
        }

        protected override void WritePixel(MemoryStream stream, MyColor pixel)
        {
            stream.WriteByte(pixel.R);
            stream.WriteByte(pixel.G);
            stream.WriteByte(pixel.B);
        }

        protected override MyColor ReadPixel(MemoryStream stream, byte[] buffer)
        {
            return new MyColor(
                (byte)stream.ReadByte(),
                (byte)stream.ReadByte(),
                (byte)stream.ReadByte());
        }
    }
}
