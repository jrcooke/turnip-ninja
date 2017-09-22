using FreeImageAPI;
using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MountainView.Imaging
{
    internal static class UsgsRawImageChunks
    {
        private const string cachedFileContainer = "sources";

        /*
            1. Go to https://earthexplorer.usgs.gov/
            2. Login
            3. For search criteria,
                a. can zoom into map and click "Use Map" in the middle section.
                b. Can also enter map boundaries manually to get more specific map
            4. On bottom, for the "Date Range", use the start date as 09/03/2014, to help limit to a single set.
            5. On next tab "Data Sets", open "Ariel Imagery" and select "NAIP JPEG2000"
            6. On next tab, "results", on "Click here to export results", use "Non-limited results" and CSV, Will show up in the email.
            7. Then open the "Show results control", and click "Add all results from current page to bulk download".
                a. Then click "Next ", and click "all to bulk again.
                b. When done, click "View item basket"
            8. Open the "Bulk Download Application"
            9. Login, select the latest chunk of data.
         */

        private static Dictionary<string, ChunkHolder<MyColor>> cache = new Dictionary<string, ChunkHolder<MyColor>>();

        private static object generalLock = new object();
        private static Dictionary<string, object> specificLocks = new Dictionary<string, object>();

        public static async Task<ChunkHolder<MyColor>> GetRawColors(Angle lat, Angle lon)
        {
            ImageFileMetadata fileInfo = (await GetChunkMetadata())
                .Where(p => Utils.Contains(p.Points, lat.DecimalDegree, lon.DecimalDegree))
                .FirstOrDefault();

            if (fileInfo == null)
            {
                throw new InvalidOperationException("Need more data for " + lat.ToLatString() + ", " + lon.ToLonString() + "!");
            }

            if (!File.Exists(fileInfo.LocalName))
            {
                using (var ms = await BlobHelper.TryGetStream(cachedFileContainer, fileInfo.FileName))
                {
                    if (ms == null)
                    {
                        throw new InvalidOperationException("File should exist: '" + fileInfo.FileName + "'");
                    }

                    using (var fileStream = File.Create(fileInfo.LocalName))
                    {
                        ms.Position = 0;
                        ms.CopyTo(fileStream);
                    }
                }
            }

            string fileName = fileInfo.LocalName;
            if (cache.TryGetValue(fileName, out ChunkHolder<MyColor> ret))
            {
                return ret;
            }

            object specificLock = null;
            lock (generalLock)
            {
                if (!specificLocks.TryGetValue(fileName, out specificLock))
                {
                    specificLock = new object();
                    specificLocks.Add(fileName, specificLock);
                }
            }

            lock (specificLock)
            {
                if (cache.TryGetValue(fileName, out ret))
                {
                    return ret;
                }

                // Now the real compute starts
                Console.WriteLine("Reading into cache: " + fileName);
                FIBITMAP sdib = FreeImage.LoadEx(fileName);
                try
                {
                    var width = (int)FreeImage.GetWidth(sdib);
                    var height = (int)FreeImage.GetHeight(sdib);
                    ret = new ChunkHolder<MyColor>(height, width,
                        Angle.FromDecimalDegrees(fileInfo.Points.Min(p => p.Item1)),
                        Angle.FromDecimalDegrees(fileInfo.Points.Min(p => p.Item2)),
                        Angle.FromDecimalDegrees(fileInfo.Points.Max(p => p.Item1)),
                        Angle.FromDecimalDegrees(fileInfo.Points.Max(p => p.Item2)),
                        (i, j) =>
                        {
                            FreeImage.GetPixelColor(sdib, (uint)(width - 1 - j), (uint)(i), out RGBQUAD value);
                            return new MyColor(value.rgbRed, value.rgbGreen, value.rgbBlue);
                        },
                        Utils.ColorToDoubleArray,
                        Utils.ColorFromDoubleArray);
                }
                finally
                {
                    FreeImage.UnloadEx(ref sdib);
                }

                cache.Add(fileName, ret);
            }

            return ret;
        }

        public static IEnumerable<string> ReadLines(Func<Stream> streamProvider)
        {
            using (var stream = streamProvider())
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        public static async Task<IEnumerable<ImageFileMetadata>> GetChunkMetadata()
        {
            List<ImageFileMetadata> ret = new List<ImageFileMetadata>();

            //            string rootMapFolder = @"C:\Users\jrcoo\Desktop\Map";
            //            DirectoryInfo root = new DirectoryInfo(rootMapFolder);
            foreach (var di in await BlobHelper.GetDirectories(cachedFileContainer, "NAIP"))
            {
                var files = await BlobHelper.GetFiles(cachedFileContainer, di);
                foreach (var metadata in files.Where(p => p.EndsWith(".csv")))
                {
                    var metadataLines = await BlobHelper.ReadAllLines(cachedFileContainer, metadata);
                    string[] header = metadataLines.First().Split(',');
                    var nameToIndex = header.Select((p, i) => new { p = p, i = i }).ToDictionary(p => p.p, p => p.i);
                    var fileInfo = metadataLines
                        .Skip(1)
                        .Select(p => p.Split(','))
                        .Select(p =>
                            new ImageFileMetadata
                            {
                                FileName = di + "/" + p[nameToIndex["NAIP Entity ID"]].ToLowerInvariant() + ".jp2",
                                Points = new Tuple<double, double>[] {
                                    new Tuple<double, double> (double.Parse(p[nameToIndex["NW Corner Lat dec"]]), double.Parse(p[nameToIndex["NW Corner Long dec"]])),
                                    new Tuple<double, double> (double.Parse(p[nameToIndex["NE Corner Lat dec"]]), double.Parse(p[nameToIndex["NE Corner Long dec"]])),
                                    new Tuple<double, double> (double.Parse(p[nameToIndex["SE Corner Lat dec"]]), double.Parse(p[nameToIndex["SE Corner Long dec"]])),
                                    new Tuple<double, double> (double.Parse(p[nameToIndex["SW Corner Lat dec"]]), double.Parse(p[nameToIndex["SW Corner Long dec"]]))
                                }
                            });
                    ret.AddRange(fileInfo);
                }
            }

            return ret;
        }

        public class ImageFileMetadata
        {
            public string FileName;
            public string LocalName { get { return FileName.Replace("/", "_"); } }
            public Tuple<double, double>[] Points;
        }
    }
}
