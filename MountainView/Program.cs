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
using System.Threading.Tasks;
using SoftEngine;
using MountainView.Render;

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
                    ////Config c = Config.Rainer();
                    ////Config config = Config.JuanetaAll();
                    //Config config = Config.Juaneta();

                    //var chunks = View.GetRelevantChunkKeys(config, log);

                    //int numParts = (int)((config.ElevationViewMax.Radians - config.ElevationViewMin.Radians) / config.AngularResolution.Radians);
                    //ColorHeight[][] view = new ColorHeight[config.NumTheta][];
                    //for (int i = 0; i < view.Length; i++)
                    //{
                    //    view[i] = new ColorHeight[numParts];
                    //}

                    //float eyeHeight = 5;
                    //float heightOffset = (await View.GetHeightAtPoint(config, chunks.Last(), log)) + eyeHeight;

                    //int counter = 0;
                    //foreach (var chunk in chunks)
                    //{
                    //    var view2 = await View.GetPolarData(config, chunk, heightOffset, log);
                    //    ColorHeight[][] view3 = new ColorHeight[config.NumTheta][];
                    //    for (int i = 0; i < view3.Length; i++)
                    //    {
                    //        view3[i] = new ColorHeight[numParts];
                    //    }

                    //    if (view2 != null)
                    //    {
                    //        foreach (var elem in view2)
                    //        {
                    //            view3[elem.iTheta][elem.iViewElev] = elem.ToColorHeight();
                    //        }
                    //    }

                    //    var www = await View.ProcessImage(view3, log);
                    //    Utils.WriteImageFile(www, Path.Combine(outputPath, counter + ".jpg"), a => a, OutputType.JPEG);

                    //    counter++;
                    //    log?.WriteLine(counter);
                    //}

                    ////var xxx = View.ProcessImage(view);
                    ////Utils.WriteImageFile(xxx, Path.Combine(outputPath, imageFile), a => a, OutputType.JPEG);
                }
            }
            catch (Exception ex)
            {
                log?.WriteLine(ex.ToString());
            }

            DateTime end = DateTime.Now;
            log?.WriteLine(start);
            log?.WriteLine(end);
            log?.WriteLine(end - start);
        }

        public static async Task Doit(Config config, TraceListener log, Action<Stream> drawToScreen)
        {
            DateTime start = DateTime.Now;
            BlobHelper.SetConnectionString(ConfigurationManager.AppSettings["ConnectionString"]);

            var x = await BlobHelper.GetFileNames("mapv8", null, log);

            var y = x
                .Select(p => p.Split('.'))
                .Select(p => new { Base = StandardChunkMetadata.ParseBase(p[0]), V = p[1], Ext = p[2] })
                .GroupBy(p => new { p.Ext, p.V, p.Base.ZoomLevel })
                .Select(p => new { p.Key.Ext, p.Key.V, p.Key.ZoomLevel, Data = p.Select(q => q.Base).ToArray() })
                .OrderBy(p => p.ZoomLevel)
                .ThenBy(p => p.Ext)
                .ThenBy(p => p.V)
                .ToArray();

            foreach (var dfgdfg in y)
            {
                log?.WriteLine(dfgdfg.Ext + "\t" + dfgdfg.ZoomLevel + "\t" + dfgdfg.V);
                var lats = dfgdfg.Data.Select(p => p.LatLo.Abs).Distinct().ToArray();
                var lons = dfgdfg.Data.Select(p => p.LonLo.Abs).Distinct().ToArray();

                for(int i = 0; i < lats.Length; i++)
                {
                    for (int j = 0; j < lons.Length; j++)
                    {
                        log?.Write(dfgdfg.Data.Any(p => p.LatLo.Abs == lats[i] && p.LonLo.Abs == lons[j]) ? "X" : " ");
                    }
                    log?.WriteLine("");
                }
            }

            var theta = (config.MaxAngle.DecimalDegree + config.MinAngle.DecimalDegree) * Math.PI / 360;
            var z = 0.01f;// 0.05f;
            int subpixel = 3;

            var chunkBmp = new DirectBitmap(subpixel * config.Width, subpixel * config.Height);
            var compositeBmp = new DirectBitmap(subpixel * config.Width, subpixel * config.Height);
            compositeBmp.SetAllPixels(View.skyColor);

            Device device = new Device()
            {
                Camera = new Camera()
                {
                    Position = new Vector3f(0, 0, z),
                    Target = new Vector3f((float)Math.Sin(theta), (float)Math.Cos(theta), z),
                    UpDirection = new Vector3f(0, 0, 1),
                    FovRad = (float)config.FOV.Radians,
                },
                AmbientLight = 0.5f,
                DirectLight = 1.0f,
                Light = new Vector3f(0, 0, 20),
            };

            var chunks = View.GetRelevantChunkKeys(config, log);

            StandardChunkMetadata mainChunk = StandardChunkMetadata.GetRangeFromKey(chunks.Last());
            var mainMesh = await Meshes.Current.GetData(mainChunk, log);
            var norm = mainMesh.GetCenterAndScale(config.Lat.DecimalDegree, config.Lon.DecimalDegree, mainChunk.ZoomLevel, 10, log);

            int counter = 0;
            foreach (var chunkKey in chunks)
            {
                StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);
                if (chunk == null) continue;

                var mesh = await Meshes.Current.GetData(chunk, log);
                if (mesh == null) continue;

                if (norm == null)
                {
                    norm = mesh.GetCenterAndScale(config.Lat.DecimalDegree, config.Lon.DecimalDegree, chunk.ZoomLevel, 10, log);
                }

                mesh.Match(norm);

                try
                {
                    mesh.ImageData = await JpegImages.Current.GetData(chunk, log);
                }
                catch
                {
                }

                if (mesh.ImageData == null)
                {
                    DirectBitmap tmp = new DirectBitmap(10, 10);
                    tmp.SetAllPixels(new MyColor(0, 0, 0, 255));
                    using (var mss = new MemoryStream())
                    {
                        tmp.WriteFile(OutputType.PNG, mss);
                        mss.Position = 0;
                        mesh.ImageData = new byte[mss.Length];
                        mss.Read(mesh.ImageData, 0, mesh.ImageData.Length);
                    }
                }

                var renderMesh = SoftEngine.Mesh.GetMesh(mesh.ImageData, mesh);
                foreach (var oldMesh in device.Meshes)
                {
                    oldMesh.Dispose();
                }

                device.Meshes.Clear();
                device.Meshes.Add(renderMesh);
                device.RenderInto(chunkBmp);
                compositeBmp.DrawOn(chunkBmp);

                drawToScreen?.Invoke(compositeBmp.GetStream(OutputType.PNG));

                using (var fs = File.OpenWrite(Path.Combine(".", counter + ".jpg")))
                {
                    chunkBmp.WriteFile(OutputType.JPEG, fs);
                }

                counter++;
                log?.WriteLine(counter);
            }

            DateTime end = DateTime.Now;
            log?.WriteLine(start);
            log?.WriteLine(end);
            log?.WriteLine(end - start);
        }

        //public static async Task<FriendlyMesh> GetMesh(TraceListener log, double latD, double lonD, int zoomLevel = 5)
        //{
        //    // 5 is most zoomed in.
        //    BlobHelper.SetConnectionString(ConfigurationManager.AppSettings["ConnectionString"]);

        //    var lat = Angle.FromDecimalDegrees(latD);
        //    var lon = Angle.FromDecimalDegrees(lonD);
        //    log?.WriteLine(lat.ToLatString() + "," + lon.ToLonString());

        //    var template = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, zoomLevel);
        //    log?.WriteLine(template);

        //    var m = await Meshes.Current.GetData(template, log);
        //    if (m != null)
        //    {
        //        m.ImageData = await JpegImages.Current.GetData(template, log);
        //    }

        //    return m;
        //}

        //public static async Task ImagesForTopChunks(string outputFolder, TraceListener log)
        //{
        //    var x = await BlobHelper.GetFiles("mapv8", "", log);
        //    var top = new Regex(@"\d\d\dDn\d\d\dDw03[.]v8.*");
        //    var t = x.Where(p => top.IsMatch(p)).ToArray();
        //    foreach (var f in t)
        //    {
        //        var parts = f.Split('D', 'n');
        //        var lat = Angle.FromDecimalDegrees(+int.Parse(parts[0]) + 0.5);
        //        var lon = Angle.FromDecimalDegrees(-int.Parse(parts[2]) + 0.5);
        //        var scm = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, 3);

        //        if (f.EndsWith(".idata"))
        //        {
        //            var xxx = await Images.Current.GetData(scm, log);
        //            Utils.WriteImageFile(xxx, Path.Combine(outputFolder, f + ".jpg"), a => a, OutputType.JPEG);
        //        }
        //        else
        //        {
        //            var yyy = await Heights.Current.GetData(scm, log);
        //            Utils.WriteImageFile(yyy, Path.Combine(outputFolder, f + ".jpg"), a => Utils.GetColorForHeight(a), OutputType.JPEG);
        //        }
        //    }
        //}

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
                log?.Write(ok ? "." : ("Heights:" + Heights.Current.GetShortFilename(template) + ":" + "missing"));
                doMore = true;
            }

            if (template.ZoomLevel <= Images.Current.SourceDataZoom)
            {
                var ok = await Images.Current.ExistsComputedChunk(template, log);
                log?.Write(ok ? "." : ("Images:" + Images.Current.GetShortFilename(template) + ":" + "missing"));
                doMore = true;
            }

            if (!doMore) return;

            foreach (var c in template.GetChildChunks())
            {
                await ProcessRawData(c, log);
            }
        }

        //private static async Task ProcessOutput(string outputFolder, string name, Config config, View.ColorHeight[][] view, TraceListener log)
        //{
        //    if (!Directory.Exists(outputFolder))
        //    {
        //        Directory.CreateDirectory(outputFolder);
        //    }

        //    var imageFile = name + ".jpg";
        //    var xxx = await View.ProcessImage(view, log);
        //    Utils.WriteImageFile(xxx, Path.Combine(outputFolder, imageFile), a => a, OutputType.JPEG);

        //    var maptxt = View.ProcessImageMap(view, imageFile);
        //    var htmltxt = "<HTML><HEAD><TITLE>title of page</TITLE></HEAD><BODY>" + maptxt + "</BODY></HTML>";
        //    File.WriteAllText(Path.Combine(outputFolder, name + ".html"), htmltxt);
        //}
    }
}

