using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace MountainView.Elevation
{
    public static class UsgsRawChunks
    {
        // https://viewer.nationalmap.gov/basic/?basemap=b1&category=ned,nedsrc&title=3DEP%20View
        //
        // Need to add one to each angle, so for 46.8N 121.7W is in
        // https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/13/ArcGrid/n47w122.zip

        private const string description = "USGS";
        private static readonly string[] inputFileTemplate = new string[] { "{0}", "grd{0}_13", "w001001.adf" };
        private const string sourceZipFileTemplate = "USGS_NED_13_{0}_ArcGrid.zip";
        private const string cachedFileContainer = "sources";

        private static Dictionary<string, ChunkHolder<float>> cache = new Dictionary<string, ChunkHolder<float>>();

        private static object generalLock = new object();
        private static Dictionary<string, object> specificLocks = new Dictionary<string, object>();

        public static async Task<ChunkHolder<float>> GetRawHeightsInMeters(int lat, int lon)
        {
            string fileName =
                (lat > 0 ? 'n' : 's') + ((int)Math.Abs(lat) + 1).ToString() +
                (lon > 0 ? 'e' : 'w') + ((int)Math.Abs(lon) + 1).ToString();

            var zipFile = string.Format(sourceZipFileTemplate, fileName);
            if (!File.Exists(zipFile))
            {
                using (var ms = await BlobHelper.TryGetStream(cachedFileContainer, zipFile))
                {
                    if (ms == null)
                    {
                        throw new InvalidOperationException("File should exist: '" + zipFile + "'");
                    }

                    using (var fileStream = File.Create(zipFile))
                    {
                        ms.Position = 0;
                        ms.CopyTo(fileStream);
                    }
                }
            }

            if (cache.TryGetValue(fileName, out ChunkHolder<float> ret))
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

                var shortWebFile =
                    (lat > 0 ? 'n' : 's') + ((int)Math.Abs(lat) + 1).ToString("D2") +
                    (lon > 0 ? 'e' : 'w') + ((int)Math.Abs(lon) + 1).ToString("D3");
                string inputFile = string.Format(Path.Combine(inputFileTemplate), shortWebFile);
                if (!File.Exists(inputFile))
                {
                    Console.WriteLine("Missing " + description + " data file: " + inputFile);
                    Console.WriteLine("Extracting raw " + description + " data from zip file '" + zipFile + "'...");
                    ZipFile.ExtractToDirectory(zipFile, shortWebFile);
                    Console.WriteLine("Extracted raw " + description + " data from zip file.");
                }

                ret = AdfReaderWorker.GetChunk(new FileInfo(inputFile).Directory.ToString());
                Console.WriteLine("Loaded raw " + description + " data: " + fileName);
                cache.Add(fileName, ret);
            }

            return ret;
        }

        public static void Uploader(string path, int lat, int lon)
        {
            var shortWebFile =
                (lat > 0 ? 'n' : 's') + ((int)Math.Abs(lat) + 1).ToString("D2") +
                (lon > 0 ? 'e' : 'w') + ((int)Math.Abs(lon) + 1).ToString("D3");

            foreach (var x in Directory.GetFiles(path).Where(p => p.Split(Path.DirectorySeparatorChar).Last() == shortWebFile + ".zip"))
            {
                Console.WriteLine(x);
                using (var ms = new MemoryStream())
                {
                    using (var fs = File.OpenRead(x))
                    {
                        fs.CopyTo(ms);
                        ms.Position = 0;
                        var blobFile = string.Format(sourceZipFileTemplate, shortWebFile);
                        Task.WaitAll(BlobHelper.WriteStream("sources", blobFile, ms));
                    }
                }
            }
        }
    }
}
