using MountainView.Mesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MountainView.Base
{
    public class KDNode
    {
        private double median;
        private int key;
        private Vector2d location;
        private readonly int depth;
        private KDNode lChild;
        private KDNode rChild;
        private HyperRect hr;

        private KDNode(Vector2d location, int key, HyperRect hr, int depth)
        {
            this.location = location;
            this.key = key;
            this.hr = hr;
            this.depth = depth;
            median = GetValue(location, depth);
        }

        public static KDNode Process(IEnumerable<Tuple<Vector2d, int>> pointList)
        {
            return Process(pointList.ToArray(), HyperRect.GetInfinite(), 0);
        }

        private GetNearestTuple[] buffs = new GetNearestTuple[100];
        public int GetNearest(ref Vector2d p)
        {
            GetNearestWorker(ref p, 0, buffs);
            return buffs[0].Node.key;
        }

        private struct GetNearestTuple
        {
            public KDNode Node;
            public double DistSq;
        }

        private static KDNode Process(Tuple<Vector2d, int>[] pointList, HyperRect hr, int depth)
        {
            if (pointList.Length == 0) return null;

            // Sort point list and choose median as pivot element
            var sorted = pointList.OrderBy(p => GetValue(p.Item1, depth)).ToArray();
            var medianIndex = sorted.Length / 2;
            var location = sorted[medianIndex];

            // Create node and construct subtree
            var node = new KDNode(sorted[medianIndex].Item1, sorted[medianIndex].Item2, hr, depth);
            var split = hr.Split(node.median, depth);
            if (pointList.Length > 1)
            {
                var lnodes = pointList.Where(p => GetValue(p.Item1, depth) < node.median).ToArray();
                var rnodes = pointList.Where(p => GetValue(p.Item1, depth) >= node.median && p.Item2 != node.key).ToArray();
                node.lChild = Process(lnodes, split.Item1, depth + 1);
                node.rChild = Process(rnodes, split.Item2, depth + 1);
            }

            return node;
        }

        private void GetNearestWorker(ref Vector2d p, int depth, GetNearestTuple[] buffs)
        {
            double val = depth % 2 == 0 ? p.X : p.Y;
            var closestChild = val > median ? rChild : lChild;
            var farthestChild = val > median ? lChild : rChild;

            double dx = p.X - location.X;
            double dy = p.Y - location.Y;
            var distsq = dx * dx + dy * dy;

            buffs[depth].Node = this;
            buffs[depth].DistSq = distsq;
            if (closestChild != null)
            {
                closestChild.GetNearestWorker(ref p, depth + 1, buffs);
                if (buffs[depth].DistSq > buffs[depth + 1].DistSq)
                {
                    buffs[depth].Node = buffs[depth + 1].Node;
                    buffs[depth].DistSq = buffs[depth + 1].DistSq;
                }
            }

            if (farthestChild != null)
            {
                var dX = farthestChild.hr.MinPoint.X > p.X ? p.X - farthestChild.hr.MinPoint.X : farthestChild.hr.MaxPoint.X < p.X ? p.X - farthestChild.hr.MaxPoint.X : 0.0;
                var dY = farthestChild.hr.MinPoint.Y > p.Y ? p.Y - farthestChild.hr.MinPoint.Y : farthestChild.hr.MaxPoint.Y < p.Y ? p.Y - farthestChild.hr.MaxPoint.Y : 0.0;
                var distanceSquaredToTarget = dX * dX + dY * dY;
                if (distanceSquaredToTarget < buffs[depth].DistSq)
                {
                    farthestChild.GetNearestWorker(ref p, depth + 1, buffs);
                    if (buffs[depth].DistSq > buffs[depth + 1].DistSq)
                    {
                        buffs[depth].Node = buffs[depth + 1].Node;
                        buffs[depth].DistSq = buffs[depth + 1].DistSq;
                    }
                }
            }
        }

        private static double GetValue(Vector2d p, int dim)
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
            KDNode root = KDNode.Process(new Tuple<Vector2d, int>[]
            {
                new Tuple<Vector2d, int>(new Vector2d(+0, +0), 0),
                new Tuple<Vector2d, int>(new Vector2d(+1, +1), 1),
                new Tuple<Vector2d, int>(new Vector2d(-1, +1), 2),
                new Tuple<Vector2d, int>(new Vector2d(+1, -1), 3),
                new Tuple<Vector2d, int>(new Vector2d(-1, -1), 4),
                new Tuple<Vector2d, int>(new Vector2d(-2, -0), 5),
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
                    log?.Write(ret);
                }

                log?.WriteLine("");
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
