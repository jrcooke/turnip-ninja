using MountainView.Base;
using MountainView.ChunkManagement;
using MountainView.Elevation;
using MountainView.Imaging;
using MountainViewDesktop.Interpolation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using MountainViewCore.Landmarks;

namespace MountainView
{
    class Program
    {
        static void Main(string[] args)
        {
            DateTime start = DateTime.Now;
            int serverLat = 47;
            int serverLon = -123;

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
                //Task.WaitAll(Foo());
                //Tests.Test12();

                //Task.WaitAll(Tests.Test3(     outputPath,
                //    Angle.FromDecimalDegrees(47.6867797),
                //    Angle.FromDecimalDegrees(-122.2907541)));

                if (isServerUpload)
                {
                    string uploadPath = "/home/mcuser/turnip-ninja/MountainView/bin/Debug/netcoreapp2.0";
                    UsgsRawChunks.Uploader(uploadPath, serverLat, serverLon);
                    UsgsRawImageChunks.Uploader(uploadPath, serverLat, serverLon);
                }
                else if (isServerCompute)
                {
                    Task.WaitAll(ProcessRawData(
                        Angle.FromDecimalDegrees(serverLat + 0.5),
                        Angle.FromDecimalDegrees(serverLon - 0.5)));
                }
                else if (isClient)
                {
                    BlobHelper.CacheLocally = true;
                    Config c = Config.Juaneta();
                    Task.WaitAll(GetPolarData(outputPath, c));
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

        public static async Task ProcessRawData(Angle lat, Angle lon)
        {
            // Generate for a 1 degree square region.
            StandardChunkMetadata template = StandardChunkMetadata.GetRangeContaingPoint(lat, lon, 2);
            await Heights.Current.ProcessRawData(template);
            await Images.Current.ProcessRawData(template);
        }

        public static async Task GetPolarData(string outputFolder, Config config)
        {
            double cosLat = Math.Cos(config.Lat.Radians);
            int numR = (int)(config.R / config.DeltaR);

            int iThetaMin = Angle.FloorDivide(config.MinAngle, config.AngularResolution);
            int iThetaMax = Angle.FloorDivide(config.MaxAngle, config.AngularResolution);
            HashSet<long> chunkKeys = new HashSet<long>();


            ColorHeight[][] ret = new ColorHeight[iThetaMax - iThetaMin][];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new ColorHeight[numR];
            }

            for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
            {
                // Use this angle to compute a heading.
                Angle theta = Angle.Multiply(config.AngularResolution, iTheta);
                var endRLat = Utils.DeltaMetersLat(theta, config.R);
                var endRLon = Utils.DeltaMetersLon(theta, config.R, cosLat);

                double cosTheta = Math.Cos(theta.Radians);
                double sinTheta = Math.Sin(theta.Radians);
                for (int iR = 1; iR < numR; iR++)
                {
                    var mult = iR * config.DeltaR / config.R;
                    ret[iTheta - iThetaMin][iR].LatDegrees = config.Lat.DecimalDegree + endRLat.DecimalDegree * mult;
                    ret[iTheta - iThetaMin][iR].LonDegrees = config.Lon.DecimalDegree + endRLon.DecimalDegree * mult;
                    ret[iTheta - iThetaMin][iR].Distance = iR * config.DeltaR;

                    double r = iR * config.DeltaR;
                    var point = Utils.APlusDeltaMeters(config.Lat, config.Lon, r * sinTheta, r * cosTheta, cosLat);
                    double metersPerElement = Math.Max(config.DeltaR / 10, r * config.AngularResolution.Radians);
                    var decimalDegreesPerElement = metersPerElement / (Utils.LengthOfLatDegree * cosLat);
                    var zoomLevel = StandardChunkMetadata.GetZoomLevel(decimalDegreesPerElement);
                    var chunkKey = StandardChunkMetadata.GetKey(point.Item1, point.Item2, zoomLevel);
                    chunkKeys.Add(chunkKey);
                    ret[iTheta - iThetaMin][iR].ChunkKey = chunkKey;

                    StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);
                    using (var hChunk = Heights.Current.GetLazySimpleInterpolator(chunk))
                    {
                        if (hChunk.TryGetIntersectLine(
                            config.Lat.DecimalDegree, endRLat.DecimalDegree,
                            config.Lon.DecimalDegree, endRLon.DecimalDegree,
                            out double loX, out double hiX))
                        {
                            int hiR = Math.Min(numR, (int)(hiX * numR));
                            iR++;
                            while (iR < hiR - 1)
                            {
                                iR++;
                                mult = iR * config.DeltaR / config.R;
                                ret[iTheta - iThetaMin][iR].LatDegrees = config.Lat.DecimalDegree + endRLat.DecimalDegree * mult;
                                ret[iTheta - iThetaMin][iR].LonDegrees = config.Lon.DecimalDegree + endRLon.DecimalDegree * mult;
                                ret[iTheta - iThetaMin][iR].Distance = iR * config.DeltaR;
                                ret[iTheta - iThetaMin][iR].ChunkKey = chunkKey;
                            }
                        }
                    }
                }
            }

            int counter = 0;
            await Utils.ForEachAsync(chunkKeys, 5, async (chunkKey) =>
            {
                await Task.Delay(0);
                StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(chunkKey);

                NearestInterpolatingChunk<float> interpChunkH = null;
                NearestInterpolatingChunk<MyColor> interpChunkI = null;
                try
                {
                    interpChunkH = Heights.Current.GetLazySimpleInterpolator(chunk);
                    interpChunkI = Images.Current.GetLazySimpleInterpolator(chunk);

                    // Now do that again, but do the rendering per chunk.
                    for (int iTheta = iThetaMin; iTheta < iThetaMax; iTheta++)
                    {
                        Angle theta = Angle.Multiply(config.AngularResolution, iTheta);
                        var endRLat = Utils.DeltaMetersLat(theta, config.R);
                        var endRLon = Utils.DeltaMetersLon(theta, config.R, cosLat);

                        // Get intersection between the chunk and the line we are going along.
                        // See which range of R intersects with chunk.
                        if (interpChunkH.TryGetIntersectLine(
                            config.Lat.DecimalDegree, endRLat.DecimalDegree,
                            config.Lon.DecimalDegree, endRLon.DecimalDegree,
                            out double loX, out double hiX))
                        {
                            int loR = Math.Max(1, (int)(loX * numR) + 1);
                            int hiR = Math.Min(numR, (int)(hiX * numR));
                            for (int iR = loR; iR < hiR; iR++)
                            {
                                if (interpChunkH.TryGetDataAtPoint(
                                    ret[iTheta - iThetaMin][iR].LatDegrees,
                                    ret[iTheta - iThetaMin][iR].LonDegrees,
                                    out float data))
                                {
                                    ret[iTheta - iThetaMin][iR].Height = data;
                                }
                            }
                        }
                    }

                    interpChunkH.Dispose();
                    interpChunkI.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                counter++;
                Console.WriteLine(counter + " of " + chunkKeys.Count);
            });

            NewMethod(outputFolder, config, ret, counter);
        }

        private static void NewMethod(string outputFolder, Config config, ColorHeight[][] ret, int counter)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var view = CollapseToViewFromHere(ret, config.DeltaR, config.ElevationViewMin, config.ElevationViewMax, config.AngularResolution);

            Dictionary<long, NearestInterpolatingChunk<MyColor>> images = new Dictionary<long, NearestInterpolatingChunk<MyColor>>();
            try
            {
                // Haze adds bluish overlay to colors. Say (195, 240, 247)
                MyColor skyColor = new MyColor(195, 240, 247);
                var xxx = view.Select(q => q.Select(p =>
                    {
                        if (p.ChunkKey == 0)
                        {
                            return skyColor;
                        }

                        if (!images.TryGetValue(p.ChunkKey, out NearestInterpolatingChunk<MyColor> image))
                        {
                            StandardChunkMetadata chunk = StandardChunkMetadata.GetRangeFromKey(p.ChunkKey);
                            image = Images.Current.GetLazySimpleInterpolator(chunk);
                            images[p.ChunkKey] = image;
                        }

                        image.TryGetDataAtPoint(p.LatDegrees, p.LonDegrees, out MyColor color);
                        double clearWeight = 0.2 + 0.8 / (1.0 + p.Distance * p.Distance * 1.0e-8);
                        return new MyColor(
                            (byte)(int)(color.R * clearWeight + skyColor.R * (1 - clearWeight)),
                            (byte)(int)(color.G * clearWeight + skyColor.G * (1 - clearWeight)),
                            (byte)(int)(color.B * clearWeight + skyColor.B * (1 - clearWeight)));
                    }).ToArray()).ToArray();
                Utils.WriteImageFile(xxx, Path.Combine(outputFolder, "xxi" + counter + ".jpg"), a => a, OutputType.JPEG);
            }
            finally
            {
                foreach (var x in images.Values)
                {
                    x.Dispose();
                }
            }

            var xxx2 = view.Select(q => q.Select(p => p.ChunkKey == 0 ? null : UsgsRawFeatures.GetData(p.LatDegrees, p.LonDegrees)).ToArray()).ToArray();
            var items = xxx2.Where(p => p != null).Distinct().ToArray();
            var idMatrix = xxx2.Select(q => q.Select(p => p?.Id ?? 0).ToArray()).ToArray();

            var cells = GetPolygons<FeatureInfo>(xxx2);

            Utils.WriteImageFile(xxx2, Path.Combine(outputFolder, "xxf" + counter + ".bmp"), p =>
            {
                return p == null ? new MyColor(0, 0, 0) :
                new MyColor(
                    (byte)((p.Id - short.MinValue) / 256 % 256),
                    (byte)((p.Id - short.MinValue) % 256),
                    (byte)((p.Id - short.MinValue) / 256 / 256 % 256));
            }, OutputType.Bitmap);


            /*
    <div id="image_map">
        <map name="map_example">
             <area
                 href="https://facebook.com"
                 alt="Facebook"
                 target="_blank"
                 shape=poly
                 coords="30,100, 140,50, 290,220, 180,280">
             <area
                href="https://en.wikipedia.org/wiki/Social_media"
                target="_blank"
                alt="Wikipedia Social Media Article"
                shape=poly
                coords="190,75, 200,60, 495,60, 495,165, 275,165">
        </map>
         <img
            src="../../wp-content/uploads/image_map_example_shapes.png"
            alt="image map example"
            width=500
            height=332
            usemap="#map_example">
    </div>

             */

        }

