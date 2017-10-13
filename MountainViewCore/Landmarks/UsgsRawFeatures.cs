using MountainView.Base;
using System;
using System.IO;
using System.Linq;

namespace MountainViewCore.Landmarks
{
    public class FeatureInfo
    {
        public int Id { get; set; }
        public string FeatureClass { get; set; }
        public string Name { get; set; }
        public Angle Lat { get; set; }
        public Angle Lon { get; set; }
    }

    public static class UsgsRawFeatures
    {
        private static Lazy<KDNode<FeatureInfo>> featureInfos = new Lazy<KDNode<FeatureInfo>>(() =>
        {
            var metadataLines = File.ReadAllLines(@"C:\Users\jrcoo\Downloads\WA_Features_20170801 (1)\WA_Features_20170801.txt");
            string[] header = metadataLines.First().Split('|');
            string[] header2 = metadataLines.Skip(1).First().Split('|');
            var nameToIndex = header.Select((p, i) => new { p = p, i = i }).ToDictionary(p => p.p, p => p.i);
            var fileInfo = metadataLines
                .Skip(1)
                .Select(p => p.Split('|'))
                .Select(p => new FeatureInfo
                {
                    Id = int.Parse(p[nameToIndex["FEATURE_ID"]]),
                    FeatureClass = p[nameToIndex["FEATURE_CLASS"]],
                    Name = p[nameToIndex["MAP_NAME"]],
                    Lat = Angle.FromDecimalDegrees(double.Parse(p[nameToIndex["PRIM_LAT_DEC"]])),
                    Lon = Angle.FromDecimalDegrees(double.Parse(p[nameToIndex["PRIM_LONG_DEC"]])),
                })
                .ToArray();
            return KDNode<FeatureInfo>.Process(fileInfo.Select(p => KDNode<FeatureInfo>.Point.WithKey(p, p.Lat.DecimalDegree, p.Lon.DecimalDegree)));
        });

        public static FeatureInfo GetData(Angle lat, Angle lon)
        {
            var latDegree = lat.DecimalDegree;
            var lonDegree = lon.DecimalDegree;
            return GetData(latDegree, lonDegree);
        }
        public static FeatureInfo GetData(double latDegree, double lonDegree)
        {
            return featureInfos.Value.GetNearest(latDegree, lonDegree).Key;
            //featureInfos.Value
            //        var dist = Math.Sqrt((x - bla.Vector[0]) * (x - bla.Vector[0]) + (y - bla.Vector[1]) * (y - bla.Vector[1]));
            //        var dist2 = ((int)(dist * 100000.0) % 256);
            //        return new MyColor((byte)((bla.Key - short.MinValue) / 256), (byte)((bla.Key - short.MinValue) % 256), (byte)dist2);
            //    },
            //    null,
            //    null);
            //return ret;
        }
    }
}
