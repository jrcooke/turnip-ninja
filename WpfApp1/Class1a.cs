﻿/*
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
        private static T[] emptyArr = new T[0];

        /// <summary>
        /// Gets the length of this array.
        /// </summary>
        public int Length { get; private set; } = 0;

        /// <summary>
        /// Gets the internal data buffer for this array.
        /// </summary>
        public T[] Data { get; private set; } = null;

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
        /// <param name="capacity">The initial array capacity.</param>
        public ResizableArray(int capacity)
            : this(capacity, 0)
        {
        }

        /// <summary>
        /// Creates a new resizable array.
        /// </summary>
        /// <param name="capacity">The initial array capacity.</param>
        /// <param name="length">The initial length of the array.</param>
        public ResizableArray(int capacity, int length)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");
            else if (length < 0 || length > capacity)
                throw new ArgumentOutOfRangeException("length");

            if (capacity > 0)
                Data = new T[capacity];
            else
                Data = emptyArr;

            Length = length;
        }

        private void IncreaseCapacity(int capacity)
        {
            T[] newItems = new T[capacity];
            Array.Copy(Data, 0, newItems, 0, Math.Min(Length, capacity));
            Data = newItems;
        }

        /// <summary>
        /// Resizes this array.
        /// </summary>
        /// <param name="length">The new length.</param>
        /// <param name="trimExess">If exess memory should be trimmed.</param>
        public void Resize(int length, bool trimExess = false)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException("capacity");

            if (length > Data.Length)
            {
                IncreaseCapacity(length);
            }
            else if (length < this.Length)
            {
                //Array.Clear(items, capacity, length - capacity);
            }

            this.Length = length;

            if (trimExess)
            {
                TrimExcess();
            }
        }

        /// <summary>
        /// Trims any excess memory for this array.
        /// </summary>
        public void TrimExcess()
        {
            if (Data.Length == Length) // Nothing to do
                return;

            T[] newItems = new T[Length];
            Array.Copy(Data, 0, newItems, 0, Length);
            Data = newItems;
        }

        /// <summary>
        /// Adds a new item to the end of this array.
        /// </summary>
        /// <param name="item">The new item.</param>
        public void Add(T item)
        {
            if (Length >= Data.Length)
            {
                IncreaseCapacity(Data.Length << 1);
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

                this.err0 = 0;
                this.err1 = 0;
                this.err2 = 0;
                this.err3 = 0;

                this.deleted = false;
                this.dirty = false;

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
                this.tstart = 0;
                this.tcount = 0;
                this.q = new SymmetricMatrix();
                this.border = true;
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

        private ResizableArray<Triangle> triangles = new ResizableArray<Triangle>(0);
        private ResizableArray<Vertex> vertices = new ResizableArray<Vertex>(0);
        private ResizableArray<Ref> refs = new ResizableArray<Ref>(0);

        private int remainingVertices = 0;

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
            var triangles = this.triangles.Data;
            int triangleCount = this.triangles.Length;
            int startTrisCount = triangleCount;
            var vertices = this.vertices.Data;

            for (int iteration = 0; iteration < maxIterationCount; iteration++)
            {
                ReportStatus(iteration, startTrisCount, (startTrisCount - deletedTriangles), targetCount);
                if ((startTrisCount - deletedTriangles) <= targetCount)
                {
                    break;
                }

                // Update mesh once in a while
                if (iteration % 5 == 0)
                {
                    UpdateMesh(iteration);
                    triangles = this.triangles.Data;
                    triangleCount = this.triangles.Length;
                    vertices = this.vertices.Data;
                }

                // Clear dirty flag
                for (int i = 0; i < triangleCount; i++)
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
                    Debug.WriteLine("iteration {0} - triangles {1} threshold {2}", iteration, startTrisCount - deletedTriangles, threshold);
                }

                // Remove vertices & mark deleted triangles
                RemoveVertexPass(startTrisCount, targetCount, threshold, deleted0, deleted1, ref deletedTriangles);
            }

            CompactMesh();
        }

        /// <summary>
        /// Decimates the mesh without losing any quality.
        /// </summary>
        public void SimplifyMeshLossless(bool verbose = false)
        {
            int deletedTris = 0;
            ResizableArray<bool> deleted0 = new ResizableArray<bool>(20);
            ResizableArray<bool> deleted1 = new ResizableArray<bool>(20);
            var triangles = this.triangles.Data;
            int triangleCount = this.triangles.Length;
            int startTrisCount = triangleCount;
            var vertices = this.vertices.Data;

            for (int iteration = 0; iteration < 9999; iteration++)
            {
                Debug.WriteLine(DateTime.Now + "\tStarting updatemesh");
                // Update mesh constantly
                UpdateMesh(iteration);
                Debug.WriteLine(DateTime.Now + "\tEnd updatemesh");

                triangles = this.triangles.Data;
                triangleCount = this.triangles.Length;
                vertices = this.vertices.Data;

                ReportStatus(iteration, startTrisCount, triangleCount, -1);

                // Clear dirty flag
                for (int i = 0; i < triangleCount; i++)
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
                RemoveVertexPass(startTrisCount, 0, threshold, deleted0, deleted1, ref deletedTris);

                if (deletedTris <= 0)
                {
                    break;
                }

                deletedTris = 0;
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
            int tcount = v0.tcount;
            var refs = this.refs.Data;
            var triangles = this.triangles.Data;
            var vertices = this.vertices.Data;
            for (int k = 0; k < tcount; k++)
            {
                Ref r = refs[v0.tstart + k];
                if (triangles[r.tid].deleted)
                {
                    continue;
                }

                int s = r.tvertex;
                int id1 = triangles[r.tid][(s + 1) % 3];
                int id2 = triangles[r.tid][(s + 2) % 3];
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
            var triangles = this.triangles.Data;
            var vertices = this.vertices.Data;
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
                refs.Add(r);
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
            ref int deletedTris)
        {
            var triangles = this.triangles.Data;
            int triangleCount = this.triangles.Length;
            var vertices = this.vertices.Data;

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
                    vertices[i0].q += vertices[i1].q;

                    int tstart = refs.Length;
                    UpdateTriangles(i0, ref vertices[i0], deleted0, ref deletedTris);
                    UpdateTriangles(i0, ref vertices[i1], deleted1, ref deletedTris);

                    int tcount = refs.Length - tstart;
                    if (tcount <= vertices[i0].tcount)
                    {
                        // save ram
                        if (tcount > 0)
                        {
                            var refsArr = refs.Data;
                            Array.Copy(refsArr, tstart, refsArr, vertices[i0].tstart, tcount);
                        }
                    }
                    else
                    {
                        // append
                        vertices[i0].tstart = tstart;
                    }

                    vertices[i0].tcount = tcount;
                    --remainingVertices;
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
            var triangles = this.triangles.Data;
            var vertices = this.vertices.Data;

            int triangleCount = this.triangles.Length;
            int vertexCount = this.vertices.Length;
            Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.compactTri");
            if (iteration > 0) // compact triangles
            {
                int dst = 0;
                for (int i = 0; i < triangleCount; i++)
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
                this.triangles.Resize(dst);
                triangles = this.triangles.Data;
                triangleCount = dst;
            }
            Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.compactTri");

            // Init Quadrics by Plane & Edge Errors
            //
            // required at the beginning ( iteration == 0 )
            // recomputing during the simplification is not required,
            // but mostly improves the result for closed meshes
            if (iteration == 0)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i].q = new SymmetricMatrix();
                }

                int v0, v1, v2;
                Vector3d n, p0, p1, p2, p10, p20, dummy;
                SymmetricMatrix sm;
                Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.update Tri");
                for (int i = 0; i < triangleCount; i++)
                {
                    v0 = triangles[i].v0;
                    v1 = triangles[i].v1;
                    v2 = triangles[i].v2;

                    p0 = vertices[v0].p;
                    p1 = vertices[v1].p;
                    p2 = vertices[v2].p;
                    p10 = p1 - p0;
                    p20 = p2 - p0;
                    n = Vector3d.Cross(ref p10, ref p20).Normalized;
                    triangles[i].n = n;

                    sm = new SymmetricMatrix(n.x, n.y, n.z, -Vector3d.Dot(ref n, ref p0));
                    vertices[v0].q += sm;
                    vertices[v1].q += sm;
                    vertices[v2].q += sm;
                }
                Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.update Tri");

                Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.calc edge error");
                for (int i = 0; i < triangleCount; i++)
                {
                    // Calc Edge Error
                    var triangle = triangles[i];
                    triangles[i].err0 = CalculateError(ref vertices[triangle.v0], ref vertices[triangle.v1], out dummy);
                    triangles[i].err1 = CalculateError(ref vertices[triangle.v1], ref vertices[triangle.v2], out dummy);
                    triangles[i].err2 = CalculateError(ref vertices[triangle.v2], ref vertices[triangle.v0], out dummy);
                    triangles[i].err3 = Math.Min(triangles[i].err0, Math.Min(triangles[i].err1, triangles[i].err2));
                }
                Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.calc edge error");
            }

            // Init Reference ID list
            Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Init Reference ID list");

            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i].tstart = 0;
                vertices[i].tcount = 0;
            }
            Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.Init Reference ID list");

            Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Update vertex triangle counts");
            for (int i = 0; i < triangleCount; i++)
            {
                ++vertices[triangles[i].v0].tcount;
                ++vertices[triangles[i].v1].tcount;
                ++vertices[triangles[i].v2].tcount;
            }
            Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.Update vertex triangle counts");

            Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Update vertex triangle counts, part 2");
            int tstartX = 0;
            remainingVertices = 0;
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i].tstart = tstartX;
                if (vertices[i].tcount > 0)
                {
                    tstartX += vertices[i].tcount;
                    vertices[i].tcount = 0;
                    ++remainingVertices;
                }
            }
            Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.Update vertex triangle counts, part 2");

            // Write References
            this.refs.Resize(tstartX);
            var refsX = this.refs.Data;
            Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Update vertex triangle counts, with Ref");
            for (int i = 0; i < triangleCount; i++)
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

                refsX[start0 + count0].Set(i, 0);
                refsX[start1 + count1].Set(i, 1);
                refsX[start2 + count2].Set(i, 2);

                ++vertices[v0].tcount;
                ++vertices[v1].tcount;
                ++vertices[v2].tcount;
            }
            Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.Update vertex triangle counts, with Ref");

            // Identify boundary : vertices[].border=0,1
            if (iteration == 0)
            {
                var refs = this.refs.Data;

                var vcount = new List<int>(8);
                var vids = new List<int>(8);
                int vsize = 0;
                Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Update vertex is border");
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i].border = false;
                }
                Debug.WriteLine(DateTime.Now + "\tEnd updatemesh.Update vertex is border");

                int ofs;
                int id;
                Debug.WriteLine(DateTime.Now + "\tStarting updatemesh.Update vertex is border, part 2");
                int borderVertexCount = 0;
                for (int i = 0; i < vertexCount; i++)
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
                                ++vcount[ofs];
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
            int dst = 0;
            var vertices = this.vertices.Data;
            int vertexCount = this.vertices.Length;
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i].tcount = 0;
            }

            var triangles = this.triangles.Data;
            int triangleCount = this.triangles.Length;
            for (int i = 0; i < triangleCount; i++)
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

            triangleCount = dst;
            this.triangles.Resize(triangleCount);
            triangles = this.triangles.Data;

            dst = 0;
            for (int i = 0; i < vertexCount; i++)
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
                    ++dst;
                }
            }

            for (int i = 0; i < triangleCount; i++)
            {
                var triangle = triangles[i];
                triangle.v0 = vertices[triangle.v0].tstart;
                triangle.v1 = vertices[triangle.v1].tstart;
                triangle.v2 = vertices[triangle.v2].tstart;
                triangles[i] = triangle;
            }

            vertexCount = dst;
            this.vertices.Resize(vertexCount);
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
            vertices.Resize(verticesIn.Length);
            var vertArr = vertices.Data;
            for (int i = 0; i < verticesIn.Length; i++)
            {
                vertArr[i] = new Vertex(verticesIn[i]);
            }

            triangles.Resize(indices.Length / 3);
            var trisArr = triangles.Data;
            int triangleIndex = 0;

            for (int i = 0; i < indices.Length / 3; i++)
            {
                int offset = i * 3;
                int v0 = indices[offset + 0];
                int v1 = indices[offset + 1];
                int v2 = indices[offset + 2];
                trisArr[triangleIndex++] = new Triangle(v0, v1, v2);
            }
        }

        public int[] GetIndices()
        {
            var trisArr = triangles.Data;
            int[] indices = new int[trisArr.Length * 3];
            for (int i = 0; i < trisArr.Length; i++)
            {
                var triangle = trisArr[i];
                indices[i * 3 + 0] = triangle.v0;
                indices[i * 3 + 1] = triangle.v1;
                indices[i * 3 + 2] = triangle.v2;
            }

            return indices;
        }

        public Vector3d[] GetVertices()
        {
            int vertexCount = this.vertices.Length;
            var vertArr = this.vertices.Data;
            var vertices = new Vector3d[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i] = vertArr[i].p;
            }

            return vertices;
        }
    }
}