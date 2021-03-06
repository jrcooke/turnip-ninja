﻿using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace MountainView
{
    class Tests
    {
        public static async Task Test1(string outputFolder, TraceListener log)
        {
            var homeLat = Angle.FromDecimalDegrees(47.6867797);
            var homeLon = Angle.FromDecimalDegrees(-122.2907541);

            var scm = StandardChunkMetadata.GetRangeContaingPoint(homeLat, homeLon, 4);
            var xxx = await Images.Current.GetData(scm, log);
            Utils.WriteImageFile(xxx, Path.Combine(outputFolder, "xxx.jpg"), a => a, OutputType.JPEG);

            var yyy = await Heights.Current.GetData(scm, log);
            Utils.WriteImageFile(yyy, Path.Combine(outputFolder, "yyy.jpg"), a => Utils.GetColorForHeight(a), OutputType.JPEG);

            //var newONe = AdfReaderWorker.GetChunk(@"C:\Users\jrcoo\Desktop\Map\n48w123\grdn48w123_13");
            //// Utils.WriteImageFile(newONe, Path.Combine(outputFolder, "newONe.png"), a => Utils.GetColorForHeight(a));
            //ChunkHolder<float> ddd = newONe.RenderSubChunk(homeLat, homeLon,
            //    Angle.FromMinutes(2), Angle.FromMinutes(2),
            //    Angle.FromThirds(20), Angle.FromThirds(20),
            //    Utils.WeightedFloatAverage);
            //Utils.WriteImageFile(ddd, Path.Combine(outputFolder, "ddd.png"), a => Utils.GetColorForHeight(a));

            //var tttt = ImageWorker.GetColors(homeLat, homeLon, 13).Result;
            ////ChunkHolder<MyColor> ddd2 = tttt.RenderSubChunk(homeLat, homeLon,
            ////    Angle.FromMinutes(2), Angle.FromMinutes(2),
            ////    Angle.FromThirds(20), Angle.FromThirds(20),
            ////    Utils.WeightedColorAverage);
            //Utils.WriteImageFile(tttt, Path.Combine(outputFolder, "tttt.png"), a => a);
            ////Utils.WriteImageFile(ddd2, Path.Combine(outputFolder, "ddd2.png"), a => a);
        }

        public static async Task Test12(string outputFolder, TraceListener log, Action<MemoryStream> getBitmap = null)
        {
            var lat = Angle.FromDecimalDegrees(47.6867797);
            var lon = Angle.FromDecimalDegrees(-122.2907541);

            for (int i = 0; i <= StandardChunkMetadata.MaxZoomLevel; i++)
            {
                var k1 = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, i);
                log?.WriteLine(i + ", 1: " + k1);
            }


            log?.WriteLine(lat.ToLatString() + "," + lon.ToLonString());

            for (int zoomLevel = StandardChunkMetadata.MaxZoomLevel; zoomLevel >= 0; zoomLevel--)
            {
                var kay = StandardChunkMetadata.GetKey(lat.Fourths, lon.Fourths, zoomLevel);
                var xxx = StandardChunkMetadata.GetRangeFromKey(kay);

                var cc = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, zoomLevel);
                if (cc == null)
                {
                    log?.WriteLine("Chunk is null");
                }
                else
                {
                    log?.Write(zoomLevel + "\t" + cc.LatDelta);
                    log?.WriteLine("\t" + cc.LatLo.ToLatString() + "," + cc.LonLo.ToLonString() + ", " + cc.LatHi.ToLatString() + "," + cc.LonHi.ToLonString());

                    var template = cc;
                    try
                    {
                        var pixels2 = await Heights.Current.GetData(template, log);
                        if (pixels2 != null)
                        {
                            Utils.WriteImageFile(pixels2,
                                Path.Combine(outputFolder, "AChunkH" + zoomLevel + ".png"),
                                a => Utils.GetColorForHeight(a),
                                OutputType.JPEG);
                            getBitmap?.Invoke(Utils.GetBitmap(pixels2, a => Utils.GetColorForHeight(a), OutputType.JPEG));
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.WriteLine(ex.Message);
                    }

                    try
                    {
                        var pixels = await Images.Current.GetData(template, log);
                        if (pixels != null)
                        {
                            Utils.WriteImageFile(pixels,
                                Path.Combine(outputFolder, "AChunkC" + zoomLevel + ".png"),
                                a => a,
                                OutputType.JPEG);
                            getBitmap?.Invoke(Utils.GetBitmap(pixels, a => a, OutputType.JPEG));
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.WriteLine(ex.Message);
                    }
                }
            }
        }

        public static async Task Test3(string outputFolder, Angle lat, Angle lon, TraceListener log)
        {
            for (int zoomLevel = 5; zoomLevel >= 3; zoomLevel--)
            {
                StandardChunkMetadata template = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, zoomLevel);
                var pixels2 = await Heights.Current.GetData(template, log);
                if (pixels2 != null)
                {
                    Utils.WriteImageFile(pixels2,
                        Path.Combine(outputFolder, "AChunkH" + zoomLevel + ".png"),
                        a => Utils.GetColorForHeight(a),
                        OutputType.JPEG);
                }

                var pixels = await Images.Current.GetData(template, log);
                if (pixels != null)
                {
                    Utils.WriteImageFile(pixels,
                        Path.Combine(outputFolder, "AChunkC" + zoomLevel + ".png"),
                        a => a,
                        OutputType.JPEG);
                }

                //var pixels3 = await Features.Current.GetData(template, log);
                //if (pixels3 != null)
                //{
                //    Utils.WriteImageFile(pixels3,
                //        Path.Combine(outputFolder, "AChunkF" + zoomLevel + ".jpg"),
                //        a => a,
                //        OutputType.JPEG);// new MyColor((byte)((a - short.MinValue) / 256), (byte)((a - short.MinValue) % 256), 0));
                //}
            }
        }
    }
}