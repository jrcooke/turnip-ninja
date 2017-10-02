using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MountainView
{
    class Tests
    {
        public static void Test1(string outputFolder)
        {
            //var homeLat = Angle.FromDecimalDegrees(47.6867797);
            //var homeLon = Angle.FromDecimalDegrees(-122.2907541);

            //var xxx = Images.Current.GetData(homeLat, homeLon, 10).Result;
            //Utils.WriteImageFile(xxx, Path.Combine(outputFolder, "xxx.png"), a => a);

            //var yyy = Heights.GenerateData(homeLat, homeLon, 10).Result;
            //Utils.WriteImageFile(yyy, Path.Combine(outputFolder, "yyy.png"), a => Utils.GetColorForHeight(a));

            ////                    var newONe = AdfReaderWorker.GetChunk(@"C:\Users\jrcoo\Desktop\Map\n48w123\grdn48w123_13");
            ////// Utils.WriteImageFile(newONe, Path.Combine(outputFolder, "newONe.png"), a => Utils.GetColorForHeight(a));
            ////ChunkHolder<float> ddd = newONe.RenderSubChunk(homeLat, homeLon,
            ////    Angle.FromMinutes(2), Angle.FromMinutes(2),
            ////    Angle.FromThirds(20), Angle.FromThirds(20),
            ////    Utils.WeightedFloatAverage);
            ////Utils.WriteImageFile(ddd, Path.Combine(outputFolder, "ddd.png"), a => Utils.GetColorForHeight(a));

            //var tttt = ImageWorker.GetColors(homeLat, homeLon, 13).Result;
            ////ChunkHolder<MyColor> ddd2 = tttt.RenderSubChunk(homeLat, homeLon,
            ////    Angle.FromMinutes(2), Angle.FromMinutes(2),
            ////    Angle.FromThirds(20), Angle.FromThirds(20),
            ////    Utils.WeightedColorAverage);
            //Utils.WriteImageFile(tttt, Path.Combine(outputFolder, "tttt.png"), a => a);
            ////Utils.WriteImageFile(ddd2, Path.Combine(outputFolder, "ddd2.png"), a => a);
        }

        public static void Test12()
        {
            for (int i = 0; i <= StandardChunkMetadata.MaxZoomLevel; i++)
            {
                StandardChunkMetadata.GetKey(100, 100, i, 1);
            }

            var lat = Angle.FromDecimalDegrees(47.6867797);
            var lon = Angle.FromDecimalDegrees(-122.2907541);

            Console.WriteLine(lat.ToLatString() + "," + lon.ToLonString());

            for (int zoomLevel = StandardChunkMetadata.MaxZoomLevel; zoomLevel >= 0; zoomLevel--)
            {
                var kay = StandardChunkMetadata.GetKey(lat.Fourths, lon.Fourths, zoomLevel, 1);
                var xxx = StandardChunkMetadata.GetRangeFromKey(kay, 1);

                var cc = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, zoomLevel, 1);
                Console.Write(zoomLevel + "\t" + cc.LatDelta);
                Console.WriteLine("\t" + cc.LatLo.ToLatString() + "," + cc.LonLo.ToLonString() + ", " + cc.LatHi.ToLatString() + "," + cc.LonHi.ToLonString());
            }
        }

        public static async Task Test3(string outputFolder, Angle lat, Angle lon)
        {
            for (int zoomLevel = StandardChunkMetadata.MaxZoomLevel; zoomLevel >= 2; zoomLevel--)
            {
                StandardChunkMetadata template = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, zoomLevel, 1);

                var pixels2 = await Heights.Current.GetData(template);
                if (pixels2 != null)
                {
                    Utils.WriteImageFile(pixels2,
                        Path.Combine(outputFolder, "AChunkH" + zoomLevel + ".png"),
                        a => Utils.GetColorForHeight(a));
                }

                var pixels = await Images.Current.GetData(template);
                if (pixels != null)
                {
                    Utils.WriteImageFile(pixels,
                        Path.Combine(outputFolder, "AChunkC" + zoomLevel + ".png"),
                        a => a);
                }
            }
        }
    }
}
