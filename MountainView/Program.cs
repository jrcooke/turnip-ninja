using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using MountainView.Mesh;
using MountainViewCore.Base;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static MountainViewCore.Base.View;

namespace MountainView
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Task.WaitAll(Foo(null));
        }

        public static async Task Foo(TraceListener log, Action<MemoryStream> getBitmap = null)
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
                await Tests.Test12(outputPath, log, getBitmap);

                //await Tests.Test3(outputPath,
                //    Angle.FromDecimalDegrees(47.6867797),
                //    Angle.FromDecimalDegrees(-122.2907541),
                //    log);

                if (isServerUpload)
                {
                    string uploadPath = "/home/mcuser/turnip-ninja/MountainView/bin/Debug/netcoreapp2.0";
                    Task.WaitAll(UsgsRawChunks.Uploader(uploadPath, serverLat, serverLon, log));
                    Task.WaitAll(UsgsRawImageChunks.Uploader(uploadPath, serverLat, serverLon, log));
                }
                else if (isServerCompute)
                {
                    Task.WaitAll(ProcessRawData(
                        Angle.FromDecimalDegrees(serverLat + 0.5),
                        Angle.FromDecimalDegrees(serverLon - 0.5), log));
                }
                else if (isClient)
                {
                    //Config c = Config.Rainer();
                    //Config config = Config.JuanetaAll();
                    Config config = Config.Juaneta();

                    var chunks = View.GetRelevantChunkKeys(config, log);

                    int numParts = (int)((config.ElevationViewMax.Radians - config.ElevationViewMin.Radians) / config.AngularResolution.Radians);
                    ColorHeight[][] view = new ColorHeight[config.NumTheta][];
                    for (int i = 0; i < view.Length; i++)
                    {
                        view[i] = new ColorHeight[numParts];
                    }

                    float eyeHeight = 5;
                    float heightOffset = (await View.GetHeightAtPoint(config, chunks.Last(), log)) + eyeHeight;

                    int counter = 0;
                    foreach (var chunk in chunks)
                    {
                        var view2 = await View.GetPolarData(config, chunk, heightOffset, log);
                        ColorHeight[][] view3 = new ColorHeight[config.NumTheta][];
                        for (int i = 0; i < view3.Length; i++)
                        {
                            view3[i] = new ColorHeight[numParts];
                        }

                        if (view2 != null)
                        {
                            foreach (var elem in view2)
                            {
                                view3[elem.iTheta][elem.iViewElev] = elem.ToColorHeight();
                            }
                        }

                        var www = await View.ProcessImage(view3, log);
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


        public static async Task<FriendlyMesh> GetMesh(TraceListener log, double latD, double lonD, int zoomLevel = 5)
        {
            // 5 is most zoomed in.
            BlobHelper.SetConnectionString(ConfigurationManager.AppSettings["ConnectionString"]);

            var lat = Angle.FromDecimalDegrees(latD);
            var lon = Angle.FromDecimalDegrees(lonD);

            log?.WriteLine(lat.ToLatString() + "," + lon.ToLonString());

            var template = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, zoomLevel);
            log?.WriteLine(zoomLevel + "\t" + template.LatDelta +
                "\t" + template.LatLo.ToLatString() + "," + template.LonLo.ToLonString() +
                ", " + template.LatHi.ToLatString() + "," + template.LonHi.ToLonString());

            var m = await Meshes.Current.GetData(template, log);

            // TODO: remove
            m.ExagerateHeight(10.0);
            m.CenterAndScale();
            return m;
        }

        public static async Task GetImages(TraceListener log, double latD, double lonD, int zoomLevel,
            Action<MemoryStream> getHeightBitmap = null,
            Action<MemoryStream> getImageBitmap2 = null)
        {
            BlobHelper.SetConnectionString(ConfigurationManager.AppSettings["ConnectionString"]);

            var lat = Angle.FromDecimalDegrees(latD);
            var lon = Angle.FromDecimalDegrees(lonD);

            log?.WriteLine(lat.ToLatString() + "," + lon.ToLonString());

            var template = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, zoomLevel);
            if (template == null)
            {
                log?.WriteLine("Chunk is null");
            }
            else
            {
                log?.Write(zoomLevel + "\t" + template.LatDelta);
                log?.WriteLine("\t" + template.LatLo.ToLatString() + "," + template.LonLo.ToLonString() + ", " + template.LatHi.ToLatString() + "," + template.LonHi.ToLonString());

                if (getHeightBitmap != null)
                {
                    try
                    {
                        var heights = await Heights.Current.GetData((StandardChunkMetadata)template, log);
                        if (heights != null) getHeightBitmap?.Invoke(Utils.GetBitmap(heights, a => Utils.GetColorForHeight(a), OutputType.JPEG));
                    }
                    catch (Exception ex)
                    {
                        log?.WriteLine(ex.Message);
                    }
                }

                if (getImageBitmap2 != null)
                {
                    try
                    {
                        var pixels = await Images.Current.GetData((StandardChunkMetadata)template, log);
                        if (pixels != null) getImageBitmap2?.Invoke(Utils.GetBitmap(pixels, a => a, OutputType.JPEG));
                    }
                    catch (Exception ex)
                    {
                        log?.WriteLine(ex.Message);
                    }
                }
            }
        }

        public static async Task ImagesForTopChunks(string outputFolder, TraceListener log)
        {
            var x = await BlobHelper.GetFiles("mapv8", "", log);
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
                    var xxx = await Images.Current.GetData(scm, log);
                    Utils.WriteImageFile(xxx, Path.Combine(outputFolder, f + ".jpg"), a => a, OutputType.JPEG);
                }
                else
                {
                    var yyy = await Heights.Current.GetData(scm, log);
                    Utils.WriteImageFile(yyy, Path.Combine(outputFolder, f + ".jpg"), a => Utils.GetColorForHeight(a), OutputType.JPEG);
                }
            }
        }

        public static async Task ProcessRawData(Angle lat, Angle lon, TraceListener log)
        {
            // Generate for a 1 degree square region.
            StandardChunkMetadata template = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, 3);
            await ProcessRawData(template, log);
        }

        public static async Task ProcessRawData(StandardChunkMetadata template, TraceListener log)
        {
            bool doMore = false;
            if (template.ZoomLevel <= Heights.Current.SourceDataZoom)
            {
                var ok = await Heights.Current.ExistsComputedChunk(template, log);
                Console.Write(ok ? "." : ("Heights:" + Heights.Current.GetShortFilename(template) + ":" + "missing"));
                doMore = true;
            }

            if (template.ZoomLevel <= Images.Current.SourceDataZoom)
            {
                var ok = await Images.Current.ExistsComputedChunk(template, log);
                Console.Write(ok ? "." : ("Images:" + Images.Current.GetShortFilename(template) + ":" + "missing"));
                doMore = true;
            }

            if (!doMore) return;

            foreach (var c in template.GetChildChunks())
            {
                await ProcessRawData(c, log);
            }
        }

        private static async Task ProcessOutput(string outputFolder, string name, Config config, View.ColorHeight[][] view, TraceListener log)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var imageFile = name + ".jpg";
            var xxx = await View.ProcessImage(view, log);
            Utils.WriteImageFile(xxx, Path.Combine(outputFolder, imageFile), a => a, OutputType.JPEG);

            var maptxt = View.ProcessImageMap(view, imageFile);
            var htmltxt = "<HTML><HEAD><TITLE>title of page</TITLE></HEAD><BODY>" + maptxt + "</BODY></HTML>";
            File.WriteAllText(Path.Combine(outputFolder, name + ".html"), htmltxt);
        }
    }
}