        private struct Point
        {
            public int X, Y;
            public Point(int x, int y)
            {
                X = x;
                Y = y;
            }

            public override string ToString()
            {
                return "(" + X + "," + Y + ")";
            }
        }


        private class Polygon<T> where T : class
        {
            public T Value;
            private HashSet<Point> points;

            public int Count { get { return points.Count; } }

            public Polygon(T value)
            {
                Value = value;
                points = new HashSet<Point>();
            }

            public void Add(int x, int y)
            {
                points.Add(new Point(x, y));
            }

            public override string ToString()
            {
                return (Value?.ToString() ?? "<null>") + " has " + points.Count +
                    ", min (x=" + points.Min(p => p.X) + ",y=" + points.Min(p => p.Y) + ")" +
                    ", max (x=" + points.Max(p => p.X) + ",y=" + points.Max(p => p.Y) + ")";
            }

            internal bool ContainsPoint(int i, int j)
            {
                return points.Contains(new Point(i, j));
            }

            //// Moore contour tracing
            //internal IEnumerable<Point> GetBoundary()
            //{
            //    var miny = points.Min(p => p.Y);
            //    var minx = points.Where(p => p.Y == miny).Min(p => p.X);
            //    var startingPoint = new Point(minx, miny);
            //    // At the lower-left.
            //    // Start tracing

