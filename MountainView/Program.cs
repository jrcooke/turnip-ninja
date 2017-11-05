using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using MountainViewCore.Base;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static MountainViewCore.Base.View;

namespace MountainView
{
    class Program
    {
        static void Main(string[] args)
        {
            DateTime start = DateTime.Now;
            int serverLat = 48;
            int serverLon = -120;

            BlobHelper.SetConnectionString(ConfigurationManager.AppSettings["ConnectionString"]);

            string outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", "Output");
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            bool isServerUpload = false;
            bool isServerCompute = false;
            bool isClient = true;
            try
            {
                //Tests.Test12();

                //Task.WaitAll(Tests.Test3(     outputPath,
                //    Angle.FromDecimalDegrees(47.6867797),
                //    Angle.FromDecimalDegrees(-122.2907541)));

                if (isServerUpload)
                {
                    string uploadPath = "/home/mcuser/turnip-ninja/MountainView/bin/Debug/netcoreapp2.0";
                    UsgsRawChunks.Uploader(uploadPath, serverLat, serverLon);
                    UsgsRawImageChunks.Uploader(uploadPath, serverLat, serverLon);
                }
                else if (isServerCompute)
                {
                    ProcessRawData(
                        Angle.FromDecimalDegrees(serverLat + 0.5),
                        Angle.FromDecimalDegrees(serverLon - 0.5));
                }
                else if (isClient)
                {
                    //Config c = Config.Rainer();
                    Config config = Config.JuanetaAll();
                    //                    Config c = Config.Juaneta();

                    var chunks = View.GetRelevantChunkKeys(config);

                    int numParts = (int)((config.ElevationViewMax.Radians - config.ElevationViewMin.Radians) / config.AngularResolution.Radians);
                    ColorHeight[][] view = new ColorHeight[config.NumTheta][];
                    for (int i = 0; i < view.Length; i++)
                    {
                        view[i] = new ColorHeight[numParts];
                    }

                    float eyeHeight = 5;
                    float heightOffset = View.GetHeightAtPoint(config, chunks.Last()) + eyeHeight;

                    int counter = 0;
                    foreach (var chunk in chunks)
                    {
                        var view2 = View.GetPolarData(config, chunk, heightOffset);
                        foreach (var elem in view2)
                        {
                            view[elem.iTheta][elem.iViewElev] = elem.ToColorHeight();
                        }

                        var www = View.ProcessImage(view);
                        Utils.WriteImageFile(www, Path.Combine(outputPath, counter + ".jpg"), a => a, OutputType.JPEG);

                        counter++;
                        Console.WriteLine(counter);
                    }

                    //var xxx = View.ProcessImage(view);
                    //Utils.WriteImageFile(xxx, Path.Combine(outputPath, imageFile), a => a, OutputType.JPEG);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            DateTime end = DateTime.Now;
            Console.WriteLine(start);
            Console.WriteLine(end);
            Console.WriteLine(end - start);
        }

        public static void ImagesForTopChunks(string outputFolder)
        {
            var x = BlobHelper.GetFiles("mapv8", "");
            var top = new Regex(@"\d\d\dDn\d\d\dDw03[.]v8.*");
            var t = x.Where(p => top.IsMatch(p)).ToArray();
            foreach (var f in t)
            {
                var parts = f.Split('D', 'n');
                var lat = Angle.FromDecimalDegrees(+int.Parse(parts[0]) + 0.5);
                var lon = Angle.FromDecimalDegrees(-int.Parse(parts[2]) + 0.5);
                var scm = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, 3);

                if (f.EndsWith(".idata"))
                {
                    var xxx = Images.Current.GetData(scm);
                    Utils.WriteImageFile(xxx, Path.Combine(outputFolder, f + ".jpg"), a => a, OutputType.JPEG);
                }
                else
                {
                    var yyy = Heights.Current.GetData(scm);
                    Utils.WriteImageFile(yyy, Path.Combine(outputFolder, f + ".jpg"), a => Utils.GetColorForHeight(a), OutputType.JPEG);
                }
            }
        }

        public static void ProcessRawData(Angle lat, Angle lon)
        {
            // Generate for a 1 degree square region.
            StandardChunkMetadata template = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, 3);
            ProcessRawData(template);
        }

        public static void ProcessRawData(StandardChunkMetadata template)
        {
            bool doMore = false;
            if (template.ZoomLevel <= Heights.Current.SourceDataZoom)
            {
                var ok = Heights.Current.ExistsComputedChunk(template);
                Console.Write(ok ? "." : ("Heights:" + Heights.Current.GetShortFilename(template) + ":" + "missing"));
                doMore = true;
            }

            if (template.ZoomLevel <= Images.Current.SourceDataZoom)
            {
                var ok = Images.Current.ExistsComputedChunk(template);
                Console.Write(ok ? "." : ("Images:" + Images.Current.GetShortFilename(template) + ":" + "missing"));
                doMore = true;
            }

            if (!doMore) return;

            foreach (var c in template.GetChildChunks())
            {
                ProcessRawData(c);
            }
        }

        private static void ProcessOutput(string outputFolder, Config config, View.ColorHeight[][] view)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var imageFile = config.Name + ".jpg";
            var xxx = View.ProcessImage(view);
            Utils.WriteImageFile(xxx, Path.Combine(outputFolder, imageFile), a => a, OutputType.JPEG);

            var maptxt = View.ProcessImageMap(view, imageFile);
            var htmltxt = "<HTML><HEAD><TITLE>title of page</TITLE></HEAD><BODY>" + maptxt + "</BODY></HTML>";
            File.WriteAllText(Path.Combine(outputFolder, config.Name + ".html"), htmltxt);
        }
    }
}

