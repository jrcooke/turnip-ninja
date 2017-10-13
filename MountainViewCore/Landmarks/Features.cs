using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MountainView.Imaging
{
    public class Features : CachingHelper<MyColor>
    {
        private Features() : base(
            "fdata",
            "Features",
            4,
            5,
            null,
            null,
            null)
        {
        }

        private static Lazy<Features> current = new Lazy<Features>(() => new Features());
        public static Features Current
        {
            get
            {
                return current.Value;
            }
        }

        protected override async Task<ChunkHolder<MyColor>> GenerateData(StandardChunkMetadata template)
        {
            await Task.Delay(0);
            var metadataLines = File.ReadAllLines(@"C:\Users\jrcoo\Downloads\WA_Features_20170801 (1)\WA_Features_20170801.txt");
            string[] header = metadataLines.First().Split('|');
            string[] header2 = metadataLines.Skip(1).First().Split('|');
            var nameToIndex = header.Select((p, i) => new { p = p, i = i }).ToDictionary(p => p.p, p => p.i);
            var fileInfo = metadataLines
                .Skip(1)
                .Select(p => p.Split('|'))
                .Select(p => new
                {
                    Id = int.Parse(p[nameToIndex["FEATURE_ID"]]),
                    FeatureClass = p[nameToIndex["FEATURE_CLASS"]],
                    Name = p[nameToIndex["MAP_NAME"]],
                    Lat = Angle.FromDecimalDegrees(double.Parse(p[nameToIndex["PRIM_LAT_DEC"]])),
                    Lon = Angle.FromDecimalDegrees(double.Parse(p[nameToIndex["PRIM_LONG_DEC"]])),
                })
                .ToArray();

            var root = KDNode<int>.Process(fileInfo.Select(p => KDNode<int>.Point.WithKey(p.Id, p.Lat.DecimalDegree, p.Lon.DecimalDegree)));

            var ret = new ChunkHolder<MyColor>(
                template.LatSteps, template.LonSteps,
                template.LatLo, template.LonLo,
                template.LatHi, template.LonHi,
                (i, j) =>
                {
                    var x = template.GetLat(i).DecimalDegree;
                    var y = template.GetLon(j).DecimalDegree;
                    var bla = root.GetNearest(x, y);
                    var dist = Math.Sqrt((x - bla.Vector[0]) * (x - bla.Vector[0]) + (y - bla.Vector[1]) * (y - bla.Vector[1]));
                    var dist2 = ((int)(dist * 100000.0) % 256);
                    return new MyColor((byte)((bla.Key - short.MinValue) / 256), (byte)((bla.Key - short.MinValue) % 256), (byte)dist2);
                },
                null,
                null);

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