            //    HashSet<Point> found = new HashSet<Point>();
            //    List<Point> list = null;
            //    List<List<Point>> lists = new List<List<Point>>();
            //    bool inside = false;

            //    // Defines the neighborhood offset position from current position and the neighborhood
            //    // position we want to check next if we find a new border at checkLocationNr.
            //    int width = size.Width;
            //    Tuple<Func<Point, Point>, int>[] neighborhood = new Tuple<Func<Point, Point>, int>[]
            //    {
            //        new Tuple<Func<Point, Point>, int>(point => new Point(point.X-1,point.Y), 7),
            //        new Tuple<Func<Point, Point>, int>(point => new Point(point.X-1,point.Y-1), 7),
            //        new Tuple<Func<Point, Point>, int>(point => new Point(point.X,point.Y-1), 1),
            //        new Tuple<Func<Point, Point>, int>(point => new Point(point.X+1,point.Y-1), 1),
            //        new Tuple<Func<Point, Point>, int>(point => new Point(point.X+1,point.Y), 3),
            //        new Tuple<Func<Point, Point>, int>(point => new Point(point.X+1,point.Y+1), 3),
            //        new Tuple<Func<Point, Point>, int>(point => new Point(point.X,point.Y+1), 5),
            //        new Tuple<Func<Point, Point>, int>(point => new Point(point.X-1,point.Y+1), 5)
            //    };

