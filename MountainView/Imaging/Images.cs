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
    internal class Images : CachingHelper<SKColor>
    {
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

        private Images() : base("idata", "Images", 4)
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

        protected override async Task<ChunkHolder<SKColor>> GenerateData(StandardChunkMetadata template)
        {
            await Task.Delay(0);
            ChunkHolder<SKColor> ret = new ChunkHolder<SKColor>(
                template.LatSteps, template.LonSteps,
                template.LatLo, template.LonLo,
                template.LatHi, template.LonHi,
                null,
                Utils.ColorToDoubleArray,
                Utils.ColorFromDoubleArray);

            var targetChunks = GetChunkMetadata()
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

            foreach (var x in targetChunks)
            {
                Console.WriteLine(x.Chunk);
            }

            var chunks = new List<ChunkHolder<SKColor>>();
            foreach (var tmp in targetChunks)
            {
                var col = GetColors(
                    Angle.Add(tmp.Chunk.LatLo, Angle.Divide(tmp.Chunk.LatDelta, 2)),
                    Angle.Add(tmp.Chunk.LonLo, Angle.Divide(tmp.Chunk.LonDelta, 2)));

                if (col != null)
                {
                    chunks.Add(col);
                }
            }

            ret.RenderChunksInto(chunks, Utils.WeightedColorAverage);
            return ret;
        }

        /*
            1. Go to https://earthexplorer.usgs.gov/
            2. Login
            3. For search criteria,
                a. can zoom into map and click "Use Map" in the middle section.
                b. Can also enter map boundaries manually to get more specific map
            4. On bottom, for the "Date Range", use the start date as 09/03/2014, to help limit to a single set.
            5. On next tab "Data Sets", open "Ariel Imagery" and select "NAIP JPEG2000"
            6. On next tab, "results", on "Click here to export results", use "Non-limited results" and CSV, Will show up in the email.
            7. Then open the "Show results control", and click "Add all results from current page to bluk download".
                a. Then click "Next ", and click "all to bulk again.
                b. When done, click "View item basket"
            8. Open the "Bulk Download Application"
            9. Login, select the latest chunk of data.
         */
        private static object colorLock = new object();
        private static ChunkHolder<SKColor> GetColors(Angle lat, Angle lon)
        {
            Console.WriteLine(lat.ToLatString() + " " + lon.ToLonString());
            ImageFileMetadata fileInfo = GetChunkMetadata()
                .Where(p => Utils.Contains(p.Points, lat.DecimalDegree, lon.DecimalDegree))
                .FirstOrDefault();

            if (fileInfo == null)
            {
                throw new InvalidOperationException("Need more data for " + lat.ToLatString() + ", " + lon.ToLonString() + "!");
            }

            if (!File.Exists(fileInfo.FileName2))
            {
                if (!File.Exists(fileInfo.FileName))
                {
                    throw new InvalidOperationException("File should exist: '" + fileInfo.FileName + "'");
                }

                lock (colorLock)
                {
                    if (!File.Exists(fileInfo.FileName2))
                    {
                        Console.WriteLine("Generating " + fileInfo.FileName2);
                        FIBITMAP sdib = FreeImage.LoadEx(fileInfo.FileName);

                        var width = FreeImage.GetWidth(sdib);
                        var height = FreeImage.GetHeight(sdib);
                        Utils.WriteImageFile((int)width, (int)height, fileInfo.FileName2, (i, j) =>
                        {
                            FreeImage.GetPixelColor(sdib, (uint)i, (uint)j, out RGBQUAD value);
                            //Console.WriteLine(i + "\t" + j + "\t" + value.rgbRed + "\t" + value.rgbGreen + "\t" + value.rgbBlue);
                            return new SKColor(value.rgbRed, value.rgbGreen, value.rgbBlue);
                        });
                        FreeImage.UnloadEx(ref sdib);
                    }
                }
            }

            using (SKBitmap bm = SKBitmap.Decode(fileInfo.FileName2))
            {
                return new ChunkHolder<SKColor>(bm.Height, bm.Width,
                    Angle.FromDecimalDegrees(fileInfo.Points.Min(p => p.Item1)),
                    Angle.FromDecimalDegrees(fileInfo.Points.Min(p => p.Item2)),
                    Angle.FromDecimalDegrees(fileInfo.Points.Max(p => p.Item1)),
                    Angle.FromDecimalDegrees(fileInfo.Points.Max(p => p.Item2)),
                    (i, j) => bm.GetPixel(bm.Width - 1 - j, bm.Height - 1 - i),
                    null,
                    null);
            }
        }

        private static IEnumerable<ImageFileMetadata> GetChunkMetadata()
        {
            List<ImageFileMetadata> ret = new List<ImageFileMetadata>();
            string path = Path.Combine(rootMapFolder, "NAIP*");
            DirectoryInfo root = new DirectoryInfo(rootMapFolder);
            foreach (var di in root.GetDirectories("NAIP*"))
            {
                var metadata = di.GetFiles("*.csv").AsEnumerable().First();
                string[] metadataLines = File.ReadAllLines(metadata.FullName);
                string[] header = metadataLines.First().Split(',');
                var nameToIndex = header.Select((p, i) => new { p = p, i = i }).ToDictionary(p => p.p, p => p.i);

                var fileInfo = metadataLines
                    .Skip(1)
                    .Select(p => p.Split(','))
                    .Select(p =>
                        new ImageFileMetadata
                        {
                            FileName = Path.Combine(metadata.Directory.FullName, p[nameToIndex["NAIP Entity ID"]] + ".jp2"),
                            FileName2 = Path.Combine(metadata.Directory.FullName, p[nameToIndex["NAIP Entity ID"]] + ".gif"),
                            Points = new Tuple<double, double>[] {
                                new Tuple<double, double> (double.Parse(p[nameToIndex["NW Corner Lat dec"]]), double.Parse(p[nameToIndex["NW Corner Long dec"]])),
                                new Tuple<double, double> (double.Parse(p[nameToIndex["NE Corner Lat dec"]]), double.Parse(p[nameToIndex["NE Corner Long dec"]])),
                                new Tuple<double, double> (double.Parse(p[nameToIndex["SE Corner Lat dec"]]), double.Parse(p[nameToIndex["SE Corner Long dec"]])),
                                new Tuple<double, double> (double.Parse(p[nameToIndex["SW Corner Lat dec"]]), double.Parse(p[nameToIndex["SW Corner Long dec"]]))
                            }
                        });
                ret.AddRange(fileInfo);
            }

            return ret;
        }

        protected override void WritePixel(MemoryStream stream, SKColor pixel)
        {
            stream.WriteByte(pixel.Red);
            stream.WriteByte(pixel.Green);
            stream.WriteByte(pixel.Blue);
        }

        protected override SKColor ReadPixel(MemoryStream stream, byte[] buffer)
        {
            return new SKColor(
                (byte)stream.ReadByte(),
                (byte)stream.ReadByte(),
                (byte)stream.ReadByte(),
                255);
        }

        private class ImageFileMetadata
        {
            public string FileName;
            public string FileName2;
            public Tuple<double, double>[] Points;
        }
    }
}
