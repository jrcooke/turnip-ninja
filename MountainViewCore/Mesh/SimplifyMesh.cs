// Copyright (c) 2018-2019 Jason Cooke
// Source is at
// https://github.com/jrcooke/turnip-ninja
// Built upon the work of
// https://github.com/Whinarn/MeshDecimator
// and
// https://github.com/sp4cerat/Fast-Quadric-Mesh-Simplification
// Which appers to be based on
// M. Garland and P. S. Heckbert. Surface Simplification using Quadric Error Metrics. Conference Proceedings of SIGGRAPH 1997, pp. 209–216.
// https://www.ri.cmu.edu/pub_files/pub2/garland_michael_1997_1/garland_michael_1997_1.pdf


/*
MIT License

Copyright(c) 2017-2018 Mattias Edlund

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
https://github.com/Whinarn/MeshDecimator
 */

/////////////////////////////////////////////
//
// Mesh Simplification Tutorial
//
// (C) by Sven Forstmann in 2014
//
// License : MIT
// http://opensource.org/licenses/MIT
//
//https://github.com/sp4cerat/Fast-Quadric-Mesh-Simplification
/////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace MountainView.Mesh
{
    /// <summary>
    /// The fast quadric mesh simplification algorithm.
    /// </summary>
    public class SimplifyMesh
    {
        private const int maxIterationCount = 100;

        private readonly bool verbose;
        private ResizableArray<Triangle> triangles;
        private ResizableArray<Vertex> vertices;
        private ResizableArray<Ref> refs;
        private ResizableArray<bool> deleted0;
        private ResizableArray<bool> deleted1;
        private Vector3d[] vertexNormals; // Only available at the end

        // Pre-allocated buffers
        private readonly double[] errArr = new double[3];

        /// <summary>
        /// Initializes the algorithm with the original mesh.
        /// </summary>
        public SimplifyMesh(IEnumerable<Vector3d> verticesIn, IEnumerable<int> indices, IEnumerable<int> edgeIndices, bool verbose = false)
        {
            this.verbose = verbose;
            refs = new ResizableArray<Ref>("refs", 0, verbose);
            deleted0 = new ResizableArray<bool>("deleted0", 50, verbose);
            deleted1 = new ResizableArray<bool>("deleted1", 50, verbose);
            vertices = new ResizableArray<Vertex>("vertices", verticesIn.Count(), verbose);

            int offset = 0;
            foreach (var vertexIn in verticesIn)
            {
                vertices.Data[offset].p.X = vertexIn.X;
                vertices.Data[offset].p.Y = vertexIn.Y;
                vertices.Data[offset].p.Z = vertexIn.Z;
                offset++;
            }

            foreach (var edgeIndex in edgeIndices)
            {
                vertices.Data[edgeIndex].IsEdge = true;
            }

            int tCount = indices.Count() / 3;
            triangles = new ResizableArray<Triangle>("triangles", tCount, verbose);
            int[] tri = new int[3];
            int triOffset = 0;
            offset = 0;
            foreach (var index in indices)
            {
                tri[triOffset++] = index;
                if (triOffset == 3)
                {
                    triangles.Data[offset].v0 = tri[0];
                    triangles.Data[offset].v1 = tri[1];
                    triangles.Data[offset].v2 = tri[2];
                    offset++;
                    triOffset = 0;
                }
            }
        }

        public int[] GetIndices()
        {
            int[] indices = new int[triangles.Length * 3];
            int offset = 0;
            for (int i = 0; i < triangles.Length; i++)
            {
                var triangle = triangles.Data[i];
                indices[offset++] = triangle.v0;
                indices[offset++] = triangle.v1;
                indices[offset++] = triangle.v2;
            }

            return indices;
        }

        public Vector3d[] GetVertices()
        {
            var verticesOut = new Vector3d[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                verticesOut[i] = vertices.Data[i].p;
            }

            return verticesOut;
        }

        public int[] GetEdgeIndices()
        {
            return vertices.Data
                .Select((p, i) => new { i, p.IsEdge })
                .Where(p => p.IsEdge && p.i < vertices.Length)
                .Select(p => p.i)
                .ToArray();
        }

        public Vector3d[] GetVertexNormals()
        {
            return vertexNormals;
        }

        /// <summary>
        /// Main simplification function
        /// </summary>
        public void SimplifyMeshByCount(int targetCount, double agressiveness = 7.0)
        {
            int initialCount = triangles.Length;
            SymmetricMatrix smbuff = new SymmetricMatrix();
            Vector3d vbuff = new Vector3d();
            Vector3d vbuff2 = new Vector3d();
            int deletedTriangles = 0;

            for (int iteration = 0; iteration < maxIterationCount; iteration++)
            {
                ReportStatus(iteration, initialCount, initialCount - deletedTriangles, targetCount);
                if (initialCount - deletedTriangles <= targetCount)
                {
                    break;
                }

                // Update mesh once in a while
                if (iteration % 5 == 0)
                {
                    UpdateMesh(iteration, ref vbuff, ref vbuff2, ref smbuff);
                }

                // Clear dirty flag
                for (int i = 0; i < triangles.Length; i++)
                {
                    triangles.Data[i].dirty = false;
                }

                // All triangles with edges below the threshold will be removed
                //
                // The following numbers works well for most models.
                // If it does not, try to adjust the 3 parameters
                double threshold = 0.000000001 * Math.Pow(iteration + 3, agressiveness);

                // target number of triangles reached ? Then break
                if (verbose && (iteration % 5) == 0)
                {
                    System.Diagnostics.Debug.WriteLine("iteration {0} - triangles {1} threshold {2}", iteration, initialCount - deletedTriangles, threshold);
                }

                // Remove vertices & mark deleted triangles
                deletedTriangles += RemoveVertexPass(initialCount, targetCount, threshold, ref vbuff, ref vbuff2, ref smbuff);
            }

            CompactMesh();
        }

        /// <summary>
        /// Simplift the mesh with a specified error threshold.
        /// </summary>
        public void SimplifyMeshByThreshold(double threshold = 1.0E-3)
        {
            if (verbose)
            {
                System.Diagnostics.Debug.WriteLine(DateTime.Now + "\tStarting SimplifyMeshByThreshold");
            }

            int initialCount = triangles.Length;
            Vector3d vbuff = new Vector3d();
            Vector3d vbuff2 = new Vector3d();
            SymmetricMatrix smbuff = new SymmetricMatrix();

            for (int iteration = 0; iteration < maxIterationCount; iteration++)
            {
                // Update mesh every loop
                UpdateMesh(iteration, ref vbuff, ref vbuff2, ref smbuff);

                ReportStatus(iteration, initialCount, triangles.Length, threshold);

                // Clear dirty flag
                for (int i = 0; i < triangles.Length; i++)
                {
                    triangles.Data[i].dirty = false;
                }

                // All triangles with edges below the threshold will be removed
                // Remove vertices & mark deleted triangles
                if (RemoveVertexPass(initialCount, 0, threshold, ref vbuff, ref vbuff2, ref smbuff) <= 0)
                {
                    break;
                }
            }

            CompactMesh();
        }

        private void ReportStatus(int iteration, int initialCount, int triangleCount, double threshold)
        {
            if (verbose)
            {
                System.Diagnostics.Debug.WriteLine(DateTime.Now + "\tIteration: " + iteration + ", initialCount: " + initialCount + ", triangleCount:" + triangleCount + ", threshold:" + threshold);
            }
        }

        /// <summary>
        /// Check if a triangle flips when this edge is removed
        /// </summary>
        private bool Flipped(ref Vector3d p, int i0, int i1, ref Vertex v0, bool[] deleted)
        {
            for (int k = 0; k < v0.tcount; k++)
            {
                Ref r = refs.Data[v0.tstart + k];
                Triangle t = triangles.Data[r.tid];
                if (t.deleted)
                {
                    continue;
                }

                int s = r.tvertex;
                int id1 = t[(s + 1) % 3];
                int id2 = t[(s + 2) % 3];
                if (id1 == i1 || id2 == i1) // delete ?
                {
                    deleted[k] = true;
                    continue;
                }

                Vector3dExt.DoCrossAndDots(ref triangles.Data[r.tid].n, ref p, ref vertices.Data[id1].p, ref vertices.Data[id2].p, out double dot1, out double dot2);

                if (Math.Abs(dot1) > 0.999)
                {
                    return true;
                }

                deleted[k] = false;
                if (dot2 < 0.2)
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Update triangle connections and edge error after a edge is collapsed.
        /// </summary>
        private void UpdateTriangles(int i0, ref Vertex v, bool[] deleted, ref int deletedTriangles, ref Vector3d vbuff, ref Vector3d vbuff2, ref SymmetricMatrix smbuff)
        {
            for (int k = 0; k < v.tcount; k++)
            {
                Ref r = refs.Data[v.tstart + k];
                Triangle t = triangles.Data[r.tid];
                if (t.deleted)
                {
                    continue;
                }

                if (deleted[k])
                {
                    t.deleted = true;
                    deletedTriangles++;
                }
                else
                {
                    t[r.tvertex] = i0;
                    t.dirty = true;
                    CalcFullError(ref t, ref vbuff, ref vbuff2, ref smbuff);
                    refs.Add(r);
                }

                triangles.Data[r.tid] = t;
            }
        }

        /// <summary>
        /// Remove vertices and mark deleted triangles
        /// </summary>
        private int RemoveVertexPass(
            int startTrisCount,
            int targetTrisCount,
            double threshold,
            ref Vector3d vbuff,
            ref Vector3d vbuff2,
            ref SymmetricMatrix smbuff)
        {
            int deletedTriangles = 0;
            Vector3d p = new Vector3d();
            for (int tid = 0; tid < triangles.Length; tid++)
            {
                var t = triangles.Data[tid];
                if (t.dirty || t.deleted || t.err3 > threshold)
                {
                    continue;
                }

                if (vertices.Data[t.v0].IsEdge || vertices.Data[t.v1].IsEdge || vertices.Data[t.v2].IsEdge)
                {
                    // Don't mess with triangles that touch edges
                    continue;
                }

                t.GetErrors(errArr);
                for (int j = 0; j < 3; j++)
                {
                    if (errArr[j] > threshold)
                    {
                        continue;
                    }

                    int i0 = t[j];
                    int i1 = t[(j + 1) % 3];

                    // Compute vertex to collapse to
                    CalculateError(i0, i1, ref p, ref vbuff, ref smbuff);
                    deleted0.Resize(vertices.Data[i0].tcount);
                    deleted1.Resize(vertices.Data[i1].tcount);

                    // Don't remove if flipped
                    if (Flipped(ref p, i0, i1, ref vertices.Data[i0], deleted0.Data))
                    {
                        continue;
                    }

                    if (Flipped(ref p, i1, i0, ref vertices.Data[i1], deleted1.Data))
                    {
                        continue;
                    }

                    // Not flipped, so remove edge
                    vertices.Data[i0].p = p;
                    p = new Vector3d();
                    SymmetricMatrix.Add(ref vertices.Data[i1].q, ref vertices.Data[i0].q, ref vertices.Data[i0].q);

                    UpdateTriangles(i0, ref vertices.Data[i0], deleted0.Data, ref deletedTriangles, ref vbuff, ref vbuff2, ref smbuff);
                    UpdateTriangles(i0, ref vertices.Data[i1], deleted1.Data, ref deletedTriangles, ref vbuff, ref vbuff2, ref smbuff);

                    int tstart = refs.Length;
                    int tcount = refs.Length - tstart;
                    if (tcount <= vertices.Data[i0].tcount)
                    {
                        // Compact
                        if (tcount > 0)
                        {
                            Array.Copy(refs.Data, tstart, refs.Data, vertices.Data[i0].tstart, tcount);
                        }
                    }
                    else
                    {
                        // append
                        vertices.Data[i0].tstart = tstart;
                    }

                    vertices.Data[i0].tcount = tcount;
                    break;
                }
            }

            return deletedTriangles;
        }

        /// <summary>
        /// Compact triangles, compute edge error and build reference list.
        /// </summary>
        /// <param name="iteration">The iteration index.</param>
        private void UpdateMesh(int iteration, ref Vector3d vbuff, ref Vector3d vbuff2, ref SymmetricMatrix smbuff)
        {
            if (iteration > 0) // compact triangles
            {
                int dst = 0;
                for (int i = 0; i < triangles.Length; i++)
                {
                    if (!triangles.Data[i].deleted)
                    {
                        if (dst != i)
                        {
                            triangles.Data[dst] = triangles.Data[i];
                        }
                        dst++;
                    }
                }

                triangles.Resize(dst);
            }

            // Init Quadrics by Plane & Edge Errors
            //
            // required at the beginning ( iteration == 0 )
            // recomputing during the simplification is not required,
            // but mostly improves the result for closed meshes
            if (iteration == 0)
            {
                Vector3d p10 = new Vector3d();
                Vector3d p20 = new Vector3d();
                for (int i = 0; i < triangles.Length; i++)
                {
                    int v0 = triangles.Data[i].v0;
                    int v1 = triangles.Data[i].v1;
                    int v2 = triangles.Data[i].v2;

                    Vector3dExt.Sub(ref vertices.Data[v1].p, ref vertices.Data[v0].p, ref p10);
                    Vector3dExt.Sub(ref vertices.Data[v2].p, ref vertices.Data[v0].p, ref p20);
                    Vector3dExt.Cross(ref p10, ref p20, ref triangles.Data[i].n);
                    triangles.Data[i].n.Normalize();

                    smbuff.Repopulate(ref triangles.Data[i].n, -Vector3dExt.Dot(ref triangles.Data[i].n, ref vertices.Data[v0].p));
                    SymmetricMatrix.AddInto(ref vertices.Data[v0].q, ref smbuff);
                    SymmetricMatrix.AddInto(ref vertices.Data[v1].q, ref smbuff);
                    SymmetricMatrix.AddInto(ref vertices.Data[v2].q, ref smbuff);
                }

                for (int i = 0; i < triangles.Length; i++)
                {
                    // Calc Edge Error
                    var t = triangles.Data[i];
                    CalcFullError(ref t, ref vbuff, ref vbuff2, ref smbuff);
                    triangles.Data[i] = t;
                }
            }

            // Init Reference ID list
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices.Data[i].tstart = 0;
                vertices.Data[i].tcount = 0;
            }

            for (int i = 0; i < triangles.Length; i++)
            {
                Triangle t = triangles.Data[i];
                vertices.Data[t.v0].tcount++;
                vertices.Data[t.v1].tcount++;
                vertices.Data[t.v2].tcount++;
            }

            int tstartX = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices.Data[i].tstart = tstartX;
                if (vertices.Data[i].tcount > 0)
                {
                    tstartX += vertices.Data[i].tcount;
                    vertices.Data[i].tcount = 0;
                }
            }

            // Write References
            refs.Resize(tstartX, tstartX * 2);
            for (int i = 0; i < triangles.Length; i++)
            {
                int v0 = triangles.Data[i].v0;
                int v1 = triangles.Data[i].v1;
                int v2 = triangles.Data[i].v2;

                int start0 = vertices.Data[v0].tstart;
                int count0 = vertices.Data[v0].tcount;
                int start1 = vertices.Data[v1].tstart;
                int count1 = vertices.Data[v1].tcount;
                int start2 = vertices.Data[v2].tstart;
                int count2 = vertices.Data[v2].tcount;

                refs.Data[start0 + count0].tid = i;
                refs.Data[start0 + count0].tvertex = 0;
                refs.Data[start1 + count1].tid = i;
                refs.Data[start1 + count1].tvertex = 1;
                refs.Data[start2 + count2].tid = i;
                refs.Data[start2 + count2].tvertex = 2;

                vertices.Data[v0].tcount++;
                vertices.Data[v1].tcount++;
                vertices.Data[v2].tcount++;
            }
        }

        /// <summary>
        /// Finally compact mesh before exiting.
        /// </summary>
        private void CompactMesh()
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices.Data[i].tcount = 0;
            }

            int dst = 0;
            for (int i = 0; i < triangles.Length; i++)
            {
                var t = triangles.Data[i];
                if (!t.deleted)
                {
                    triangles.Data[dst++] = t;
                    vertices.Data[t.v0].tcount = 1;
                    vertices.Data[t.v1].tcount = 1;
                    vertices.Data[t.v2].tcount = 1;
                }
            }

            triangles.Resize(dst);

            dst = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices.Data[i];
                if (v.tcount > 0)
                {
                    v.tstart = dst;
                    vertices.Data[i] = v;
                    if (dst != i)
                    {
                        vertices.Data[dst].p = v.p;
                        vertices.Data[dst].IsEdge = v.IsEdge;
                    }

                    dst++;
                }
            }

            for (int i = 0; i < triangles.Length; i++)
            {
                var t = triangles.Data[i];
                t.v0 = vertices.Data[t.v0].tstart;
                t.v1 = vertices.Data[t.v1].tstart;
                t.v2 = vertices.Data[t.v2].tstart;
                triangles.Data[i] = t;
            }

            vertices.Resize(dst);

            vertexNormals = new Vector3d[vertices.Length];
            int[] numTrisPerVertex = new int[vertices.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                var t = triangles.Data[i];
                Vector3dExt.WeightedAverage(ref vertexNormals[t.v0], ref t.n, numTrisPerVertex[t.v0]++, 1);
                Vector3dExt.WeightedAverage(ref vertexNormals[t.v1], ref t.n, numTrisPerVertex[t.v1]++, 1);
                Vector3dExt.WeightedAverage(ref vertexNormals[t.v2], ref t.n, numTrisPerVertex[t.v2]++, 1);
            }
        }

        // Error between vertex and Quadric
        private double VertexError(ref SymmetricMatrix q, ref Vector3d v)
        {
            return
                1 * q.m0 * v.X * v.X +
                2 * q.m1 * v.X * v.Y +
                2 * q.m2 * v.X * v.Z +
                2 * q.m3 * v.X +
                1 * q.m4 * v.Y * v.Y +
                2 * q.m5 * v.Y * v.Z +
                2 * q.m6 * v.Y +
                1 * q.m7 * v.Z * v.Z +
                2 * q.m8 * v.Z +
                1 * q.m9;
        }

        private void CalcFullError(ref Triangle t, ref Vector3d vbuff, ref Vector3d vbuff2, ref SymmetricMatrix smbuff)
        {
            t.err0 = CalculateError(t.v0, t.v1, ref vbuff, ref vbuff2, ref smbuff);
            t.err1 = CalculateError(t.v1, t.v2, ref vbuff, ref vbuff2, ref smbuff);
            t.err2 = CalculateError(t.v2, t.v0, ref vbuff, ref vbuff2, ref smbuff);
            t.err3 = Math.Min(t.err0, Math.Min(t.err1, t.err2));
        }

        // Error for one edge
        private double CalculateError(int id_v1, int id_v2, ref Vector3d result, ref Vector3d vbuff, ref SymmetricMatrix smbuff)
        {
            // compute interpolated vertex
            SymmetricMatrix.Add(ref vertices.Data[id_v1].q, ref vertices.Data[id_v2].q, ref smbuff);
            double error = 0;
            double det = smbuff.Determinant1();
            if (Math.Abs(det) > 1.0e-10)
            {
                // q_delta is invertible
                result.X = -1.0 / det * smbuff.Determinant2(); // vx = A41/det(q_delta)
                result.Y = +1.0 / det * smbuff.Determinant3(); // vy = A42/det(q_delta)
                result.Z = -1.0 / det * smbuff.Determinant4(); // vz = A43/det(q_delta)
                error = VertexError(ref smbuff, ref result);
            }
            else
            {
                // det = 0 -> try to find best result
                Vector3d p1 = vertices.Data[id_v1].p;
                Vector3d p2 = vertices.Data[id_v2].p;
                Vector3dExt.Average(ref p1, ref p2, ref vbuff);
                double error1 = VertexError(ref smbuff, ref p1);
                double error2 = VertexError(ref smbuff, ref p2);
                double error3 = VertexError(ref smbuff, ref vbuff);
                error = Math.Min(error1, Math.Min(error2, error3));
                if (error1 == error)
                {
                    result = p1;
                }
                else if (error2 == error)
                {
                    result = p2;
                }
                else
                {
                    result = vbuff;
                    vbuff = new Vector3d();
                }
            }

            return error;
        }

        private class Vector3dExt
        {
            public static void Average(ref Vector3d a, ref Vector3d b, ref Vector3d result)
            {
                result.X = (a.X + b.X) * 0.5;
                result.Y = (a.Y + b.Y) * 0.5;
                result.Z = (a.Z + b.Z) * 0.5;
            }

            public static void Sub(ref Vector3d a, ref Vector3d b, ref Vector3d result)
            {
                result.X = a.X - b.X;
                result.Y = a.Y - b.Y;
                result.Z = a.Z - b.Z;
            }

            /// <summary>
            /// Dot Product of two vectors.
            /// </summary>
            public static double Dot(ref Vector3d a, ref Vector3d b)
            {
                return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            }

            /// <summary>
            /// Cross Product of two vectors.
            /// </summary>
            public static void Cross(ref Vector3d a, ref Vector3d b, ref Vector3d result)
            {
                result.X = a.Y * b.Z - a.Z * b.Y;
                result.Y = a.Z * b.X - a.X * b.Z;
                result.Z = a.X * b.Y - a.Y * b.X;
            }

            public static void DoCrossAndDots(ref Vector3d tirNorm, ref Vector3d p, ref Vector3d v1, ref Vector3d v2, out double dot1, out double dot2)
            {
                double d1X = v1.X - p.X;
                double d1Y = v1.Y - p.Y;
                double d1Z = v1.Z - p.Z;

                double d2X = v2.X - p.X;
                double d2Y = v2.Y - p.Y;
                double d2Z = v2.Z - p.Z;

                // Do the cross product
                double nX = d1Y * d2Z - d1Z * d2Y;
                double nY = d1Z * d2X - d1X * d2Z;
                double nZ = d1X * d2Y - d1Y * d2X;

                // Bulk normarize
                double norm1 = Math.Sqrt(d1X * d1X + d1Y * d1Y + d1Z * d1Z);
                double norm2 = Math.Sqrt(d2X * d2X + d2Y * d2Y + d2Z * d2Z);
                double normN = Math.Sqrt(nX * nX + nY * nY + nZ * nZ);

                dot1 = (d1X * d2X + d1Y * d2Y + d1Z * d2Z) / (norm1 * norm2);
                dot2 = (nX * tirNorm.X + nY * tirNorm.Y + nZ * tirNorm.Z) / (normN);
            }

            internal static void WeightedAverage(ref Vector3d a, ref Vector3d b, int aWeight, int bWeight)
            {
                a.X = (a.X * aWeight + b.X * bWeight) / (aWeight + bWeight);
                a.Y = (a.Y * aWeight + b.Y * bWeight) / (aWeight + bWeight);
                a.Z = (a.Z * aWeight + b.Z * bWeight) / (aWeight + bWeight);
            }
        }

        /// <summary>
        /// A resizable array.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        internal class ResizableArray<T>
        {
            private readonly string name;
            public T[] Data;
            private readonly bool verbose;

            /// <summary>
            /// Gets the length of this array.
            /// </summary>
            public int Length;

            /// <summary>
            /// Creates a new resizable array.
            /// </summary>
            /// <param name="length">The initial array length.</param>
            public ResizableArray(string name, int length, bool verbose = false)
            {
                this.name = name;
                Data = new T[length];
                Length = length;
                this.verbose = verbose;
            }

            /// <summary>
            /// Resizes this array.
            /// </summary>
            /// <param name="length">The new length.</param>
            public void Resize(int length, int dataLength = -1)
            {
                if (dataLength == -1)
                {
                    dataLength = length;
                }

                if (dataLength > Data.Length || dataLength < (Data.Length - 1000))
                {
                    // Don't worry about downsizing unless it is a big change
                    Array.Resize(ref Data, dataLength);
                }

                Length = length;
            }

            /// <summary>
            /// Adds a new item to the end of this array.
            /// </summary>
            /// <param name="item">The new item.</param>
            public void Add(T item)
            {
                if (Length >= Data.Length)
                {
                    int length = (int)((Length + 1) * 2);
                    if (verbose)
                    {
                        System.Diagnostics.Debug.WriteLine(DateTime.Now + "\tImplicitly resizing array '" + name + "' from " + Data.Length + " to " + length);
                    }

                    Array.Resize(ref Data, length);
                }

                Data[Length++] = item;
            }
        }

        /// <summary>
        /// A symmetric matrix.
        /// </summary>
        private struct SymmetricMatrix
        {
            public double m0;
            public double m1;
            public double m2;
            public double m3;
            public double m4;
            public double m5;
            public double m6;
            public double m7;
            public double m8;
            public double m9;

            /// <summary>
            /// Creates a symmetric matrix from a plane.
            /// </summary>
            /// <param name="a">The plane x,y,z-components.</param>
            /// <param name="d">The plane w-component</param>
            public void Repopulate(ref Vector3d a, double d)
            {
                m0 = a.X * a.X; m1 = a.X * a.Y; m2 = a.X * a.Z; m3 = a.X * d;
                m4 = a.Y * a.Y; m5 = a.Y * a.Z; m6 = a.Y * d;
                m7 = a.Z * a.Z; m8 = a.Z * d;
                m9 = d * d;
            }

            /// <summary>
            /// Determinant(0, 1, 2, 1, 4, 5, 2, 5, 7)
            /// </summary>
            /// <returns></returns>
            internal double Determinant1()
            {
                double det =
                    m0 * m4 * m7 +
                    m2 * m1 * m5 +
                    m1 * m5 * m2 -
                    m2 * m4 * m2 -
                    m0 * m5 * m5 -
                    m1 * m1 * m7;
                return det;
            }

            /// <summary>
            /// Determinant(1, 2, 3, 4, 5, 6, 5, 7, 8)
            /// </summary>
            /// <returns></returns>
            internal double Determinant2()
            {
                double det =
                    m1 * m5 * m8 +
                    m3 * m4 * m7 +
                    m2 * m6 * m5 -
                    m3 * m5 * m5 -
                    m1 * m6 * m7 -
                    m2 * m4 * m8;
                return det;
            }

            /// <summary>
            /// Determinant(0, 2, 3, 1, 5, 6, 2, 7, 8)
            /// </summary>
            /// <returns></returns>
            internal double Determinant3()
            {
                double det =
                    m0 * m5 * m8 +
                    m3 * m1 * m7 +
                    m2 * m6 * m2 -
                    m3 * m5 * m2 -
                    m0 * m6 * m7 -
                    m2 * m1 * m8;
                return det;
            }

            /// <summary>
            /// Determinant(0, 1, 3, 1, 4, 6, 2, 5, 8)
            /// </summary>
            /// <returns></returns>
            internal double Determinant4()
            {
                double det =
                    m0 * m4 * m8 +
                    m3 * m1 * m5 +
                    m1 * m6 * m2 -
                    m3 * m4 * m2 -
                    m0 * m6 * m5 -
                    m1 * m1 * m8;
                return det;
            }

            /// <summary>
            /// Adds two matrixes together.
            /// </summary>
            /// <param name="a">The left hand side.</param>
            /// <param name="b">The right hand side.</param>
            /// <param name="result">The resulting matrix.</returns>
            public static void Add(ref SymmetricMatrix a, ref SymmetricMatrix b, ref SymmetricMatrix result)
            {
                result.m0 = a.m0 + b.m0;
                result.m1 = a.m1 + b.m1;
                result.m2 = a.m2 + b.m2;
                result.m3 = a.m3 + b.m3;
                result.m4 = a.m4 + b.m4;
                result.m5 = a.m5 + b.m5;
                result.m6 = a.m6 + b.m6;
                result.m7 = a.m7 + b.m7;
                result.m8 = a.m8 + b.m8;
                result.m9 = a.m9 + b.m9;
            }

            /// <summary>
            /// Implements a += b, for matrices
            /// </summary>
            public static void AddInto(ref SymmetricMatrix a, ref SymmetricMatrix b)
            {
                a.m0 += b.m0;
                a.m1 += b.m1;
                a.m2 += b.m2;
                a.m3 += b.m3;
                a.m4 += b.m4;
                a.m5 += b.m5;
                a.m6 += b.m6;
                a.m7 += b.m7;
                a.m8 += b.m8;
                a.m9 += b.m9;
            }
        }

        private struct Triangle
        {
            public int v0;
            public int v1;
            public int v2;

            public double err0;
            public double err1;
            public double err2;
            public double err3;

            public bool deleted;
            public bool dirty;
            public Vector3d n;

            public int this[int index]
            {
                get
                {
                    return (index == 0 ? v0 : (index == 1 ? v1 : v2));
                }
                set
                {
                    switch (index)
                    {
                        case 0:
                            v0 = value;
                            break;
                        case 1:
                            v1 = value;
                            break;
                        case 2:
                            v2 = value;
                            break;
                        default:
                            throw new IndexOutOfRangeException();
                    }
                }
            }

            public void GetErrors(double[] err)
            {
                err[0] = err0;
                err[1] = err1;
                err[2] = err2;
            }
        }

        private struct Vertex
        {
            public Vector3d p;
            public int tstart;
            public int tcount;
            public SymmetricMatrix q;
            public bool IsEdge;
        }

        private struct Ref
        {
            public int tid;
            public int tvertex;
        }
    }
}