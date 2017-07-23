﻿using System;
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


        public static async Task<ChunkHolder<float>> GetRawHeightsInMeters(int lat, int lon)
        {
            string fileName =
                (lat > 0 ? 'n' : 's') + ((int)Math.Abs(lat) + 1).ToString() +
                (lon > 0 ? 'e' : 'w') + ((int)Math.Abs(lon) + 1).ToString();

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
                        HttpResponseMessage message = await TryDownloadDifferentFormats(shortWebFile, client);
                        if (message != null && message.StatusCode == HttpStatusCode.OK)
                        {
                            var content = await message.Content.ReadAsByteArrayAsync();
                            File.WriteAllBytes(target, content);
                        }
                        else if (message != null && message.StatusCode == HttpStatusCode.NotFound)
                        {
                            missing = true;
                        }
                        else
                        {
                            throw new InvalidOperationException("Bad response: " + message.StatusCode.ToString());
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

            ChunkHolder<float> ret = null;
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

            return ret;
        }

        private static async Task<HttpResponseMessage> TryDownloadDifferentFormats(string shortWebFile, HttpClient client)
        {
            HttpResponseMessage message = null;
            for (int i = 0; i < sourceUrlTemplates.Length; i++)
            {
                try
                {
                    message = await client.GetAsync(new Uri(string.Format(sourceUrlTemplates[i], shortWebFile)));
                }
                catch (Exception ex)
                {
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
