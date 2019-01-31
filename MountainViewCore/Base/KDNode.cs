using MountainView.Mesh;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MountainView.Base
{
    public class KDNode
    {
        private readonly double median;
        private readonly int key;
        private readonly Vector2d location;
        private readonly int depth;
        private KDNode lChild;
        private KDNode rChild;
        private readonly HyperRect hr;

        private static WorkerState[] stack;

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

        private struct WorkerState
        {
            public KDNode Curr;
            public KDNode NearC;
            public KDNode FarrC;
            public int Address;
            public double DisSq;
            public KDNode Match;
        }

        public int GetNearest(ref Vector2d p)
        {
            if (stack == null)
            {
                stack = new WorkerState[100];
            }

            int stackLevel = 0;
            stack[stackLevel].Curr = this;
            stack[stackLevel].Address = 1;

            while (stackLevel >= 0)
            {
                var curr = stack[stackLevel].Curr;
                var address = stack[stackLevel].Address;
                if (address == 1)
                {
                    stack[stackLevel].Address = 2;

                    double dx = p.X - curr.location.X;
                    double dy = p.Y - curr.location.Y;
                    var distsq = dx * dx + dy * dy;
                    stack[stackLevel].DisSq = distsq;
                    stack[stackLevel].Match = curr;

                    double val = curr.depth % 2 == 0 ? p.X : p.Y;
                    var nearC = val > curr.median ? curr.rChild : curr.lChild;
                    var narrC = val > curr.median ? curr.lChild : curr.rChild;
                    stack[stackLevel].NearC = nearC;
                    stack[stackLevel].FarrC = narrC;

                    if (nearC != null)
                    {
                        stackLevel++;
                        stack[stackLevel].Curr = nearC;
                        stack[stackLevel].Address = 1;
                    }
                }
                else if (address == 2)
                {
                    stack[stackLevel].Address = 3;

                    var farrC = stack[stackLevel].FarrC;
                    if (farrC != null)
                    {
                        var dX = farrC.hr.MinPoint.X > p.X ? p.X - farrC.hr.MinPoint.X : farrC.hr.MaxPoint.X < p.X ? p.X - farrC.hr.MaxPoint.X : 0.0;
                        var dY = farrC.hr.MinPoint.Y > p.Y ? p.Y - farrC.hr.MinPoint.Y : farrC.hr.MaxPoint.Y < p.Y ? p.Y - farrC.hr.MaxPoint.Y : 0.0;
                        var distanceSquaredToTarget = dX * dX + dY * dY;
                        if (distanceSquaredToTarget < stack[stackLevel].DisSq)
                        {
                            stackLevel++;
                            stack[stackLevel].Curr = farrC;
                            stack[stackLevel].Address = 1;
                        }
                    }
                }
                else // if (address == 3)
                {
                    if (stackLevel > 0 && stack[stackLevel - 1].DisSq > stack[stackLevel].DisSq)
                    {
                        stack[stackLevel - 1].Match = stack[stackLevel].Match;
                        stack[stackLevel - 1].DisSq = stack[stackLevel].DisSq;
                    }

                    stackLevel--;
                }
            }

            return stack[0].Match.key;
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