            //    for (int y = 0; y < size.Height; ++y)
            //    {
            //        for (int x = 0; x < size.Width; ++x)
            //        {
            //            Point point = new Point(x, y);
            //            // Scan for non-transparent pixel
            //            if (found.Contains(point) && !inside)
            //            {
            //                // Entering an already discovered border
            //                inside = true;
            //                continue;
            //            }
            //            bool isTransparent = pixels.isTransparent(point);
            //            if (!isTransparent && inside)
            //            {
            //                // Already discovered border point
            //                continue;
            //            }
            //            if (isTransparent && inside)
            //            {
            //                // Leaving a border
            //                inside = false;
            //                continue;
            //            }
            //            if (!isTransparent && !inside)
            //            {
            //                lists.Add(list = new List<Point>());

            //                // Undiscovered border point
            //                found.Add(point); list.Add(point);   // Mark the start pixel
            //                int checkLocationNr = 1;  // The neighbor number of the location we want to check for a new border point
            //                Point startPos = point;      // Set start position
            //                int counter = 0;       // Counter is used for the jacobi stop criterion
            //                int counter2 = 0;       // Counter2 is used to determine if the point we have discovered is one single point

            //                // Trace around the neighborhood
            //                while (true)
            //                {
            //                    // The corresponding absolute array address of checkLocationNr
            //                    Point checkPosition = neighborhood[checkLocationNr - 1].Item1(point);
            //                    // Variable that holds the neighborhood position we want to check if we find a new border at checkLocationNr
            //                    int newCheckLocationNr = neighborhood[checkLocationNr - 1].Item2;

            //                    // Beware that the point might be outside the bitmap.
            //                    // The isTransparent method contains the safety check.
            //                    if (!pixels.isTransparent(checkPosition))
            //                    {
            //                        // Next border point found
            //                        if (checkPosition == startPos)
            //                        {
            //                            counter++;

            //                            // Stopping criterion (jacob)
            //                            if (newCheckLocationNr == 1 || counter >= 3)
            //                            {
            //                                // Close loop
            //                                inside = true; // Since we are starting the search at were we first started we must set inside to true
            //                                break;
            //                            }
            //                        }

