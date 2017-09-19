using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace MountainView.Elevation
{
    internal static class UsgsRawChunks
    {
        // https://viewer.nationalmap.gov/basic/?basemap=b1&category=ned,nedsrc&title=3DEP%20View

        private const string description = "USGS";
        private static readonly string[] inputFileTemplate = new string[] { "{0}", "grd{0}_13", "w001001.adf" };
        private const string sourceZipFileTemplate = "USGS_NED_13_{0}_ArcGrid.zip";
        private static string rootMapFolder = Directory.GetCurrentDirectory();//  @"C:\Users\jrcoo\Desktop\Map";

        private static Dictionary<string, ChunkHolder<float>> cache = new Dictionary<string, ChunkHolder<float>>();

        private static object generalLock = new object();
        private static Dictionary<string, object> specificLocks = new Dictionary<string, object>();

        public static ChunkHolder<float> GetRawHeightsInMeters(int lat, int lon)
        {
            string fileName =
                (lat > 0 ? 'n' : 's') + ((int)Math.Abs(lat) + 1).ToString() +
                (lon > 0 ? 'e' : 'w') + ((int)Math.Abs(lon) + 1).ToString();

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
                string inputFile = Path.Combine(rootMapFolder, string.Format(Path.Combine(inputFileTemplate), shortWebFile));
                if (!File.Exists(inputFile))
                {
                    Console.WriteLine("Missing " + description + " data file: " + inputFile);
                    var target = Path.Combine(rootMapFolder, string.Format(sourceZipFileTemplate, fileName));
                    if (!File.Exists(target))
                    {
                        throw new InvalidOperationException("File missing: " + target);
                    }

                    Console.WriteLine("Extracting raw " + description + " data from zip file '" + target + "'...");
                    ZipFile.ExtractToDirectory(target, Path.Combine(rootMapFolder, shortWebFile));
                    Console.WriteLine("Extracted raw " + description + " data from zip file.");
                }

                ret = AdfReaderWorker.GetChunk(new FileInfo(inputFile).Directory.ToString());
                Console.WriteLine("Loaded raw " + description + " data: " + fileName);
                cache.Add(fileName, ret);
            }

            return ret;
        }
    }
}
