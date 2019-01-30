using System.Collections.Generic;
using System.Linq;

namespace MountainViewCore.Base
{

    public class Polygon<T> where T : class
    {
        public T Value;
        public Point[] Border;
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

        private static readonly int[] deltaX = new int[] { +1, +0, -1, -1, -1, +0, +1, +1 };
        private static readonly int[] deltaY = new int[] { -1, -1, -1, +0, +1, +1, +1, +0 };

        // Moore contour tracing
        public void CacheBoundary(Polygon<T>[][] cache)
        {
            int width = cache.Length;
            int height = cache[0].Length;
            int miny = points.Min(p => p.Y);
            int minx = points.Where(p => p.Y == miny).Min(p => p.X);
            var curPoint = new Point(minx, miny);
            // At the lower-left.

            if (this.Count == 1)
            {
                this.Border = new Point[] { curPoint };
                return;
            }

            int theta = 0;
            int startTheta = 0;
            Point? startPoint = null;
            List<Point> border = new List<Point>();
            while (true)
            {
                Point testPoint = new Point(curPoint.X + deltaX[theta], curPoint.Y + deltaY[theta]);
                if (testPoint.X >= 0 && testPoint.X < width &&
                    testPoint.Y >= 0 && testPoint.Y < height &&
                    cache[testPoint.X][testPoint.Y]?.Value == Value)
                {
                    if (!startPoint.HasValue)
                    {
                        startPoint = testPoint;
                        startTheta = theta;
                    }
                    else if (testPoint.X == startPoint.Value.X && testPoint.Y == startPoint.Value.Y)
                    {
                        if (theta == startTheta) break;
                    }

                    theta = (theta + 5) % 8;
                    curPoint = testPoint;
                    border.Add(curPoint);
                }
                else
                {
                    theta = (theta + 1) % 8;
                }
            }

            var keepers = new List<Point>() { border[0] };
            int index = 1;
            int dx1 = border[index].X - border[index - 1].X;
            int dy1 = border[index].Y - border[index - 1].Y;
            index++;
            while (index < border.Count)
            {
                int dx2 = border[index].X - border[index - 1].X;
                int dy2 = border[index].Y - border[index - 1].Y;
                if (!(dx1 == dx2 && dy1 == dy2))
                {
                    Point cur = border[index - 1];
                    keepers.Add(border[index - 1]);
                    dx1 = dx2;
                    dy1 = dy2;
                }

                index++;
            }

            this.Border = keepers.ToArray();
        }

        public void FloodFill(T[][] values, Polygon<T>[][] cache, int i, int j)
        {
            Queue<Point> queue = new Queue<Point>();
            queue.Enqueue(new Point(i, j));

            while (queue.Count > 0)
            {
                var pt = queue.Dequeue();
                if (pt.X < 0 || pt.Y < 0 || pt.X >= cache.Length || pt.Y >= cache[0].Length) continue;
                if (cache[pt.X][pt.Y] != null) continue;
                if (values[pt.X][pt.Y] != this.Value) continue;
                cache[pt.X][pt.Y] = this;
                this.Add(pt.X, pt.Y);

                queue.Enqueue(new Point(pt.X + 1, pt.Y));
                queue.Enqueue(new Point(pt.X - 1, pt.Y));
                queue.Enqueue(new Point(pt.X, pt.Y + 1));
                queue.Enqueue(new Point(pt.X, pt.Y - 1));
            }
        }


        public struct Point
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
    }
}
