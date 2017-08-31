using MountainView.Base;
using MountainView.ChunkManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace MountainView.Elevation
{
    internal static class UsgsRawChunks
    {
        private const string description = "USGS";
        private const string inputFileTemplate = @"{0}\grd{0}_13\w001001.adf";
        private static readonly string[] sourceUrlTemplates = new string[] {
            @"https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/13/ArcGrid/USGS_NED_13_{0}_ArcGrid.zip",
            @"https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/13/ArcGrid/{0}.zip",
        };
        private const string sourceZipFileTemplate = "USGS_NED_13_{0}_ArcGrid.zip";
        private static string rootMapFolder = ConfigurationManager.AppSettings["RootMapFolder"];

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

                bool missing = false;
                Console.WriteLine("Local " + description + " raw data does not exist: " + fileName);
                Console.WriteLine("Downloading locally...");

                var shortWebFile =
                    (lat > 0 ? 'n' : 's') + ((int)Math.Abs(lat) + 1).ToString("D2") +
                    (lon > 0 ? 'e' : 'w') + ((int)Math.Abs(lon) + 1).ToString("D3");
                string inputFile = Path.Combine(rootMapFolder, string.Format(inputFileTemplate, shortWebFile));
                if (!File.Exists(inputFile))
                {
                    Console.WriteLine("Missing " + description + " data file: " + inputFile);
                    // Need to get fresh data:

                    var target = Path.Combine(rootMapFolder, string.Format(sourceZipFileTemplate, fileName));
                    if (!File.Exists(target))
                    {
                        Console.WriteLine("Attemping to download " + description + " source zip to '" + target + "'...");
                        using (HttpClient client = new HttpClient())
                        {
                            client.Timeout = TimeSpan.FromMinutes(5);
                            HttpResponseMessage message = TryDownloadDifferentFormats(shortWebFile, client).Result;
                            if (message != null && message.StatusCode == HttpStatusCode.OK)
                            {
                                var content = message.Content.ReadAsByteArrayAsync().Result;
                                File.WriteAllBytes(target, content);
                            }
                            else if (message != null && message.StatusCode == HttpStatusCode.NotFound)
                            {
                                missing = true;
                            }
                            else
                            {
                                throw new InvalidOperationException("Bad response: " + (message?.StatusCode.ToString() ?? "No response") + " when trying to get " + shortWebFile);
                            }
                        }

                        if (!missing)
                        {
                            Console.WriteLine("Downloaded " + description + " source zip to '" + target + "'");
                        }
                        else
                        {
                            throw new InvalidOperationException("Source is missing. This is expected when asking for data outside of USA");
                            // Console.WriteLine("Source is missing.");
                        }
                    }

                    if (!missing)
                    {
                        Console.WriteLine("Extracting raw " + description + " data from zip file '" + target + "'...");
                        ZipFile.ExtractToDirectory(target, Path.Combine(rootMapFolder, shortWebFile));
                        Console.WriteLine("Extracted raw " + description + " data from zip file.");
                        //                File.Delete(target);
                    }
                }

                if (!missing)
                {
                    //cache[fileName] = ReadDataToChunks(inputFile);
                    ret = AdfReaderWorker.GetChunk(new FileInfo(inputFile).Directory.ToString());
                    Console.WriteLine("Loaded raw " + description + " data: " + fileName);
                }
                else
                {
                    Console.WriteLine("Data not available.");
                }

                cache.Add(fileName, ret);
            }

            return ret;
        }

        private static async Task<HttpResponseMessage> TryDownloadDifferentFormats(string shortWebFile, HttpClient client)
        {
            HttpResponseMessage message = null;
            for (int i = 0; i < sourceUrlTemplates.Length; i++)
            {
                Uri uri = new Uri(string.Format(sourceUrlTemplates[i], shortWebFile));
                try
                {
                    message = await client.GetAsync(uri);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Problem downloading " + uri.ToString());
                    Console.WriteLine(ex.ToString());
                }

                if (message?.StatusCode == HttpStatusCode.OK)
                {
                    break;
                }
            }

            return message;
        }
    }
}
