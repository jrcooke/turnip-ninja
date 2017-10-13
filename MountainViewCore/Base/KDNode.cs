using System;
using System.Collections.Generic;
using System.Linq;

namespace MountainView.Base
{
    public class KDNode<T>
    {
        private double median;
        private Point location;
        private int depth;
        private KDNode<T> lChild;
        private KDNode<T> rChild;
        private HyperRect hr;

        private KDNode(Point location, HyperRect hr, int depth)
        {
            this.location = location;
            this.hr = hr;
            this.depth = depth;
            median = GetValue(location, depth);
        }

        public static KDNode<T> Process(IEnumerable<Point> pointList)
        {
            return Process(pointList.ToArray(), HyperRect.GetInfinite(pointList.First().Vector.Length), 0);
        }

        public Point GetNearest(params double[] v)
        {
            return GetNearest(new Point(v)).Item1.location;
        }

        private static KDNode<T> Process(Point[] pointList, HyperRect hr, int depth)
        {
            if (pointList.Length == 0) return null;

            // Sort point list and choose median as pivot element
            var sorted = pointList.OrderBy(p => GetValue(p, depth)).ToArray();
            var medianIndex = sorted.Length / 2;
            var location = sorted[medianIndex];

            // Create node and construct subtree
            var node = new KDNode<T>(sorted[medianIndex], hr, depth);
            var split = hr.Split(node.median, depth);
            if (pointList.Length > 1)
            {
                var lnodes = pointList.Where(p => GetValue(p, depth) < node.median).ToArray();
                var rnodes = pointList.Where(p => GetValue(p, depth) >= node.median && p != node.location).ToArray();
                node.lChild = Process(lnodes, split.Item1, depth + 1);
                node.rChild = Process(rnodes, split.Item2, depth + 1);
            }

            return node;
        }

        private Tuple<KDNode<T>, double> GetNearest(Point p)
        {
            double val = GetValue(p, depth);
            var closestChild = val > median ? this.rChild : this.lChild;
            var farthestChild = val > median ? this.lChild : this.rChild;
            var best = new Tuple<KDNode<T>, double>(this, p.DistanceSqTo(this.location));
            if (closestChild != null)
            {
                var c1Best = closestChild.GetNearest(p);
                if (best.Item2 > c1Best.Item2)
                {
                    best = c1Best;
                }
            }

            if (farthestChild != null)
            {
                var distanceSquaredToTarget = farthestChild.hr.GetDistSqToClosestPoint(p);
                if (distanceSquaredToTarget < best.Item2)
                {
                    var c2Best = farthestChild.GetNearest(p);
                    if (best.Item2 > c2Best.Item2)
                    {
                        best = c2Best;
                    }
                }
            }

            return best;
        }

        private static double GetValue(Point p, int dim)
        {
            return p.Vector[dim % p.Vector.Length];
        }

        public override string ToString()
        {
            return "".PadLeft(2 * depth) + median + " : " + location + "\r\n" +
                (lChild == null ? "".PadLeft(2 * (1 + depth)) + "null" : lChild.ToString()) + "\r\n" +
                (rChild == null ? "".PadLeft(2 * (1 + depth)) + "null" : rChild.ToString());
        }

        public static void Test()
        {
            KDNode<int> root = KDNode<int>.Process(new KDNode<int>.Point[]
            {
                KDNode<int>.Point.WithKey(0, +0, +0),
                KDNode<int>.Point.WithKey(1, +1, +1),
                KDNode<int>.Point.WithKey(2, -1, +1),
                KDNode<int>.Point.WithKey(3, +1, -1),
                KDNode<int>.Point.WithKey(4, -1, -1),
                KDNode<int>.Point.WithKey(5, -2, -0),
            });

            Console.WriteLine(root);
            double x = -2.0;
            double y = 0.1;
            for (y = 2.0; y > -2; y -= 0.25)
            {
                for (x = -3.0; x < 3; x += 0.25)
                {
                    var ret = root.GetNearest(x, y);
                    Console.Write(ret.Key);
                }

                Console.WriteLine();
            }
        }

        public class Point
        {
            public double[] Vector { get; private set; }
            public T Key { get; private set; }

            public Point(params double[] vector)
            {
                this.Vector = vector;
            }

            public static Point WithKey(T key, params double[] vector)
            {
                return new Point(vector) { Key = key };
            }

            public double DistanceSqTo(Point b)
            {
                var v1 = Vector;
                var v2 = b.Vector;
                if (v1.Length != v2.Length) throw new InvalidOperationException();

                double ret = 0.0;
                for (int i = 0; i < v1.Length; i++)
                {
                    var tmp = v1[i] - v2[i];
                    ret += tmp * tmp;
                }

                return ret;
            }

            public override string ToString()
            {
                return Key + " at (" + string.Join(",", Vector) + ")";
            }
        }

        private class HyperRect
        {
            private int len;
            public double[] MinPoint { get; private set; }
            public double[] MaxPoint { get; private set; }

            private HyperRect(double[] minPoint, double[] maxPoint)
            {
                this.len = minPoint.Length;
                this.MinPoint = minPoint;
                this.MaxPoint = maxPoint;
            }

            public static HyperRect GetInfinite(int len)
            {
                var ret = new HyperRect(new double[len], new double[len]);
                for (var i = 0; i < len; i++)
                {
                    ret.MinPoint[i] = double.NegativeInfinity;
                    ret.MaxPoint[i] = double.PositiveInfinity;
                }

                return ret;
            }

            public double GetDistSqToClosestPoint(Point p)
            {
                double ret = 0.0;
                double tmp = 0.0;
                for (var i = 0; i < len; i++)
                {
                    double pi = p.Vector[i];
                    if (MinPoint[i] > pi)
                    {
                        tmp = pi - MinPoint[i];
                        ret += tmp * tmp;
                    }
                    else if (MaxPoint[i] < pi)
                    {
                        tmp = pi - MaxPoint[i];
                        ret += tmp * tmp;
                    }
                }

                return ret;
            }

            public Tuple<HyperRect, HyperRect> Split(double x, int dim)
            {
                var lRect = new HyperRect((double[])this.MinPoint.Clone(), (double[])this.MaxPoint.Clone());
                lRect.MaxPoint[dim % lRect.MaxPoint.Length] = x;

                var rRect = new HyperRect((double[])this.MinPoint.Clone(), (double[])this.MaxPoint.Clone());
                rRect.MinPoint[dim % lRect.MaxPoint.Length] = x;

                return new Tuple<HyperRect, HyperRect>(lRect, rRect);
            }
        }
    }
}
