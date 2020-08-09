using MountainView.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// The logic for unfolding the graph comes from the following:
// https://github.com/felixfeliz/paperfoldmodels
// https://geom.ivd.kit.edu/downloads/proj-paper-models_cut_out_sheets.pdf

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            var mesh = new Mesh();

            bool test = false;

            if (test)
            {
                mesh.Add(new Tri3(
                    new Vec3(0, 0, 0),
                    new Vec3(1, 0, 0),
                    new Vec3(0, 1, 1)));
                mesh.Add(new Tri3(
                    new Vec3(1, 0, 0),
                    new Vec3(1, 1, 0),
                    new Vec3(0, 1, 1)));
                mesh.Add(new Tri3(
                    new Vec3(0, -1, -1),
                    new Vec3(1, -1, 0),
                    new Vec3(0, 0, 0)));
                mesh.Add(new Tri3(
                    new Vec3(1, -1, 0),
                    new Vec3(1, 0, 0),
                    new Vec3(0, 0, 0)));
            }
            else
            {
                Random r = new Random(4);
                int n = 10;
                double[,] heights = new double[n, n];
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        heights[i, j] = 2 * r.NextDouble();
                    }
                }

                for (int i = 0; i < n - 1; i++)
                {
                    for (int j = 0; j < n - 1; j++)
                    {
                        mesh.Add(new Tri3(
                            new Vec3(i, j, heights[i, j]),
                            new Vec3(i + 1, j, heights[i + 1, j]),
                            new Vec3(i, j + 1, heights[i, j + 1])));
                        mesh.Add(new Tri3(
                            new Vec3(i + 1, j + 1, heights[i + 1, j + 1]),
                            new Vec3(i + 1, j, heights[i + 1, j]),
                            new Vec3(i, j + 1, heights[i, j + 1])));
                    }
                }
            }

            var spanTree = mesh.GetSpanningTree();
            Console.WriteLine("spanning tree faces");
            foreach (var x in spanTree.Faces)
            {
                Console.WriteLine(x.Index);
            }
            Console.WriteLine("spanning tree dual edges");
            foreach (var x in spanTree.Edges)
            {
                Console.WriteLine(string.Join(" to ", x.VertexIndices));
            }

            var baseFlatTree = Flatten(mesh, spanTree, mesh.Faces.First());
            foreach (var x in baseFlatTree)
            {
                Console.WriteLine(x.Space.Index);
                Console.WriteLine(x.Space);
                Console.WriteLine(x.Paper);
            }

            var flatTrees = unfold(spanTree, baseFlatTree);

            var rawminX = baseFlatTree.SelectMany(p => p.Paper.Vertices).Min(p => p.X);
            var rawmaxX = baseFlatTree.SelectMany(p => p.Paper.Vertices).Max(p => p.X);
            var rawminY = baseFlatTree.SelectMany(p => p.Paper.Vertices).Min(p => p.Y);
            var rawmaxY = baseFlatTree.SelectMany(p => p.Paper.Vertices).Max(p => p.Y);

            int counter = 0;
            foreach (var flatTree in flatTrees)
            {
                var rawoffset = new Vec2(rawminX, rawminY);
                var rawwidth = rawmaxX - rawminX;
                var rawheight = rawmaxY - rawminY;
                var width = 2000;
                var height = 1000;

                double scale = Math.Min(height / rawheight, width / rawwidth);
                Vec3 eye = new Vec3(0, 0, 6);

                var fileName = @"c:\temp\goo" + (counter++) + ".png";

                bool onlyEdge = false;
                using (DirectBitmap bm = new DirectBitmap(width, height))
                {
                    foreach (var containingTri in flatTree.OrderByDescending(p => p.TabId)) // Put tabs first go get overwritten if needed
                    {
                        // Get simple bounding box for this triangle
                        var bb = containingTri.GetBoundingBox();

                        for (var y = (int)((bb.MinY - rawminY) * scale) - 1; y <= (int)((bb.MaxY - rawminY) * scale) + 1; y++)
                        {
                            var sy = y / scale + rawminY;
                            for (var x = (int)((bb.MinX - rawminX) * scale) - 1; x <= (int)((bb.MaxX - rawminX) * scale) + 1; x++)
                            {
                                var sx = x / scale + rawminX;
                                if (containingTri.Paper.PointInTriangle(sx, sy))
                                {
                                    var distToEdge = containingTri.Paper.DistToClosestEdge(sx, sy);
                                    if (onlyEdge)
                                    {
                                        if (distToEdge < 0.01)
                                        {
                                            MyColor c = new MyColor(0, 0, 0);
                                            bm.SetPixel(x, y, c);
                                        }
                                    }
                                    else
                                    {
                                        var s = containingTri.PaperToSpace(sx, sy);

                                        MyColor c;
                                        if (containingTri.TabId.HasValue)
                                        {
                                            // This is a tab triangle
                                            // Determine distance to common edge

                                            var distToCommon = s.DistanceFromPointToSegment(containingTri.CommonSpace);
                                            var alpha = (byte)(Math.Max(0, 1 - distToCommon) * 255);

                                        //    Console.WriteLine(distToCommon);
                                            //c = new MyColor(255, 0, 0);
                                            c = GetColor(containingTri.TabId.Value, 0);
                                            c = new MyColor(c.R, c.G, c.B, alpha);
                                        }
                                        else
                                        {
                                            var ray = s.Subtract(eye).Unit();
                                            var vx = Math.Asin(ray.X);
                                            var vy = Math.Asin(ray.Y);
                                            c = GetColor(vx, vy);
                                        }
                                        if (distToEdge < 0.01)
                                        {
                                            c = new MyColor(0, 0, 0);
                                        }
                                        bm.SetPixel(x, y, c);
                                    }
                                }
                            }
                        }
                    }

                    File.Delete(fileName);
                    using (FileStream stream = File.OpenWrite(fileName))
                    {
                        bm.WriteFile(OutputType.PNG, stream);
                    }
                }
            }
        }

        public static IEnumerable<IEnumerable<BothTri>> unfold(SpanningTree spanningTree, IEnumerable<BothTri> faces)
        {
            var epsilon = 1E-12; // accuracy
            var faceIntersections = new List<Tuple<int, int>>();
            foreach (var face1 in faces)
            {
                foreach (var face2 in faces.Where(p => p.Index < face1.Index))
                {
                    if (face1.Paper.TriangleIntersection(face2.Paper, epsilon))
                    {
                        faceIntersections.Add(new Tuple<int, int>(face1.Index, face2.Index));
                    }
                }
            }

            // Find all paths between intersecting triangles
            var edgepaths = new List<IEnumerable<DualEdge>>();
            foreach (var intersection in faceIntersections)
            {
                edgepaths.Add(spanningTree.Path(intersection.Item1, intersection.Item2));
            }

            // Count the number of times each edge occurs
            var allEdgesInPaths = edgepaths
                .SelectMany(p => p)
                .Select(p => p.Index)
                .Distinct()
                .Select(p => new
                {
                    EdgeId = p,
                    NumPaths = edgepaths.Where(q => q.Any(r => r.Index == p)).Count(),
                })
                .ToArray();

            // set of new cut edges
            var cuts = new List<int>();
            //set of already covered paths
            var C = new List<IEnumerable<DualEdge>>();
            while (C.Count != edgepaths.Count)
            {
                // Determine the edge with minimum average cost at which it covers new elements.
                var cutWeights = new double[allEdgesInPaths.Length];
                for (int i = 0; i < allEdgesInPaths.Length; i++)
                {
                    // Count how many of the paths where the 
                    // edge occurs have already been cut
                    var numInC = C
                        .Where(p => p
                            .Select(q => q.Index)
                            .Contains(allEdgesInPaths[i].EdgeId))
                        .Count();

                    // Determine the weight
                    cutWeights[i] = ((allEdgesInPaths[i].NumPaths - numInC) > 0) ?
                        1.0 / (allEdgesInPaths[i].NumPaths - numInC) :
                        double.MaxValue;
                }

                // Find the edge with the lowest weight
                var minimalIndex = cutWeights
                    .Select((p, i) => new { p, i })
                    .OrderBy(p => p.p)
                    .First().i; // np.argmin(cutWeights)
                cuts.Add(allEdgesInPaths[minimalIndex].EdgeId);
                // Find all paths where the edge occurs and add them to C.
                foreach (IEnumerable<DualEdge> path in edgepaths)
                {
                    if (path
                        .Select(p => p.Index)
                        .Contains(allEdgesInPaths[minimalIndex].EdgeId)
                        && !C.Contains(path))
                    {
                        C.Add(path);
                    }
                }
            }

            // Make the cuts in the spanning tree
            IEnumerable<SpanningTree> connectedComponents = spanningTree.MakeCuts(cuts);


            BothTri[][] splitFaces = connectedComponents
                .Select(p => p.Faces
                    .Select(q =>
                    {
                        if (q is SterileTri3 st)
                            return new BothTri(faces.Single(r => r.Index == -q.Index), st.TabId, st.Common);
                        else
                            return faces.Single(r => r.Index == q.Index);
                    })
                    .ToArray())
                .Select(p => p)
                .ToArray();

            return splitFaces;
        }

        private static IEnumerable<BothTri> Flatten(
            Mesh mesh,
            SpanningTree spanTree,
            Tri3 node)
        {
            return FlattenWorker(
                mesh,
                spanTree,
                node,
                null,
                new HashSet<int>(),
                0);
        }

        private static IEnumerable<BothTri> FlattenWorker(
            Mesh mesh,
            SpanningTree spanTree,
            Tri3 curr,
            BothTri prev,
            HashSet<int> visitedNodes,
            int depth)
        {
            //            Console.WriteLine("".PadLeft(depth, '.') + curr.Index);
            if (visitedNodes.Contains(curr.Index)) return new BothTri[0];
            visitedNodes.Add(curr.Index);

            var node = curr;
            Tri3 nodePrev = null;

            if (prev != null)
            {
                // Need to treat the common edge special.
                var ab = curr.CommonEdge(prev.Space);
                var abi = ab.Select(p => p.Index).ToArray();
                var c = curr.Vertices.Where(p => !abi.Contains(p.Index)).Single();
                var origOrder = curr.Vertices.Select(p => p.Index).ToArray();
                var newOrder = new int[] { ab[0].Index, ab[1].Index, c.Index };
                var curr2 = new Tri3(ab[0], ab[1], c, curr.Index);
                curr = new Tri3(ab[0], ab[1], c, curr.Index);
                node = prev.SpaceToPaper.Apply(curr);

                var c2 = prev.Space.Vertices.Where(p => !abi.Contains(p.Index)).Single();
                nodePrev = new Tri3(ab[0], ab[1], c2);
                nodePrev = prev.SpaceToPaper.Apply(nodePrev);
            }

            var tri1 = node.Translate(node.A.Neg());
            var BYtoX = tri1.B.GetXYRot();
            var tri2 = tri1.Apply(BYtoX);

            var BZtoX = tri2.B.GetXZRot();
            var tri3 = tri2.Apply(BZtoX);

            var CZtoY = tri3.C.GetYZRot();
            var tri4 = tri3.Apply(CZtoY);

            nodePrev = nodePrev?.Translate(node.A.Neg()).Apply(BYtoX).Apply(BZtoX);
            // Need to check if rotation put tri4 over prev.Paper. This is easy because the common axis is along x, and z=0. 
            if (prev != null)
            {
                // Need to determine which 
                if (tri4.C.Y > 0 && nodePrev.C.Y > 0)
                {
                    // overlap. Just need to do another rotation around xhat.
                    CZtoY = Mat3.RotateYZ(Math.PI).Mult(CZtoY);
                }
            }

            Mat3 spaceToPaper = CZtoY.Mult(BZtoX.Mult(BYtoX));
            if (prev != null)
            {
                // Need to restore angle of the common edge.
                spaceToPaper = BYtoX.Inv().Mult(BZtoX.Inv().Mult(spaceToPaper));
            }

            Transform t;
            if (prev == null)
            {
                t = Transform.ContructRT(node.A.Neg(), spaceToPaper);
            }
            else
            {
                t = Transform.ContructTRT(node.A.Neg(), spaceToPaper);
            }

            if (prev != null)
            {
                // Apply atop previous transform
                t = t.Compose(prev.SpaceToPaper);
            }

            var test1 = t.Apply(curr);
            if (Math.Abs(test1.C.Z) > 1e-8) throw new InvalidOperationException();
            var test4 = curr;


            BothTri bt = new BothTri(curr, t);

            var test0 = bt.Paper;
            var test2 = bt.SpaceToPaper.Apply(curr);

            var test3 = bt.PaperToSpace(bt.Paper);
            var test5 = bt.Space;

            if (test0.Dist(test1) > 1e-5) throw new InvalidOperationException();
            if (test0.Dist(test2) > 1e-5) throw new InvalidOperationException();
            if (test3.Dist(test4) > 1e-5) throw new InvalidOperationException();
            if (test3.Dist(test5) > 1e-5) throw new InvalidOperationException();

            var ret = new List<BothTri>() { bt };
            foreach (Tri3 child in spanTree.GetConnectedNodes(curr))
            {
                var cbt = FlattenWorker(mesh, spanTree, child, bt, visitedNodes, depth + 1);
                ret.AddRange(cbt);
            }

            return ret;
        }

        private static MyColor GetColor(double vx, double vy)
        {
            // Use those views to determine the color
            var cr = (byte)(127 * (Math.Pow(Math.Sin(vx * 100), 2) + Math.Pow(Math.Cos(vy * 100), 2)));
            var cg = (byte)(127 * (Math.Pow(Math.Sin(vx * 30), 2) + Math.Pow(Math.Cos(vy * 20), 2)));
            var cb = (byte)(127 * (Math.Pow(Math.Sin(vx * 20), 2) + Math.Pow(Math.Cos(vy * 30), 2)));
            var c = new MyColor(cr, cg, cb);
            return c;
        }
    }

    public class Transform
    {
        private static readonly Transform Identity = new Transform(new Vec3(0, 0, 0), Mat3.Identity);
        private readonly Vec3 tran;
        private readonly Mat3 rot;

        private Transform(Vec3 tran, Mat3 rot)
        {
            this.tran = tran;
            this.rot = rot;
        }

        public static Transform ContructTRT(Vec3 translate, Mat3 rotate)
        {
            // The transform is 
            // TI_a R T_a v =
            // R v + (R a) - a =
            // T_t r v
            return new Transform(rotate.Mult(translate).Subtract(translate), rotate);
        }

        public static Transform ContructRT(Vec3 translate, Mat3 rotate)
        {
            // The transform is 
            // R T_a v =
            // R v + (R a) =
            // T_t r v
            return new Transform(rotate.Mult(translate), rotate);
        }

        internal Transform Compose(Transform spaceToPaper)
        {
            // T_t1 r_1 T_t0 r_0 v =
            // r_1 r_0 v + r_1 t0 + t1 =
            // t_01 = r_1 t0 + t1
            // r_01 = r_1 r_0
            return new Transform(
                rot.Mult(spaceToPaper.tran).Add(tran),
                rot.Mult(spaceToPaper.rot));
        }

        internal Tri3 Apply(Tri3 t)
        {
            if (t == null) return t;
            return t
                .Apply(rot)
                .Translate(tran);
        }

        internal Vec3 Apply(Vec3 t)
        {
            if (t == null) return t;
            return rot.Mult(t).Add(tran);
        }

        internal Transform Inv()
        {
            // (T_t R)^-1 v 
            // R^-1 T_(-t) = 
            // R^-1 (v - t)
            // R^-1 v + R^-1 (-t)
            // T_(R_^1 -t) R^-1 v

            var tmp2 = new Transform(tran, Mat3.Identity);
            var tmp3 = new Transform(tran.Neg(), Mat3.Identity);

            var t1 = tmp2.Compose(tmp3);
            var t2 = tmp3.Compose(tmp2);

            if (t1.Dist(Identity) > 1e-5) throw new InvalidOperationException();
            if (t2.Dist(Identity) > 1e-5) throw new InvalidOperationException();

            var ret = new Transform(rot.Inv().Mult(tran.Neg()), rot.Inv());

            t1 = ret.Compose(this);
            t2 = Compose(ret);

            if (t1.Dist(Identity) > 1e-5) throw new InvalidOperationException();
            if (t2.Dist(Identity) > 1e-5) throw new InvalidOperationException();

            return ret;
        }

        public double Dist(Transform t)
        {
            return tran.Subtract(t.tran).LenSq + rot.Dist(t.rot);
        }
    }

    public class Edge
    {
        internal double Length { get { return Math.Sqrt(A.Subtract(B).LenSq); } }

        public readonly Vec3 A;
        public readonly Vec3 B;
        public int Index;

        public Edge(Vec3 a, Vec3 b)
        {
            A = a;
            B = b;
        }

        public IEnumerable<int> VertexIndices
        {
            get
            {
                return (A.Index < B.Index) ? new[] { A.Index, B.Index } : new[] { B.Index, A.Index };
            }
        }
    }

    public class Mesh
    {
        private List<Tri3> faces = new List<Tri3>();
        private List<Vec3> vertices = new List<Vec3>();
        private List<Edge> edges = new List<Edge>();

        internal IEnumerable<Tri3> Faces { get { return faces; } }

        internal Tri3 Add(Tri3 t)
        {
            return AddFace(t.A, t.B, t.C);
        }

        internal Tri3 AddFace(Vec3 a, Vec3 b, Vec3 c)
        {
            a = AddVertex(a);
            b = AddVertex(b);
            c = AddVertex(c);

            AddEdge(a, b);
            AddEdge(b, c);
            AddEdge(c, a);

            var t = new Tri3(a, b, c);
            var tIndices = t.Vertices.Select(q => q.Index).OrderBy(q => q).ToArray();
            var match = faces.FirstOrDefault(p =>
            {
                var dist = p.Vertices.Select(q => q.Index).OrderBy(q => q)
                    .Zip(tIndices)
                    .Select(q => (q.First - q.Second) * (q.First - q.Second))
                    .Sum();
                return dist == 0;
            });

            if (match != null) return match;

            var t2 = new Tri3(a, b, c, faces.Count());
            faces.Add(t2);
            return t2;
        }

        private Edge AddEdge(Vec3 a, Vec3 b)
        {
            var e = new Edge(a, b);
            var tIndices = e.VertexIndices;
            var match = edges.FirstOrDefault(p =>
            {
                var dist = p.VertexIndices
                    .Zip(tIndices)
                    .Select(q => (q.First - q.Second) * (q.First - q.Second))
                    .Sum();
                return dist == 0;
            });

            if (match != null) return match;

            edges.Add(e);
            e.Index = edges.IndexOf(e);
            return e;
        }

        private Vec3 AddVertex(Vec3 point)
        {
            var match = vertices.FirstOrDefault(p => p.Subtract(point).LenSq < 1.0e-10);
            if (match != null) return match;

            vertices.Add(point);
            point.Index = vertices.IndexOf(point);
            return point;
        }

        internal Tuple<Tri3, Tri3> FacesOnEdge(Edge startingNode)
        {
            var indices = new[] { startingNode.A.Index, startingNode.B.Index };
            var foe = faces
                .Where(p =>
                {
                    var ins = p.Vertices.Select(q => q.Index);
                    return ins.Contains(indices[0]) && ins.Contains(indices[1]);
                })
                .ToArray();
            if (foe.Length == 0) throw new InvalidOperationException();
            if (foe.Length == 1) return new Tuple<Tri3, Tri3>(foe[0], null);
            if (foe.Length == 2) return new Tuple<Tri3, Tri3>(foe[0], foe[1]);
            throw new InvalidOperationException();
        }

        public SpanningTree GetSpanningTree()
        {
            double minLength = edges.Min(p => p.Length);
            double maxLength = edges.Max(p => p.Length);

            List<DualEdge3> dedges = new List<DualEdge3>();

            // https://github.com/felixfeliz/paperfoldmodels
            foreach (var edge in edges)
            {
                var faces = FacesOnEdge(edge);
                var edgeweight = 1.0 - (edge.Length - minLength) / (maxLength - minLength);
                if (faces.Item2 != null)
                {
                    dedges.Add(new DualEdge3()
                    {
                        Vertex1 = faces.Item1,
                        Vertex2 = faces.Item2,
                        Weight = edgeweight,
                    });
                }
            }

            // https://en.wikipedia.org/wiki/Kruskal%27s_algorithm
            var A = new List<DualEdge>();

            var vSets = dedges.Select(p => p.Vertex1.Index)
                .Union(dedges.Select(p => p.Vertex2.Index))
                .Distinct()
                .Select(p => new[] { p })
                .ToList();

            var sortedArray = dedges.OrderBy(p => p.Weight);
            foreach (var dedge in sortedArray)
            {
                var s1 = vSets.First(p => p.Contains(dedge.Vertex1.Index));
                var s2 = vSets.First(p => p.Contains(dedge.Vertex2.Index));
                if (s1 != s2)
                {
                    A.Add(new DualEdge(dedge.Vertex1, dedge.Vertex2));
                    vSets.Remove(s1);
                    vSets.Remove(s2);
                    vSets.Add(s1.Union(s2).ToArray());
                }
            }

            return new SpanningTree(A);
        }

        private class DualEdge3
        {
            public Tri3 Vertex1;
            public Tri3 Vertex2;
            public double Weight;
        }
    }

    public class DualEdge
    {
        public readonly Tri3[] Vertices;
        public readonly int[] VertexIndices;
        internal readonly int Index;

        public DualEdge(Tri3 v1, Tri3 v2)
        {
            Vertices = new Tri3[] { v1, v2 };
            VertexIndices = new int[] { v1.Index, v2.Index };
            var x = VertexIndices.OrderBy(p => p).ToArray();
            if (x[1] > 60000) throw new ArgumentOutOfRangeException();
            Index = 60000 * x[0] + x[1];
        }
    }

    public class SpanningTree
    {
        public readonly IEnumerable<DualEdge> Edges;
        public readonly IEnumerable<Tri3> Faces;
        private readonly Dictionary<int, Tri3> faceLookup;

        public SpanningTree(IEnumerable<DualEdge> edges)
        {
            Edges = edges;
            Faces = Edges.SelectMany(p => p.Vertices)
                .Distinct()
                .ToArray();
            faceLookup = new Dictionary<int, Tri3>();
            foreach (var f in Faces)
            {
                faceLookup[f.Index] = f;
            }
            //            faceLookup = Faces.ToDictionary(p => p.Index, p => p);
        }

        public SpanningTree(Tri3 face)
        {
            Edges = new DualEdge[0];
            Faces = new Tri3[] { face };
            faceLookup = Faces.ToDictionary(p => p.Index, p => p);
        }

        internal IEnumerable<Tri3> GetConnectedNodes(Tri3 node)
        {
            return Edges
                .Where(p => p.VertexIndices.Contains(node.Index))
                .Select(p => p.Vertices.Single(q => q.Index != node.Index))
                .ToArray();
        }

        internal IEnumerable<SpanningTree> MakeCuts(List<int> cutEdgeIds)
        {
            var ret = new List<SpanningTree>();
            HashSet<int> unvisited = new HashSet<int>(Faces.Select(p => p.Index));

            // Remove cut edges
            var newEdges = Edges.Where(p => !cutEdgeIds.Contains(p.Index)).ToList();

            while (unvisited.Count > 0)
            {
                var cur = Faces.Single(p => p.Index == unvisited.First());
                var tree = GetTreeFromFace(newEdges, unvisited, cur);
                if (tree.Count() == 0)
                {
                    ret.Add(new SpanningTree(cur));
                }
                else
                {
                    ret.Add(new SpanningTree(tree));
                }
            }

            int tabId = 0;
            var removedEdges = Edges.Where(p => cutEdgeIds.Contains(p.Index)).ToList();
            foreach (var removedEdge in removedEdges)
            {
                NewMethod(ret, tabId, removedEdge.Vertices[0], removedEdge.Vertices[1]);
                NewMethod(ret, tabId, removedEdge.Vertices[1], removedEdge.Vertices[0]);
                tabId++;
            }
            return ret;
        }

        private void NewMethod(List<SpanningTree> ret, int tabId, Tri3 v1, Tri3 v2)
        {
            var t1 = ret.Single(p => p.Faces.Any(q => q.Index == v1.Index));

            var common = v1.Vertices.Where(p => v2.Vertices.Any(q => q.Index == p.Index)).ToArray();
            var t1p = new SpanningTree(t1.Edges.Union(new[] { new DualEdge(v1, new SterileTri3(v2, tabId, common)) }));
            ret.Remove(t1);
            ret.Add(t1p);
        }

        private static IEnumerable<DualEdge> GetTreeFromFace(
            IEnumerable<DualEdge> edges,
            HashSet<int> unvisited,
            Tri3 cur)
        {
            var ret = new List<DualEdge>();
            if (!(cur is Tri3Ext) && unvisited.Contains(cur.Index))
            {
                unvisited.Remove(cur.Index);
                var ww = edges.Where(p => p.VertexIndices.Contains(cur.Index))
                    .GroupBy(p => p.Index)
                    .Select(p => p.Count() == 1 ?
                        p.Single() :
                        p.Select(q => new { q, o = q.Vertices[0] is Tri3Ext || q.Vertices[1] is Tri3Ext ? 0 : 1 })
                            .OrderBy(q => q.o)
                            .First().q);
                //                    .Where(p => p.Count() > 1)
                //                  .Count();
                //if (ww > 1)
                //{

                //}
                foreach (var fi in ww) // edges.Where(p => p.VertexIndices.Contains(cur.Index)))
                {
                    ret.Add(fi);
                    var toFace = fi.Vertices.Single(p => p.Index != cur.Index);
                    ret.AddRange(GetTreeFromFace(edges, unvisited, toFace));
                }
            }

            return ret;
        }

        internal IEnumerable<DualEdge> Path(int sourceIndex, int destIndex)
        {
            // Easy to construct since this is a tree
            var source = faceLookup[sourceIndex];
            var path = PathWorker(source, destIndex, new HashSet<int>()).ToArray();
            var edgePath = new List<DualEdge>();
            for (int i = 0; i < path.Length - 1; i++)
            {
                edgePath.Add(new DualEdge(path[i], path[i + 1]));
            }

            return edgePath;
        }

        private IEnumerable<Tri3> PathWorker(Tri3 cur, int destIndex, HashSet<int> visited)
        {
            if (!visited.Contains(cur.Index))
            {
                visited.Add(cur.Index);
                var ret = new Tri3[] { cur };
                if (cur.Index == destIndex) return ret;

                foreach (var fi in GetConnectedNodes(cur))
                {
                    var p = PathWorker(fi, destIndex, visited);
                    if (p != null)
                    {
                        return ret.Union(p);
                    }
                }
            }

            return null;
        }
    }

    public class SterileTri3 : Tri3
    {
        public readonly int TabId;
        public readonly Vec3[] Common;

        public SterileTri3(Tri3 source, int tabId, Vec3[] common) : base(source.A, source.B, source.C, -source.Index)
        {
            TabId = tabId;
            Common = common;
        }
    }


    public class BothTri
    {
        public readonly Tri3 Space;
        public readonly Tri2 Paper;
        public readonly Transform SpaceToPaper;
        private readonly Transform paperToSpace;
        public readonly int Index;
        public readonly int? TabId;
        public readonly Vec3[] CommonSpace;
        public readonly Vec3[] CommonPaper;

        public BothTri(Tri3 space, Transform spaceToPaper)
        {
            Space = space;
            Index = space.Index;
            Paper = spaceToPaper.Apply(space).AsTri2();
            SpaceToPaper = spaceToPaper;
            paperToSpace = spaceToPaper.Inv();
        }

        public BothTri(BothTri bothTri, int tabId) : this(bothTri.Space, bothTri.SpaceToPaper)
        {
            TabId = tabId;
        }

        public BothTri(BothTri bothTri, int tabId, Vec3[] common) : this(bothTri, tabId)
        {
            CommonSpace = common;
            CommonPaper = common.Select(p => SpaceToPaper.Apply(p)).ToArray();
        }

        public Tri3 PaperToSpace(Tri2 t)
        {
            return new Tri3(
                PaperToSpace(t.A.X, t.A.Y),
                PaperToSpace(t.B.X, t.B.Y),
                PaperToSpace(t.C.X, t.C.Y));
        }

        public Vec3 PaperToSpace(double x, double y)
        {
            var v = new Vec3(x, y, 0.0);
            return paperToSpace.Apply(v);
        }

        public class BoundingBox
        {
            public readonly double MinX;
            public readonly double MaxX;
            public readonly double MinY;
            public readonly double MaxY;
            public BoundingBox(Tri2 t)
            {
                MinX = t.Vertices.Min(p => p.X);
                MaxX = t.Vertices.Max(p => p.X);
                MinY = t.Vertices.Min(p => p.Y);
                MaxY = t.Vertices.Max(p => p.Y);
            }
        }

        internal BoundingBox GetBoundingBox()
        {
            return new BoundingBox(Paper);
        }
    }

    public class Tri2
    {
        public readonly Vec2 A;
        public readonly Vec2 B;
        public readonly Vec2 C;
        private readonly Vec2 v0;
        private readonly Vec2 v1;
        private readonly double d;
        private readonly double absd;
        public Tri2(Vec2 a, Vec2 b, Vec2 c)
        {
            A = a;
            B = b;
            C = c;

            v0 = C.Subtract(A);
            v1 = B.Subtract(A);
            d = v1.Cross(v0);
            absd = d >= 0 ? d : -d;
        }

        public IEnumerable<Vec2> Vertices { get { return new[] { A, B, C }; } }

        public override string ToString()
        {
            return "A=" + A.ToString() + ", B=" + B.ToString() + ", C=" + C.ToString();
        }

        // https://stackoverflow.com/questions/1585459/whats-the-most-efficient-way-to-detect-triangle-triangle-intersections
        public bool PointInTriangle(Vec2 p, double epsilon = 0.0)
        {
            // Cross product
            double v2X = p.X - A.X;
            double v2Y = p.Y - A.Y;
            var u = +v2X * v0.Y - v2Y * v0.X;
            var v = -v2X * v1.Y + v2Y * v1.X;

            if (d < 0)
            {
                u = -u;
                v = -v;
            }

            return
                u >= epsilon &&
                v >= epsilon &&
                (u + v) <= (absd - epsilon);
        }

        public bool PointInTriangle(double x, double y, double eplison = 0.0)
        {
            return PointInTriangle(new Vec2(x, y), eplison);
        }

        // Check if two lines intersect
        private static bool LineIntersection(Vec2 v1, Vec2 v2, Vec2 v3, Vec2 v4, double epsilon)
        {
            var d = (v4.Y - v3.Y) * (v2.X - v1.X) - (v4.X - v3.X) * (v2.Y - v1.Y);
            var u = (v4.X - v3.X) * (v1.Y - v3.Y) - (v4.Y - v3.Y) * (v1.X - v3.X);
            var v = (v2.X - v1.X) * (v1.Y - v3.Y) - (v2.Y - v1.Y) * (v1.X - v3.X);
            if (d < 0)
            {
                u = -u;
                v = -v;
                d = -d;
            }
            return
                epsilon <= u && u <= (d - epsilon) &&
                epsilon <= v && v <= (d - epsilon);
        }

        // Check if two triangles intersect
        public bool TriangleIntersection(Tri2 t2, double epsilon)
        {
            if (LineIntersection(A, B, t2.A, t2.B, epsilon)) return true;
            if (LineIntersection(A, B, t2.A, t2.C, epsilon)) return true;
            if (LineIntersection(A, B, t2.B, t2.C, epsilon)) return true;
            if (LineIntersection(A, C, t2.A, t2.B, epsilon)) return true;
            if (LineIntersection(A, C, t2.A, t2.C, epsilon)) return true;
            if (LineIntersection(A, C, t2.B, t2.C, epsilon)) return true;
            if (LineIntersection(B, C, t2.A, t2.B, epsilon)) return true;
            if (LineIntersection(B, C, t2.A, t2.C, epsilon)) return true;
            if (LineIntersection(B, C, t2.B, t2.C, epsilon)) return true;
            if (PointInTriangle(t2.A, epsilon) &&
                PointInTriangle(t2.B, epsilon) &&
                PointInTriangle(t2.C, epsilon)) return true;
            if (t2.PointInTriangle(A, epsilon) &&
                t2.PointInTriangle(B, epsilon) &&
                t2.PointInTriangle(C, epsilon)) return true;
            return false;
        }

        internal Tri3 AsTri3()
        {
            return new Tri3(
                A.AsVec3(),
                B.AsVec3(),
                C.AsVec3());
        }

        internal double Dist(Tri3 t)
        {
            return AsTri3().Dist(t);
        }

        internal double DistToClosestEdge(double sx, double sy)
        {
            Vec3 p = new Vec3(sx, sy, 0);

            var dists = new[]
            {
                p.DistanceFromPointToSegment(A.AsVec3(), B.AsVec3()),
                p.DistanceFromPointToSegment(B.AsVec3(), C.AsVec3()),
                p.DistanceFromPointToSegment(C.AsVec3(), A.AsVec3()),
            };

            return dists.Min();
        }
    }

    public class Tri3Ext : Tri3
    {
        public readonly int TabId;
        public Tri3Ext(Tri3 t, int tabId) : base(t.A, t.B, t.C, t.Index)
        {
            TabId = tabId;
        }
    }

    public class Tri3
    {
        public readonly Vec3 A;
        public readonly Vec3 B;
        public readonly Vec3 C;
        public readonly int Index;

        public IEnumerable<Vec3> Vertices { get { return new[] { A, B, C }; } }

        public Tri3(Vec3 a, Vec3 b, Vec3 c)
        {
            A = a;
            B = b;
            C = c;
        }

        public Tri3(Vec3 a, Vec3 b, Vec3 c, int index) : this(a, b, c)
        {
            Index = index;
        }

        public Tri3(List<Vec3> points)
        {
            if (points.Count != 3) throw new ArgumentOutOfRangeException("points");
            A = points[0];
            B = points[1];
            C = points[2];
        }

        public Tri3(Tri3 t, int index)
        {
            A = t.A;
            B = t.B;
            C = t.C;
            Index = index;
        }

        public Tri3 Translate(Vec3 offset)
        {
            return new Tri3(
                A.Add(offset),
                B.Add(offset),
                C.Add(offset));
        }

        public Tri3 Apply(Mat3 m)
        {
            return new Tri3(
                m.Mult(A),
                m.Mult(B),
                m.Mult(C));
        }

        public override string ToString()
        {
            return "A=" + A.ToString() + ", B=" + B.ToString() + ", C=" + C.ToString();
        }

        internal Tri2 AsTri2()
        {
            return new Tri2(
                A.AsVec2(),
                B.AsVec2(),
                C.AsVec2());
        }

        internal Vec3[] CommonEdge(Tri3 prev)
        {
            var vts1 = this.Vertices.Select(p => p.Index).ToArray();
            var vts2 = prev.Vertices.Select(p => p.Index).ToArray();
            var match = vts1.Where(p => vts2.Contains(p)).OrderBy(p => p);
            if (match.Count() != 2) throw new InvalidOperationException();
            return this.Vertices.Where(p => match.Contains(p.Index)).ToArray();
        }

        internal double Dist(Tri3 t)
        {
            var vert3 = Vertices.ToArray();
            var vertexCombos = new List<Vec3[]>()
            {
                new Vec3[] { vert3[0], vert3[1], vert3[2] },
                new Vec3[] { vert3[0], vert3[2], vert3[1] },
                new Vec3[] { vert3[1], vert3[0], vert3[2] },
                new Vec3[] { vert3[1], vert3[2], vert3[1] },
                new Vec3[] { vert3[2], vert3[0], vert3[1] },
                new Vec3[] { vert3[2], vert3[1], vert3[0] },
            };

            var tVert3 = t.Vertices.ToArray();
            var minDelta = vertexCombos
                .Min(p =>
                    p[0].Subtract(tVert3[0]).LenSq +
                    p[1].Subtract(tVert3[1]).LenSq +
                    p[2].Subtract(tVert3[2]).LenSq);

            return minDelta;
        }
    }

    public class Vec2
    {
        public readonly double X;
        public readonly double Y;

        public Vec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return "(" +
                X.ToString("0.00") + "," +
                Y.ToString("0.00") + ")";
        }

        internal Vec3 AsVec3()
        {
            return new Vec3(X, Y, 0.0);
        }

        public Vec2 Subtract(Vec2 v)
        {
            return new Vec2(X - v.X, Y - v.Y);
        }

        public double Cross(Vec2 v)
        {
            return X * v.Y - Y * v.X;
        }
    }

    public class Mat3
    {
        public static readonly Mat3 Identity =
            new Mat3(
                1, 0, 0,
                0, 1, 0,
                0, 0, 1);

        internal readonly double M00; internal readonly double M01; internal readonly double M02;
        internal readonly double M10; internal readonly double M11; internal readonly double M12;
        internal readonly double M20; internal readonly double M21; internal readonly double M22;

        public Mat3(
            double m00, double m01, double m02,
            double m10, double m11, double m12,
            double m20, double m21, double m22)
        {
            M00 = m00; M01 = m01; M02 = m02;
            M10 = m10; M11 = m11; M12 = m12;
            M20 = m20; M21 = m21; M22 = m22;
        }

        //public static Mat3 RotateXY(double theta)
        //{
        //    return new Mat3(
        //        +Math.Cos(theta), +Math.Sin(theta), 0,
        //        -Math.Sin(theta), +Math.Cos(theta), 0,
        //        0, 0, 1);
        //}
        public static Mat3 RotateYZ(double theta)
        {
            return new Mat3(
                1, 0, 0,
                0, +Math.Cos(theta), +Math.Sin(theta),
                0, -Math.Sin(theta), Math.Cos(theta));
        }
        //public static Mat3 RotateXZ(double theta)
        //{
        //    return new Mat3(
        //        +Math.Cos(theta), 0, +Math.Sin(theta),
        //        0, 1, 0,
        //        -Math.Sin(theta), 0, +Math.Cos(theta));
        //}

        public Vec3 Mult(Vec3 a)
        {
            return new Vec3(
                M00 * a.X + M01 * a.Y + M02 * a.Z,
                M10 * a.X + M11 * a.Y + M12 * a.Z,
                M20 * a.X + M21 * a.Y + M22 * a.Z);
        }

        public Mat3 Mult(Mat3 a)
        {
            return new Mat3(
                M00 * a.M00 + M01 * a.M10 + M02 * a.M20, M00 * a.M01 + M01 * a.M11 + M02 * a.M21, M00 * a.M02 + M01 * a.M12 + M02 * a.M22,
                M10 * a.M00 + M11 * a.M10 + M12 * a.M20, M10 * a.M01 + M11 * a.M11 + M12 * a.M21, M10 * a.M02 + M11 * a.M12 + M12 * a.M22,
                M20 * a.M00 + M21 * a.M10 + M22 * a.M20, M20 * a.M01 + M21 * a.M11 + M22 * a.M21, M20 * a.M02 + M21 * a.M12 + M22 * a.M22);
        }

        public Mat3 Inv()
        {
            return new Mat3(
                M00, M10, M20,
                M01, M11, M21,
                M02, M12, M22);
        }

        public override string ToString()
        {
            return "(" +
                "(" + M00.ToString("0.00") + "," + M01.ToString("0.00") + "," + M02.ToString("0.00") + ")," +
                "(" + M10.ToString("0.00") + "," + M11.ToString("0.00") + "," + M12.ToString("0.00") + ")," +
                "(" + M20.ToString("0.00") + "," + M21.ToString("0.00") + "," + M22.ToString("0.00") + "))";
        }

        public double Dist(Mat3 m)
        {
            return
                Math.Abs(M00 - m.M00) + Math.Abs(M01 - m.M01) + Math.Abs(M02 - m.M02) +
                Math.Abs(M10 - m.M10) + Math.Abs(M11 - m.M11) + Math.Abs(M12 - m.M12) +
                Math.Abs(M20 - m.M20) + Math.Abs(M21 - m.M21) + Math.Abs(M22 - m.M22);
        }
    }


    public class Vec3
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;
        public int Index;

        public double LenSq { get { return X * X + Y * Y + Z * Z; } }

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vec3 Neg()
        {
            return new Vec3(-X, -Y, -Z);
        }
        public override string ToString()
        {
            return Index + " (" +
                X.ToString("0.00") + "," +
                Y.ToString("0.00") + "," +
                Z.ToString("0.00") + ")";
        }

        public Vec3 Add(Vec3 offset)
        {
            return new Vec3(
                X + offset.X,
                Y + offset.Y,
                Z + offset.Z);
        }

        public Vec3 Subtract(Vec3 offset)
        {
            return new Vec3(
                X - offset.X,
                Y - offset.Y,
                Z - offset.Z);
        }

        public Mat3 GetXYRot()
        {
            // theta = Math.Atan2(Y, X);
            // X/r == Math.Cos(theta)
            var r = Math.Sqrt(X * X + Y * Y);
            return new Mat3(
                +X / r, +Y / r, 0,
                -Y / r, +X / r, 0,
                0, 0, 1);
        }

        public Mat3 GetYZRot()
        {
            var r = Math.Sqrt(Y * Y + Z * Z);
            return new Mat3(
                1, 0, 0,
                0, +Y / r, +Z / r,
                0, -Z / r, +Y / r);
        }

        public Mat3 GetXZRot()
        {
            var r = Math.Sqrt(X * X + Z * Z);
            return new Mat3(
                +X / r, 0, +Z / r,
                0, 1, 0,
                -Z / r, 0, +X / r);
        }

        internal Vec2 AsVec2()
        {
            if (Math.Abs(Z) > 0.00000001)
            {
                throw new InvalidOperationException("Z must be near 0 to be Vec2");
            }

            return new Vec2(X, Y);
        }

        public double Dot(Vec3 viewUp)
        {
            return X * viewUp.X + Y * viewUp.Y + Z * viewUp.Z;
        }

        internal Vec3 Mult(double v)
        {
            return new Vec3(X * v, Y * v, Z * v);
        }

        internal Vec3 Unit()
        {
            var len = Math.Sqrt(X * X + Y * Y + Z * Z);
            if (len == 0) return this;
            return new Vec3(X / len, Y / len, Z / len);
        }

        // https://stackoverflow.com/questions/849211/shortest-distance-between-a-point-and-a-line-segment
        public double DistanceFromPointToSegment(Vec3 s1, Vec3 s2)
        {
            // Return minimum distance between line segment vw and this point
            double l2 = s1.Subtract(s2).LenSq;
            if (l2 == 0.0) return Math.Sqrt(Subtract(s1).LenSq);

            // Consider the line extending the segment, parameterized as v + t (w - v).
            // We find projection of point p onto the line. 
            // It falls where t = [(p-v) . (w-v)] / |w-v|^2
            // We clamp t from [0,1] to handle points outside the segment vw.
            double t = Math.Max(0, Math.Min(1, Subtract(s1).Dot(s2.Subtract(s1)) / l2));
            Vec3 projection = s1.Add(s2.Subtract(s1).Mult(t));  // Projection falls on the segment
            return Math.Sqrt(Subtract(projection).LenSq);
        }

        public double DistanceFromPointToSegment(Vec3[] ss)
        {
            if (ss == null || ss.Length != 2) throw new ArgumentOutOfRangeException();

            return DistanceFromPointToSegment(ss[0], ss[1]);
        }
    }
}
