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


using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MeshDecimator.Algorithms2
{
    /// <summary>
    /// A double precision 3D vector.
    /// </summary>
    public struct Vector3d
    {
        public double x;
        public double y;
        public double z;

        /// <summary>
        /// Creates a new vector.
        /// </summary>
        /// <param name="x">The x value.</param>
        /// <param name="y">The y value.</param>
        /// <param name="z">The z value.</param>
        public Vector3d(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// Adds two vectors.
        /// </summary>
        /// <param name="a">The first vector.</param>
        /// <param name="b">The second vector.</param>
        /// <returns>The resulting vector.</returns>
        public static Vector3d operator +(Vector3d a, Vector3d b)
        {
            return new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        /// <summary>
        /// Subtracts two vectors.
        /// </summary>
        /// <param name="a">The first vector.</param>
        /// <param name="b">The second vector.</param>
        /// <returns>The resulting vector.</returns>
        public static Vector3d operator -(Vector3d a, Vector3d b)
        {
            return new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        /// <summary>
        /// Dot Product of two vectors.
        /// </summary>
        /// <param name="a">The left hand side vector.</param>
        /// <param name="b">The right hand side vector.</param>
        /// <returns>The dot product value.</returns>
        public static double Dot(ref Vector3d a, ref Vector3d b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        /// <summary>
        /// Scales the vector uniformly.
        /// </summary>
        /// <param name="a">The vector.</param>
        /// <param name="d">The scaling value.</param>
        /// <returns>The resulting vector.</returns>
        public static Vector3d operator *(Vector3d a, double d)
        {
            return new Vector3d(a.x * d, a.y * d, a.z * d);
        }

        /// <summary>
        /// Gets a normalized vector from this vector.
        /// </summary>
        public Vector3d Normalized
        {
            get
            {
                double square = System.Math.Sqrt(x * x + y * y + z * z);
                return new Vector3d(x / square, y / square, z / square);
            }
        }

        /// <summary>
        /// Cross Product of two vectors.
        /// </summary>
        /// <param name="a">The left hand side vector.</param>
        /// <param name="b">The right hand side vector.</param>
        public static Vector3d Cross(ref Vector3d a, ref Vector3d b)
        {
            return new Vector3d(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x);
        }
    }

    /// <summary>
    /// A resizable array.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    internal sealed class ResizableArray<T>
    {
        private T[] data;

        /// <summary>
        /// Gets the length of this array.
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// Gets the internal data buffer for this array.
        /// </summary>
        public T[] Data { get { return data; } }

        /// <summary>
        /// Gets or sets the element value at a specific index.
        /// </summary>
        /// <param name="index">The element index.</param>
        /// <returns>The element value.</returns>
        public T this[int index]
        {
            get { return Data[index]; }
            set { Data[index] = value; }
        }

        /// <summary>
        /// Creates a new resizable array.
        /// </summary>
        /// <param name="length">The initial array length.</param>
        public ResizableArray(int length)
        {
            data = new T[length];
            Length = length;
        }

        /// <summary>
        /// Resizes this array.
        /// </summary>
        /// <param name="length">The new length.</param>
        public void Resize(int length)
        {
            if (length != Data.Length)
            {
                Array.Resize(ref data, length);
            }

            // Don't worry about downsizing

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
                Array.Resize(ref data, (int)((Length + 1)* 1.2));
            }

            Data[Length++] = item;
        }
    }

    /// <summary>
    /// The fast quadric mesh simplification algorithm.
    /// </summary>
    public class FastQuadricMeshSimplification
    {
        private const double DoubleEpsilon = 1.0E-3;

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
            /// Creates a symmetric matrix with a value in each component.
            /// </summary>
            /// <param name="c">The component value.</param>
            public SymmetricMatrix(double c = 0)
            {
                this.m0 = c;
                this.m1 = c;
                this.m2 = c;
                this.m3 = c;
                this.m4 = c;
                this.m5 = c;
                this.m6 = c;
                this.m7 = c;
                this.m8 = c;
                this.m9 = c;
            }

            /// <summary>
            /// Creates a symmetric matrix.
            /// </summary>
            /// <param name="m11">The m11 component.</param>
            /// <param name="m12">The m12 component.</param>
            /// <param name="m13">The m13 component.</param>
            /// <param name="m14">The m14 component.</param>
            /// <param name="m22">The m22 component.</param>
            /// <param name="m23">The m23 component.</param>
            /// <param name="m24">The m24 component.</param>
            /// <param name="m33">The m33 component.</param>
            /// <param name="m34">The m34 component.</param>
            /// <param name="m44">The m44 component.</param>
            public SymmetricMatrix(double m11, double m12, double m13, double m14,
                double m22, double m23, double m24,
                double m33, double m34,
                double m44)
            {
                m0 = m11; m1 = m12; m2 = m13; m3 = m14;
                m4 = m22; m5 = m23; m6 = m24;
                m7 = m33; m8 = m34;
                m9 = m44;
            }

            /// <summary>
            /// Creates a symmetric matrix from a plane.
            /// </summary>
            /// <param name="a">The plane x-component.</param>
            /// <param name="b">The plane y-component</param>
            /// <param name="c">The plane z-component</param>
            /// <param name="d">The plane w-component</param>
            public SymmetricMatrix(double a, double b, double c, double d)
            {
                m0 = a * a; m1 = a * b; m2 = a * c; m3 = a * d;
                m4 = b * b; m5 = b * c; m6 = b * d;
                m7 = c * c; m8 = c * d;
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
            /// <returns>The resulting matrix.</returns>
            public static SymmetricMatrix operator +(SymmetricMatrix a, SymmetricMatrix b)
            {
                return new SymmetricMatrix(
                    a.m0 + b.m0, a.m1 + b.m1, a.m2 + b.m2, a.m3 + b.m3,
                    a.m4 + b.m4, a.m5 + b.m5, a.m6 + b.m6,
                    a.m7 + b.m7, a.m8 + b.m8,
                    a.m9 + b.m9);
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

            public Triangle(int v0, int v1, int v2)
            {
                this.v0 = v0;
                this.v1 = v1;
                this.v2 = v2;

                err0 = 0;
                err1 = 0;
                err2 = 0;
                err3 = 0;

                deleted = false;
                dirty = false;

                n = new Vector3d();
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
            public bool border;

            public Vertex(Vector3d p)
            {
                this.p = p;
                tstart = 0;
                tcount = 0;
                q = new SymmetricMatrix();
                border = true;
            }
        }

        private struct Ref
        {
            public int tid;
            public int tvertex;

            public void Set(int tid, int tvertex)
            {
                this.tid = tid;
                this.tvertex = tvertex;
            }
        }

        private int maxIterationCount = 100;

        private ResizableArray<Triangle> trianglesRA;
        private ResizableArray<Vertex> verticesRA;
        private ResizableArray<Ref> refsRA = new ResizableArray<Ref>(0);

        private Triangle[] triangles { get { return trianglesRA.Data; } }

        private Vertex[] vertices { get { return verticesRA.Data; } }

        private Ref[] refs { get { return refsRA.Data; } }

        // Pre-allocated buffers
        private double[] errArr = new double[3];

        /// <summary>
        /// Main simplification function
        /// </summary>
        public void SimplifyMesh(int targetCount, double agressiveness = 7.0, bool verbose = false)
        {
            int deletedTriangles = 0;
            ResizableArray<bool> deleted0 = new ResizableArray<bool>(20);
            ResizableArray<bool> deleted1 = new ResizableArray<bool>(20);
            int initialCount = trianglesRA.Length;

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
                    UpdateMesh(iteration);
                }

                // Clear dirty flag
                for (int i = 0; i < trianglesRA.Length; i++)
                {
                    triangles[i].dirty = false;
                }

                // All triangles with edges below the threshold will be removed
                //
                // The following numbers works well for most models.
                // If it does not, try to adjust the 3 parameters
                double threshold = 0.000000001 * System.Math.Pow(iteration + 3, agressiveness);

                // target number of triangles reached ? Then break
                if (verbose && (iteration % 5) == 0)
                {
                    Debug.WriteLine("iteration {0} - triangles {1} threshold {2}", iteration, initialCount - deletedTriangles, threshold);
                }

                // Remove vertices & mark deleted triangles
                RemoveVertexPass(initialCount, targetCount, threshold, deleted0, deleted1, ref deletedTriangles);
            }

            CompactMesh();
        }

        /// <summary>
        /// Decimates the mesh without losing any quality.
        /// </summary>
        public void SimplifyMeshLossless(bool verbose = false)
        {
            int deletedTriangles = 0;
            ResizableArray<bool> deleted0 = new ResizableArray<bool>(20);
            ResizableArray<bool> deleted1 = new ResizableArray<bool>(20);
            int initialCount = trianglesRA.Length;

            for (int iteration = 0; iteration < 9999; iteration++)
            {
                Debug.WriteLine(DateTime.Now + "\tStarting updatemesh");
                // Update mesh constantly
                UpdateMesh(iteration);
                Debug.WriteLine(DateTime.Now + "\tEnd updatemesh");

                ReportStatus(iteration, initialCount, trianglesRA.Length, -1);

                // Clear dirty flag
                for (int i = 0; i < trianglesRA.Length; i++)
                {
                    triangles[i].dirty = false;
                }

                // All triangles with edges below the threshold will be removed
                //
                // The following numbers works well for most models.
                // If it does not, try to adjust the 3 parameters
                double threshold = DoubleEpsilon; //1.0E-3 EPS;
                if (verbose)
                {
                    Debug.WriteLine("Lossless iteration {0}", iteration);
                }

                // Remove vertices & mark deleted triangles
                RemoveVertexPass(initialCount, 0, threshold, deleted0, deleted1, ref deletedTriangles);

                if (deletedTriangles <= 0)
                {
                    break;
                }

                deletedTriangles = 0;
            }

            CompactMesh();
        }

        private void ReportStatus(int iteration, int startTrisCount, int triangleCount, int v)
        {
            Debug.WriteLine(DateTime.Now + "Iteration: " + iteration + ", startTrisCount: " + startTrisCount + ", triangleCount:" + triangleCount + ", v:" + v);
        }

        /// <summary>
        /// Check if a triangle flips when this edge is removed
        /// </summary>
        private bool Flipped(ref Vector3d p, int i0, int i1, ref Vertex v0, bool[] deleted)
        {
            for (int k = 0; k < v0.tcount; k++)
            {
                Ref r = refs[v0.tstart + k];
                Triangle t = triangles[r.tid];
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

                Vector3d d1 = (vertices[id1].p - p).Normalized;
                Vector3d d2 = (vertices[id2].p - p).Normalized;
                double dot = Vector3d.Dot(ref d1, ref d2);
                if (Math.Abs(dot) > 0.999)
                {
                    return true;
                }

                Vector3d n = Vector3d.Cross(ref d1, ref d2).Normalized;
                deleted[k] = false;
                dot = Vector3d.Dot(ref n, ref triangles[r.tid].n);
                if (dot < 0.2)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Update triangle connections and edge error after a edge is collapsed.
        /// </summary>
        private void UpdateTriangles(int i0, ref Vertex v, ResizableArray<bool> deleted, ref int deletedTriangles)
        {
            Vector3d p;
            int tcount = v.tcount;
            for (int k = 0; k < tcount; k++)
            {
                Ref r = refs[v.tstart + k];
                int tid = r.tid;
                Triangle t = triangles[tid];
                if (t.deleted)
                {
                    continue;
                }

                if (deleted[k])
                {
                    triangles[tid].deleted = true;
                    ++deletedTriangles;
                    continue;
                }

                t[r.tvertex] = i0;
                t.dirty = true;
                t.err0 = CalculateError(ref vertices[t.v0], ref vertices[t.v1], out p);
                t.err1 = CalculateError(ref vertices[t.v1], ref vertices[t.v2], out p);
                t.err2 = CalculateError(ref vertices[t.v2], ref vertices[t.v0], out p);
                t.err3 = Math.Min(t.err0, Math.Min(t.err1, t.err2));
                triangles[tid] = t;
                refsRA.Add(r);
            }
        }

        /// <summary>
        /// Remove vertices and mark deleted triangles
        /// </summary>
        private void RemoveVertexPass(
            int startTrisCount,
            int targetTrisCount,
            double threshold,
            ResizableArray<bool> deleted0,
            ResizableArray<bool> deleted1,
            ref int deletedTriangles)
        {
            int triangleCount = this.trianglesRA.Length;

            Vector3d p;
            for (int tid = 0; tid < triangleCount; tid++)
            {
                if (triangles[tid].dirty || triangles[tid].deleted || triangles[tid].err3 > threshold)
                {
                    continue;
                }

                triangles[tid].GetErrors(errArr);
                for (int edgeIndex = 0; edgeIndex < 3; edgeIndex++)
                {
                    if (errArr[edgeIndex] > threshold)
                    {
                        continue;
                    }

                    int nextEdgeIndex = ((edgeIndex + 1) % 3);
                    int i0 = triangles[tid][edgeIndex];
                    int i1 = triangles[tid][nextEdgeIndex];

                    // Border check
                    if (vertices[i0].border != vertices[i1].border)
                    {
                        continue;
                    }

                    // Compute vertex to collapse to
                    CalculateError(ref vertices[i0], ref vertices[i1], out p);
                    deleted0.Resize(vertices[i0].tcount); // normals temporarily
                    deleted1.Resize(vertices[i1].tcount); // normals temporarily

                    // Don't remove if flipped
                    if (Flipped(ref p, i0, i1, ref vertices[i0], deleted0.Data))
                    {
                        continue;
                    }

                    if (Flipped(ref p, i1, i0, ref vertices[i1], deleted1.Data))
                    {
                        continue;
                    }

                    // Not flipped, so remove edge
                    vertices[i0].p = p;
                    vertices[i0].q = vertices[i1].q + vertices[i0].q;

                    int tstart = refs.Length;
                    UpdateTriangles(i0, ref vertices[i0], deleted0, ref deletedTriangles);
                    UpdateTriangles(i0, ref vertices[i1], deleted1, ref deletedTriangles);

                    int tcount = refs.Length - tstart;
                    if (tcount <= vertices[i0].tcount)
                    {
                        // save ram
                        if (tcount > 0)
                        {
                            Array.Copy(refs, tstart, refs, vertices[i0].tstart, tcount);
                        }
                    }
                    else
                    {
                        // append
                        vertices[i0].tstart = tstart;
                    }

                    vertices[i0].tcount = tcount;
                    break;
                }
            }
        }

        /// <summary>
        /// Compact triangles, compute edge error and build reference list.
        /// </summary>
        /// <param name="iteration">The iteration index.</param>
        private void UpdateMesh(int iteration)
        {
            Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.compactTri");
            if (iteration > 0) // compact triangles
            {
                int dst = 0;
                for (int i = 0; i < trianglesRA.Length; i++)
                {
                    if (!triangles[i].deleted)
                    {
                        if (dst != i)
                        {
                            triangles[dst] = triangles[i];
                        }
                        dst++;
                    }
                }

                trianglesRA.Resize(dst);
            }
            Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.compactTri");

            // Init Quadrics by Plane & Edge Errors
            //
            // required at the beginning ( iteration == 0 )
            // recomputing during the simplification is not required,
            // but mostly improves the result for closed meshes
            if (iteration == 0)
            {
                for (int i = 0; i < verticesRA.Length; i++)
                {
                    vertices[i].q = new SymmetricMatrix();
                }

                Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.update Tri");
                for (int i = 0; i < trianglesRA.Length; i++)
                {
                    int v0 = triangles[i].v0;
                    int v1 = triangles[i].v1;
                    int v2 = triangles[i].v2;

                    Vector3d p0 = vertices[v0].p;
                    Vector3d p1 = vertices[v1].p;
                    Vector3d p2 = vertices[v2].p;
                    Vector3d p10 = p1 - p0;
                    Vector3d p20 = p2 - p0;
                    Vector3d n = Vector3d.Cross(ref p10, ref p20).Normalized;
                    triangles[i].n = n;

                    var sm = new SymmetricMatrix(n.x, n.y, n.z, -Vector3d.Dot(ref n, ref p0));
                    vertices[v0].q += sm;
                    vertices[v1].q += sm;
                    vertices[v2].q += sm;
                }
                Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.update Tri");

                Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.calc edge error");
                for (int i = 0; i < trianglesRA.Length; i++)
                {
                    // Calc Edge Error
                    var triangle = triangles[i];
                    Vector3d dummy;
                    triangles[i].err0 = CalculateError(ref vertices[triangle.v0], ref vertices[triangle.v1], out dummy);
                    triangles[i].err1 = CalculateError(ref vertices[triangle.v1], ref vertices[triangle.v2], out dummy);
                    triangles[i].err2 = CalculateError(ref vertices[triangle.v2], ref vertices[triangle.v0], out dummy);
                    triangles[i].err3 = Math.Min(triangles[i].err0, Math.Min(triangles[i].err1, triangles[i].err2));
                }
                Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.calc edge error");
            }

            // Init Reference ID list
            Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Init Reference ID list");

            for (int i = 0; i < verticesRA.Length; i++)
            {
                vertices[i].tstart = 0;
                vertices[i].tcount = 0;
            }
            Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.Init Reference ID list");

            Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Update vertex triangle counts");
            for (int i = 0; i < trianglesRA.Length; i++)
            {
                Triangle t = triangles[i];
                vertices[t.v0].tcount++;
                vertices[t.v1].tcount++;
                vertices[t.v2].tcount++;
            }
            Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.Update vertex triangle counts");

            Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Update vertex triangle counts, part 2");
            int tstartX = 0;
            for (int i = 0; i < verticesRA.Length; i++)
            {
                vertices[i].tstart = tstartX;
                if (vertices[i].tcount > 0)
                {
                    tstartX += vertices[i].tcount;
                    vertices[i].tcount = 0;
                }
            }
            Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.Update vertex triangle counts, part 2");

            // Write References
            refsRA.Resize(tstartX);

            Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Update vertex triangle counts, with Ref");
            for (int i = 0; i < trianglesRA.Length; i++)
            {
                int v0 = triangles[i].v0;
                int v1 = triangles[i].v1;
                int v2 = triangles[i].v2;
                int start0 = vertices[v0].tstart;
                int count0 = vertices[v0].tcount;
                int start1 = vertices[v1].tstart;
                int count1 = vertices[v1].tcount;
                int start2 = vertices[v2].tstart;
                int count2 = vertices[v2].tcount;

                refs[start0 + count0].Set(i, 0);
                refs[start1 + count1].Set(i, 1);
                refs[start2 + count2].Set(i, 2);

                ++vertices[v0].tcount;
                ++vertices[v1].tcount;
                ++vertices[v2].tcount;
            }
            Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.Update vertex triangle counts, with Ref");

            // Identify boundary : vertices[].border=0,1
            if (iteration == 0)
            {
                var vcount = new List<int>(8);
                var vids = new List<int>(8);
                int vsize = 0;
                Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Update vertex is border");
                for (int i = 0; i < verticesRA.Length; i++)
                {
                    vertices[i].border = false;
                }
                Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.Update vertex is border");

                int ofs;
                int id;
                Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Update vertex is border, part 2");
                int borderVertexCount = 0;
                for (int i = 0; i < verticesRA.Length; i++)
                {
                    int tstart = vertices[i].tstart;
                    int tcount = vertices[i].tcount;
                    vcount.Clear();
                    vids.Clear();
                    vsize = 0;

                    for (int j = 0; j < tcount; j++)
                    {
                        int tid = refs[tstart + j].tid;
                        for (int k = 0; k < 3; k++)
                        {
                            ofs = 0;
                            id = triangles[tid][k];
                            while (ofs < vsize)
                            {
                                if (vids[ofs] == id)
                                {
                                    break;
                                }

                                ++ofs;
                            }

                            if (ofs == vsize)
                            {
                                vcount.Add(1);
                                vids.Add(id);
                                ++vsize;
                            }
                            else
                            {
                                vcount[ofs]++;
                            }
                        }
                    }

                    for (int j = 0; j < vsize; j++)
                    {
                        if (vcount[j] == 1)
                        {
                            vertices[vids[j]].border = true;
                            ++borderVertexCount;
                        }
                    }
                }
                Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.Update vertex is border, part 2");

            }
        }

        /// <summary>
        /// Finally compact mesh before exiting.
        /// </summary>
        private void CompactMesh()
        {
            for (int i = 0; i < verticesRA.Length; i++)
            {
                vertices[i].tcount = 0;
            }

            int dst = 0;
            for (int i = 0; i < trianglesRA.Length; i++)
            {
                var t = triangles[i];
                if (!t.deleted)
                {
                    triangles[dst++] = t;
                    vertices[t.v0].tcount = 1;
                    vertices[t.v1].tcount = 1;
                    vertices[t.v2].tcount = 1;
                }
            }

            this.trianglesRA.Resize(dst);

            dst = 0;
            for (int i = 0; i < verticesRA.Length; i++)
            {
                var v = vertices[i];
                if (v.tcount > 0)
                {
                    v.tstart = dst;
                    vertices[i] = v;
                    if (dst != i)
                    {
                        vertices[dst].p = v.p;
                    }

                    dst++;
                }
            }

            for (int i = 0; i < trianglesRA.Length; i++)
            {
                var t = triangles[i];
                t.v0 = vertices[t.v0].tstart;
                t.v1 = vertices[t.v1].tstart;
                t.v2 = vertices[t.v2].tstart;
                triangles[i] = t;
            }

            verticesRA.Resize(dst);
        }

        // Error between vertex and Quadric
        private double VertexError(ref SymmetricMatrix q, double x, double y, double z)
        {
            return
                1 * q.m0 * x * x +
                2 * q.m1 * x * y +
                2 * q.m2 * x * z +
                2 * q.m3 * x +
                1 * q.m4 * y * y +
                2 * q.m5 * y * z +
                2 * q.m6 * y +
                1 * q.m7 * z * z +
                2 * q.m8 * z +
                1 * q.m9;
        }

        private double CalculateError(ref Vertex vert0, ref Vertex vert1, out Vector3d result)
        {
            // compute interpolated vertex
            SymmetricMatrix q = vert0.q + vert1.q;
            bool border = vert0.border & vert1.border;
            double error = 0;
            double det = q.Determinant1();
            if (det != 0 && !border)
            {
                // q_delta is invertible
                result = new Vector3d(
                    -1.0 / det * q.Determinant2(),  // vx = A41/det(q_delta)
                    +1.0 / det * q.Determinant3(),  // vy = A42/det(q_delta)
                    -1.0 / det * q.Determinant4()); // vz = A43/det(q_delta)
                error = VertexError(ref q, result.x, result.y, result.z);
            }
            else
            {
                // det = 0 -> try to find best result
                Vector3d p1 = vert0.p;
                Vector3d p2 = vert1.p;
                Vector3d p3 = (p1 + p2) * 0.5;
                double error1 = VertexError(ref q, p1.x, p1.y, p1.z);
                double error2 = VertexError(ref q, p2.x, p2.y, p2.z);
                double error3 = VertexError(ref q, p3.x, p3.y, p3.z);
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
                    result = p3;
                }
            }

            return error;
        }

        /// <summary>
        /// Initializes the algorithm with the original mesh.
        /// </summary>
        public void Initialize(Vector3d[] verticesIn, int[] indices)
        {
            verticesRA = new ResizableArray<Vertex>(verticesIn.Length);
            for (int i = 0; i < verticesIn.Length; i++)
            {
                vertices[i] = new Vertex(verticesIn[i]);
            }

            trianglesRA = new ResizableArray<Triangle>(indices.Length / 3);
            int triangleIndex = 0;

            for (int i = 0; i < indices.Length / 3; i++)
            {
                int offset = i * 3;
                int v0 = indices[offset + 0];
                int v1 = indices[offset + 1];
                int v2 = indices[offset + 2];
                triangles[triangleIndex++] = new Triangle(v0, v1, v2);
            }
        }

        public int[] GetIndices()
        {
            int[] indices = new int[trianglesRA.Length * 3];
            for (int i = 0; i < trianglesRA.Length; i++)
            {
                var triangle = triangles[i];
                indices[i * 3 + 0] = triangle.v0;
                indices[i * 3 + 1] = triangle.v1;
                indices[i * 3 + 2] = triangle.v2;
            }

            return indices;
        }

        public Vector3d[] GetVertices()
        {
            var verticesOut = new Vector3d[verticesRA.Length];

            for (int i = 0; i < verticesRA.Length; i++)
            {
                verticesOut[i] = vertices[i].p;
            }

            return verticesOut;
        }
    }
}