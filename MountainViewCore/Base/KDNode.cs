using MountainView.Mesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MountainView.Base
{
    public class KDNode<T>
    {
        private double median;
        private Point location;
        private readonly int depth;
        private KDNode<T> lChild;
        private KDNode<T> rChild;
        private HyperRect hr;

        private KDNode(Point location, HyperRect hr, int depth)
        {
            this.location = location;
            this.hr = hr;
            this.depth = depth;
            median = GetValue(ref location.Location, depth);
        }

        public static KDNode<T> Process(IEnumerable<Point> pointList)
        {
            return Process(pointList.ToArray(), HyperRect.GetInfinite(), 0);
        }

        public Point GetNearest(ref Vector2d p)
        {
            return GetNearestWorker(ref p).Item1.location;
        }

        private static KDNode<T> Process(Point[] pointList, HyperRect hr, int depth)
        {
            if (pointList.Length == 0) return null;

            // Sort point list and choose median as pivot element
            var sorted = pointList.OrderBy(p => GetValue(ref p.Location, depth)).ToArray();
            var medianIndex = sorted.Length / 2;
            var location = sorted[medianIndex];

            // Create node and construct subtree
            var node = new KDNode<T>(sorted[medianIndex], hr, depth);
            var split = hr.Split(node.median, depth);
            if (pointList.Length > 1)
            {
                var lnodes = pointList.Where(p => GetValue(ref p.Location, depth) < node.median).ToArray();
                var rnodes = pointList.Where(p => GetValue(ref p.Location, depth) >= node.median && p != node.location).ToArray();
                node.lChild = Process(lnodes, split.Item1, depth + 1);
                node.rChild = Process(rnodes, split.Item2, depth + 1);
            }

            return node;
        }

        private Tuple<KDNode<T>, double> GetNearestWorker(ref Vector2d p)
        {
            double val = GetValue(ref p, depth);
            var closestChild = val > median ? this.rChild : this.lChild;
            var farthestChild = val > median ? this.lChild : this.rChild;
            var best = new Tuple<KDNode<T>, double>(this, p.DistSqTo(ref this.location.Location));
            if (closestChild != null)
            {
                var c1Best = closestChild.GetNearestWorker(ref p);
                if (best.Item2 > c1Best.Item2)
                {
                    best = c1Best;
                }
            }

            if (farthestChild != null)
            {
                var distanceSquaredToTarget = farthestChild.hr.GetDistSqToClosestPoint(ref p);
                if (distanceSquaredToTarget < best.Item2)
                {
                    var c2Best = farthestChild.GetNearestWorker(ref p);
                    if (best.Item2 > c2Best.Item2)
                    {
                        best = c2Best;
                    }
                }
            }

            return best;
        }

        private static double GetValue(ref Vector2d p, int dim)
        {
            return dim % 2 == 0 ? p.X : p.Y;
        }

        public override string ToString()
        {
            return "".PadLeft(2 * depth) + median + " : " + location + "\r\n" +
                (lChild == null ? "".PadLeft(2 * (1 + depth)) + "null" : lChild.ToString()) + "\r\n" +
                (rChild == null ? "".PadLeft(2 * (1 + depth)) + "null" : rChild.ToString());
        }

        public static void Test(TraceListener log)
        {
            KDNode<int> root = KDNode<int>.Process(new KDNode<int>.Point[]
            {
                new KDNode<int>.Point(new Vector2d(+0, +0), 0),
                new KDNode<int>.Point(new Vector2d(+1, +1), 1),
                new KDNode<int>.Point(new Vector2d(-1, +1), 2),
                new KDNode<int>.Point(new Vector2d(+1, -1), 3),
                new KDNode<int>.Point(new Vector2d(-1, -1), 4),
                new KDNode<int>.Point(new Vector2d(-2, -0), 5),
            });

            log?.WriteLine(root);
            double x = -2.0;
            double y = 0.1;
            Vector2d buff = new Vector2d();
            for (y = 2.0; y > -2; y -= 0.25)
            {
                for (x = -3.0; x < 3; x += 0.25)
                {
                    buff.X = x;
                    buff.Y = y;
                    var ret = root.GetNearest(ref buff);
                    log?.Write(ret.Key);
                }

                log?.WriteLine("");
            }
        }

        public class Point
        {
            public Vector2d Location;
            //public double[] Vector { get; private set; }
            public T Key { get; private set; }

            public Point(Vector2d location, T key)
            {
                Location = location;
                Key = key;
            }

            public double DistanceSqTo(Point b)
            {
                return Location.DistSqTo(ref b.Location);
            }

            public override string ToString()
            {
                return Key + " at (" + Location.X + "," + Location.Y + ")";
            }
        }

        private class HyperRect
        {
            public Vector2d MinPoint;
            public Vector2d MaxPoint;

            private HyperRect(Vector2d minPoint, Vector2d maxPoint)
            {
                MinPoint = minPoint;
                MaxPoint = maxPoint;
            }

            public static HyperRect GetInfinite()
            {
                return new HyperRect(
                    new Vector2d(double.NegativeInfinity, double.NegativeInfinity),
                    new Vector2d(double.PositiveInfinity, double.PositiveInfinity));
            }

            public double GetDistSqToClosestPoint(ref Vector2d p)
            {
                var dX = MinPoint.X > p.X ? p.X - MinPoint.X : MaxPoint.X < p.X ? p.X - MaxPoint.X : 0.0;
                var dY = MinPoint.Y > p.Y ? p.Y - MinPoint.Y : MaxPoint.Y < p.Y ? p.Y - MaxPoint.Y : 0.0;
                var ret = dX * dX + dY * dY;
                return ret;
            }

            public Tuple<HyperRect, HyperRect> Split(double x, int dim)
            {
                var lRect = new HyperRect(MinPoint, MaxPoint);
                var rRect = new HyperRect(MinPoint, MaxPoint);

                if (dim % 2 == 0)
                {
                    lRect.MaxPoint.X = x;
                    rRect.MinPoint.X = x;
                }
                else
                {
                    lRect.MaxPoint.Y = x;
                    rRect.MinPoint.Y = x;
                }

                return new Tuple<HyperRect, HyperRect>(lRect, rRect);
            }
        }
    }
}
