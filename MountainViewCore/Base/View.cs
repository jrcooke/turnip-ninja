using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Mesh;
using MountainViewCore.Landmarks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MountainViewCore.Base
{
    public static class View
    {
        // Haze adds bluish overlay to colors
        public static MyColor skyColor = new MyColor(195, 240, 247);

        public static long[] GetRelevantChunkKeys(Config config, TraceListener log)
        {
            // Look for all chunks within R of current point.
            var all = NewMethod(config, config.MinZoom, config.R, log);
            return all.Reverse().ToArray();
        }

        private static long[] NewMethod(Config config, int zoom, double R, TraceListener log)
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
                        log?.WriteLine(StandardChunkMetadata.GetRangeFromKey(curr));
                    }
                }
            }

            return orderedChunkKeys.ToArray();
        }

        public static long[] GetRelevantChunkKeysOld(Config config, TraceListener log)
        {
            double cosLat = Math.Cos(config.Lat.Radians);
            int numR = (int)(config.R / config.DeltaR);

            HashSet<long> chunkKeys = new HashSet<long>();
            var distToFarthestPointInChunk = new Dictionary<long, int>();
            var chunkZoom = new Dictionary<long, int>();
            for (int iTheta = 0; iTheta < config.Width; iTheta++)
            {
                // Use this angle to compute a heading.
                Angle theta = Angle.Multiply(config.AngularResolution, iTheta + config.IThetaMin);
                var endRLat = Utils.DeltaMetersLat(theta, config.R);
                var endRLon = Utils.DeltaMetersLon(theta, config.R, cosLat);

                double cosTheta = Math.Cos(theta.Radians);
                double sinTheta = Math.Sin(theta.Radians);
                for (int iR = 1; iR < numR; iR++)
                {
                    double r = iR * config.DeltaR;
                    var point = Utils.APlusDeltaMeters(config.Lat, config.Lon, r * sinTheta, r * cosTheta, cosLat);
                    double metersPerElement = Math.Max(config.DeltaR / 10, r * config.AngularResolution.Radians);

                    var decimalDegreesPerElement = metersPerElement / (Utils.LengthOfLatDegree * cosLat);
                    var zoomLevel = StandardChunkMetadata.GetZoomLevel(decimalDegreesPerElement);
                    zoomLevel = Math.Max(config.MinZoom, zoomLevel);
                    zoomLevel = Math.Min(config.MaxZoom, zoomLevel);

                    var chunkKey = StandardChunkMetadata.GetKey(point.Item1, point.Item2, zoomLevel);
                    StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);

                    if (!chunkKeys.Contains(chunkKey))
                    {
                        chunkKeys.Add(chunkKey);
                        chunkZoom[chunkKey] = zoomLevel;
                        log?.WriteLine("Chunk is " + chunk.ToString());
                    }

                    if (!chunk.TryGetIntersectLine(
                        config.Lat.DecimalDegree, endRLat.DecimalDegree,
                        config.Lon.DecimalDegree, endRLon.DecimalDegree,
                        out double loX, out double hiX))
                    {
                        continue;
                    }

                    if (!distToFarthestPointInChunk.TryGetValue(chunkKey, out int val) || val < (int)(hiX * numR))
                    {
                        distToFarthestPointInChunk[chunkKey] = (int)(hiX * numR);
                    }


                    iR = Math.Max(iR, Math.Min(numR, (int)(hiX * numR)) - 1);
                }
            }

            return (new long[] { 0 }).Union(chunkKeys
                .OrderBy(p => chunkZoom[p])
                .ThenByDescending(p => distToFarthestPointInChunk[p])
                ).ToArray();
        }

        public static string ProcessImageMap(ColorHeight[][] view, string imageName)
        {
            var features = view.Select(q => q.Select(p => p.ChunkKey == 0 ? null : UsgsRawFeatures.GetData(new Vector2d(p.LatDegrees, p.LonDegrees))).ToArray()).ToArray();
            var polys = GetPolygons(features);
            var polymap = polys
                .Where(p => p.Value != null)
                .Select(p => new
                {
                    id = p.Value.Id,
                    alt = p.Value.Name,
                    coords = string.Join(",", p.Border.Select(q => q.X + "," + (view[0].Length - 1 - q.Y))),
                })
                .Select(p => "<area href='" + p.alt + "' title='" + p.alt + "' alt='" + p.alt + "' shape='poly' coords='" + p.coords + "' >")
                .ToArray();
            var mapId = Guid.NewGuid().ToString();

            return "<img class='cornerimage' src='" + imageName + "'>";

            //var mapText = "<div>" +
            //    "<map name='" + mapId + "'>" + string.Join("\r\n", polymap) + "</map>" +
            //    "<img src='" + imageName + "' usemap='#" + mapId + "' >" +
            //    "</div>";
            //            return mapText;
        }

        public static string ProcessImageMapBackdrop(string imageName)
        {
            return "<img class='cornerimage' src='" + imageName + "'>";
        }

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

        public struct ColorHeight
        {
            public float Height;
            public double Distance;
            public double LatDegrees;
            public double LonDegrees;
            public long ChunkKey;
        }

        public struct SparseColorHeight
        {
            public int iTheta;
            public int iViewElev;
            public float Height;
            public double Distance;
            public double LatDegrees;
            public double LonDegrees;
            public long ChunkKey;

            public ColorHeight ToColorHeight()
            {
                return new ColorHeight
                {
                    Height = Height,
                    Distance = Distance,
                    LatDegrees = LatDegrees,
                    LonDegrees = LonDegrees,
                    ChunkKey = ChunkKey,
                };
            }
        }
    }
}
