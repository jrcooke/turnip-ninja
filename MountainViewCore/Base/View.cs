using MountainView.ChunkManagement;
using MountainView.Mesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MountainView.Base
{
    public static class View
    {
        // Haze adds bluish overlay to colors
        public static MyColor skyColor = new MyColor(195, 240, 247);

        public static long[] GetRelevantChunkKeys(Config config, TraceListener log)
        {
            // Look for all chunks within R of current point.
            var all = GetVisibleChunks(config, config.MinZoom, config.R, log);
            return all;
        }

        private static long[] GetVisibleChunks(Config config, int zoom, double R, TraceListener log)
        {
            int numR = 1000;
            List<long> orderedChunkKeys = new List<long>();
            for (int iR = 1; iR <= numR; iR++)
            {
                for (double iTheta = config.MinAngleDec; iTheta <= config.MaxAngleDec; iTheta += (config.MaxAngleDec - config.MinAngleDec) / numR)
                {
                    var dest = Utils.GetDestFromBearing(config.HomePoint, Angle.FromDecimalDegrees(iTheta), R * iR / numR);
                    var curr = StandardChunkMetadata.GetKey(dest.Lat.Fourths, dest.Lon.Fourths, zoom);
                    if (!orderedChunkKeys.Contains(curr))
                    {
                        orderedChunkKeys.Add(curr);
                    }
                }
            }

            orderedChunkKeys.Reverse();
            return orderedChunkKeys.ToArray();
        }

        //public static string ProcessImageMap(ColorHeight[][] view, string imageName)
        //{
        //    var features = view.Select(q => q.Select(p => p.ChunkKey == 0 ? null : UsgsRawFeatures.GetData(new Vector2d(p.LatDegrees, p.LonDegrees))).ToArray()).ToArray();
        //    var polys = GetPolygons(features);
        //    var polymap = polys
        //        .Where(p => p.Value != null)
        //        .Select(p => new
        //        {
        //            id = p.Value.Id,
        //            alt = p.Value.Name,
        //            coords = string.Join(",", p.Border.Select(q => q.X + "," + (view[0].Length - 1 - q.Y))),
        //        })
        //        .Select(p => "<area href='" + p.alt + "' title='" + p.alt + "' alt='" + p.alt + "' shape='poly' coords='" + p.coords + "' >")
        //        .ToArray();
        //    var mapId = Guid.NewGuid().ToString();

        //    return "<img class='cornerimage' src='" + imageName + "'>";

        //    //var mapText = "<div>" +
        //    //    "<map name='" + mapId + "'>" + string.Join("\r\n", polymap) + "</map>" +
        //    //    "<img src='" + imageName + "' usemap='#" + mapId + "' >" +
        //    //    "</div>";
        //    //            return mapText;
        //}

        public static IEnumerable<Polygon<T>> GetPolygons<T>(T[][] values) where T : class
        {
            int minSize = 100;
            int width = values.Length;
            int height = values[0].Length;

            Polygon<T>[][] cache = new Polygon<T>[width][];
            for (int i = 0; i < width; i++)
            {
                cache[i] = new Polygon<T>[height];
            }

            var ret = new List<Polygon<T>>();
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    if (cache[i][j] == null && values[i][j] != null)
                    {
                        // start a new flood fill
                        var cur = new Polygon<T>(values[i][j]);
                        cur.FloodFill(values, cache, i, j);

                        if (cur.Count < minSize && j > 0)
                        {
                            var replacement = cache[i][j - 1];
                            for (int i2 = Math.Max(0, i - minSize); i2 < Math.Min(width, i + minSize); i2++)
                            {
                                for (int j2 = Math.Max(0, j - minSize); j2 < Math.Min(height, j + minSize); j2++)
                                {
                                    if (cache[i2][j2] == cur)
                                    {
                                        cache[i2][j2] = replacement;
                                    }
                                }
                            }
                        }
                        else
                        {
                            ret.Add(cur);
                        }
                    }
                }
            }

            foreach (var poly in ret)
            {
                poly.CacheBoundary(cache);
            }

            return ret.ToArray();
        }
    }
}