            //                        checkLocationNr = newCheckLocationNr; // Update which neighborhood position we should check next
            //                        point = checkPosition;
            //                        counter2 = 0;             // Reset the counter that keeps track of how many neighbors we have visited
            //                        found.Add(point); list.Add(point); // Set the border pixel
            //                    }
            //                    else
            //                    {
            //                        // Rotate clockwise in the neighborhood
            //                        checkLocationNr = 1 + (checkLocationNr % 8);
            //                        if (counter2 > 8)
            //                        {
            //                            // If counter2 is above 8 we have traced around the neighborhood and
            //                            // therefor the border is a single black pixel and we can exit
            //                            counter2 = 0;
            //                            list = null;
            //                            break;
            //                        }
            //                        else
            //                        {
            //                            counter2++;
            //                        }
            //                    }
            //                }

            //            }
            //        }
            //    }
            //    return lists;
            //}

        }

        private static IEnumerable<Polygon<T>> GetPolygons<T>(T[][] values) where T : FeatureInfo // class
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
                    if (cache[i][j] == null)
                    {
                        // start a new flood fill
                        var cur = new Polygon<T>(values[i][j]);
                        FloodFill(cur, values, cache, i, j);

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
//                var boundary = poly.GetBoundary();
            }

            Utils.WriteImageFile(width, height, Path.Combine(@"C:\Users\jrcoo\Desktop\Output", "xxfnew.bmp"), (i, j) =>
            {
                var x = cache[i][j];
                return x.Value == null ? new MyColor(0, 0, 0) :
                 new MyColor(
                     (byte)((x.Value.Id - short.MinValue) / 256 % 256),
                     (byte)((x.Value.Id - short.MinValue) % 256),
                     (byte)((x.Value.Id - short.MinValue) / 256 / 256 % 256));
            }, OutputType.Bitmap);




            // Now have cluster.

            return ret.ToArray();
        }

        private static void FloodFill<T>(Polygon<T> cur, T[][] values, Polygon<T>[][] cache, int i, int j) where T : class
        {
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(new Point(i, j));

            while (queue.Count > 0)
            {
                var pt = queue.Dequeue();
                if (pt.X < 0 || pt.Y < 0 || pt.X >= cache.Length || pt.Y >= cache[0].Length) continue;
                if (cache[pt.X][pt.Y] != null) continue;
                if (values[pt.X][pt.Y] != cur.Value) continue;
                cache[pt.X][pt.Y] = cur;
                cur.Add(pt.X, pt.Y);

                queue.Enqueue(new Point(pt.X + 1, pt.Y));
                queue.Enqueue(new Point(pt.X - 1, pt.Y));
                queue.Enqueue(new Point(pt.X, pt.Y + 1));
                queue.Enqueue(new Point(pt.X, pt.Y - 1));
            }
        }

        public struct ColorHeight
        {
            public float Height;
            public double Distance;
            public double LatDegrees;
            public double LonDegrees;
            internal long ChunkKey;
        }

        private static ColorHeight[][] CollapseToViewFromHere(
            ColorHeight[][] thetaRad,
            double deltaR,
            Angle elevationViewMin, Angle elevationViewMax,
            Angle angularRes)
        {
            ColorHeight[][] ret = new ColorHeight[thetaRad.Length][];
            int numParts = (int)((elevationViewMax.Radians - elevationViewMin.Radians) / angularRes.Radians);
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new ColorHeight[numParts];
                float eyeHeight = 10;
                float heightOffset = thetaRad[i][0].Height + eyeHeight;

                int j = 0;
                for (int r = 1; r < thetaRad[i].Length; r++)
                {
                    double curTheta = Math.Atan2(thetaRad[i][r].Height - heightOffset, thetaRad[i][r].Distance);
                    while ((elevationViewMin.Radians + j * angularRes.Radians) < curTheta && j < numParts)
                    {
                        ret[i][j++] = thetaRad[i][r];
                    }
                }
            }

            return ret;
        }
    }
}
