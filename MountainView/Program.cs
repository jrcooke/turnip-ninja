using MeshDecimator;
using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using MountainViewCore.Base;
using MountainViewDesktopCore.Elevation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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


        public static async Task<MOO> Foo2(TraceListener log, double latD, double lonD,
            Action<MemoryStream> getBitmap = null,
            Action<MemoryStream> getBitmap2 = null)
        {
            MOO m2 = null;

            BlobHelper.SetConnectionString(ConfigurationManager.AppSettings["ConnectionString"]);

            var lat = Angle.FromDecimalDegrees(latD);
            var lon = Angle.FromDecimalDegrees(lonD);

            log?.WriteLine(lat.ToLatString() + "," + lon.ToLonString());

            int zoomLevel = 5; // 4; // 5 is max;//StandardChunkMetadata.MaxZoomLevel; zoomLevel >= 0; zoomLevel--)
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
                            getBitmap?.Invoke(Utils.GetBitmap(pixels2, a => Utils.GetColorForHeight(a), OutputType.JPEG));
                            var heig = pixels2.Data;

                            int max = heig.Length;

                            Dictionary<int, Tuple<double, double>> latSinCoses = new Dictionary<int, Tuple<double, double>>();
                            Dictionary<int, Tuple<double, double>> lonSinCoses = new Dictionary<int, Tuple<double, double>>();
                            for (int i = 0; i < max; i++)
                            {
                                var latRad = Math.PI / 180 * (pixels2.LatLo.DecimalDegree + i * pixels2.LatDelta.DecimalDegree / max);
                                latSinCoses.Add(i, new Tuple<double, double>(Math.Sin(latRad), Math.Cos(latRad)));

                                var lonRad = Math.PI / 180 * (pixels2.LonLo.DecimalDegree + i * pixels2.LonDelta.DecimalDegree / max);
                                lonSinCoses.Add(i, new Tuple<double, double>(Math.Sin(lonRad), Math.Cos(lonRad)));
                            }

                            Vector3d[][] positions = new Vector3d[max][];
                            for (int i = 0; i < max; i++)
                            {
                                positions[i] = new Vector3d[max];
                                var latSinCos = latSinCoses[i];

                                for (int j = 0; j < max; j++)
                                {
                                    var lonSinCos = lonSinCoses[j];
                                    //double height = heig[j][max - 1 - i] + Utils.AlphaMeters;
                                    double height = 5 * heig[i][max - 1 - j] + Utils.AlphaMeters;
                                    positions[i][j].X = height * latSinCos.Item2 * lonSinCos.Item2;
                                    positions[i][j].Y = height * latSinCos.Item2 * lonSinCos.Item1;
                                    positions[i][j].Z = height * latSinCos.Item1;
                                }
                            }

                            CenterAndScale(positions, out Vector3d avgV, out double deltaV);
                            Mesh m = new Mesh(positions);

                            m2 = new MOO(m, avgV, deltaV, template);
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
                            getBitmap2?.Invoke(Utils.GetBitmap(pixels, a => a, OutputType.JPEG));
                            var bm = Utils.GetPlainBitmap(pixels, a => a);
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.WriteLine(ex.Message);
                    }
                }
            }

            return m2;
        }

        private static void CenterAndScale(Vector3d[][] positions, out Vector3d avgV, out double deltaV)
        {
            avgV = new Vector3d(
                positions.SelectMany(p => p).Average(p => p.X),
                positions.SelectMany(p => p).Average(p => p.Y),
                positions.SelectMany(p => p).Average(p => p.Z));

            // Find the max dist between adjacent corners. This will the the characteristic length.
            var cornerDistsSq = new double[]
            {
                positions[0][0].DeltaSq(ref positions[0][positions.Length-1]),
                positions[0][0].DeltaSq(ref positions[positions.Length-1][0]),
                positions[positions.Length-1][positions.Length-1].DeltaSq(ref positions[0][positions.Length-1]),
                positions[positions.Length-1][positions.Length-1].DeltaSq(ref positions[positions.Length-1][0]),
            };
            deltaV = 10.0 / Math.Sqrt(cornerDistsSq.Max());

            for (int i = 0; i < positions.Length; i++)
            {
                for (int j = 0; j < positions.Length; j++)
                {
                    positions[i][j].X = (positions[i][j].X - avgV.X) * deltaV;
                    positions[i][j].Y = (positions[i][j].Y - avgV.Y) * deltaV;
                    positions[i][j].Z = (positions[i][j].Z - avgV.Z) * deltaV;
                }
            }
        }

        public class MOO
        {
            public Vector3d[] Vertices { get; private set; }
            public int[] TriangleIndices { get; private set; }
            public Vector3d[] VertexNormals { get; private set; }
            public Vector2d[] VertexRelatives { get; private set; }

            public MOO(Mesh m, Vector3d avgV, double deltaV, StandardChunkMetadata chunkMetadata)
            {
                Vertices = m.Vertices;
                TriangleIndices = m.TriangleIndices;
                VertexNormals = m.VertexNormals;
                VertexRelatives = m.Vertices
                    .Select(p => Trip.Invert(p, avgV, deltaV))
                    .Select(p =>
                    {
                        var ret = new Vector2d();
                        ret.X = (p.LonDegrees - chunkMetadata.LonLo.DecimalDegree) / chunkMetadata.LonDelta.DecimalDegree;
                        ret.Y = 1-(p.LatDegrees - chunkMetadata.LatLo.DecimalDegree) / chunkMetadata.LatDelta.DecimalDegree;
                        return ret;
                    })
                    .ToArray();
            }

            public struct Vector2d
            {
                public double X;
                public double Y;
            }

            private class Trip
            {
                private const double RadToDeg = 180.0 / Math.PI;
                public double LatDegrees;
                public double LonDegrees;
                public double Height;

                internal static Trip Invert(Vector3d pRel, Vector3d avgV, double deltaV)
                {
                    var pX = pRel.X / deltaV + avgV.X;
                    var pY = pRel.Y / deltaV + avgV.Y;
                    var pZ = pRel.Z / deltaV + avgV.Z;

                    var x2y2 = pX * pX + pY * pY; // = height^2 * latCos^2
                    var h = Math.Sqrt(x2y2 + pZ * pZ);

                    var latSin = pZ / h;
                    // Lat is between -90 and 90 degrees, so latCos is always positive
                    var hLatCos = h * Math.Sqrt(1.0 - latSin * latSin);

                    var lonCos = pX / hLatCos;
                    var lonSin = pY / hLatCos;

                    var lon = Math.Asin(lonSin) * RadToDeg;
                    if (lonCos < 0.0)
                    {
                        lon = (lon > 0 ? 180 : -180) - lon;
                    }

                    return new Trip()
                    {
                        Height = h - Utils.AlphaMeters,
                        LatDegrees = Math.Asin(latSin) * RadToDeg,
                        LonDegrees = lon,
                    };
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

